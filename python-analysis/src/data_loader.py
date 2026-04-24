"""
Load insider trading data from FI CSV files or the AktieKoll REST API.

FI CSV format:
  - Encoding: UTF-16 LE with BOM
  - Delimiter: semicolon
  - Decimal separator: comma (Swedish locale)
  - Thousands separator: space (e.g. "1 234 567,0")
  - Date format: "2025-06-24 13:37:51" or "2025-06-24 00:00:00"
"""

import os
import re
import glob
from pathlib import Path

import pandas as pd
import requests


COLUMN_MAP = {
    "Publiceringsdatum": "publishing_date",
    "Emittent": "company_raw",
    "LEI-kod": "lei",
    "Anmälningsskyldig": "reporting_entity",
    "Person i ledande ställning": "insider_name",
    "Befattning": "position_raw",
    "Närstående": "related_party",
    "Korrigering": "correction",
    "Beskrivning av korrigering": "correction_desc",
    "Är förstagångsrapportering": "first_time_reporting",
    "Är kopplad till aktieprogram": "stock_program",
    "Karaktär": "transaction_type",
    "Instrumenttyp": "instrument_type",
    "Instrumentnamn": "instrument_name",
    "ISIN": "isin",
    "Transaktionsdatum": "transaction_date",
    "Volym": "shares",
    "Volymsenhet": "volume_unit",
    "Pris": "price",
    "Valuta": "currency",
    "Handelsplats": "trading_venue",
    "Status": "status",
}

# Same exclusion filters as the .NET backend
EXCLUDED_TRANSACTION_TYPES = {
    "lån mottaget", "utdelning lämnad", "utdelning mottagen",
    "lösen minskning", "lösen ökning", "lån återgång ökning",
    "lån återgång minskning", "utbyte minskning", "utbyte ökning",
    "utfärdande av instrument", "pantsättning", "pantsättning åter",
    "bodelning minskning", "bodelning ökning", "arv mottagen",
    "konvertering ökning", "lån utlåning",
    "interntransaktion – avyttring", "interntransaktion – förvärv",
    "inlösen egenutfärdat instrument", "fusion ökning",
    "gåva lämnad", "gåva mottagen",
    "koncernintern överföring ökning", "koncernintern överföring minskning",
    "konvertering minskning",
}

EXCLUDED_INSTRUMENT_TYPES = {
    "swap", "ränteswap", "btu", "teckningsrätt", "uniträtt",
    "teckningsrätt/uniträtt", "kreditderivat", "terminskontrakt", "option",
}

POSITION_MAP = {
    "verkställande direktör (vd)": "VD",
    "ekonomichef/finanschef/finansdirektör": "CFO",
    "annan medlem i bolagets administrations-, lednings- eller kontrollorgan": "Ledningsgrupp",
    "arbetstagarrepresentant i styrelsen eller arbetstagarsuppleant": "Arbetstagarrepresentant",
}

_STRIP_COMPANY = re.compile(
    r"\s*\(publ\)\.?|\baktiebolaget\b|\bab\b|\bgroup\b|\bseries [ab]\b",
    re.IGNORECASE,
)


def _clean_company(name: str) -> str:
    name = _STRIP_COMPANY.sub("", name)
    return re.sub(r"\s{2,}", " ", name).strip(" .,")


def _normalize_position(raw: str) -> str:
    return POSITION_MAP.get(raw.lower().strip(), raw.strip())


def _parse_swedish_number(val):
    """Convert Swedish-locale number string to float (e.g. '1 234,56' → 1234.56)."""
    if pd.isna(val):
        return float("nan")
    s = str(val).strip().replace("\xa0", "").replace(" ", "").replace(",", ".")
    try:
        return float(s)
    except ValueError:
        return float("nan")


def load_fi_csv(path: str) -> pd.DataFrame:
    """Load one FI CSV file and return a cleaned DataFrame."""
    # FI exports UTF-16 LE, sometimes with BOM (utf-16) and sometimes without (utf-16-le)
    with open(path, "rb") as f:
        raw = f.read(2)
    enc = "utf-16" if raw[:2] in (b"\xff\xfe", b"\xfe\xff") else "utf-16-le"

    df = pd.read_csv(
        path,
        encoding=enc,
        sep=";",
        dtype=str,
        skipinitialspace=True,
    )
    # Drop the trailing empty column FI adds after the last semicolon
    df = df.loc[:, df.columns.notna()]
    df.columns = [c.strip() for c in df.columns]
    df = df.rename(columns=COLUMN_MAP)

    # Drop rows with no useful content
    df = df.dropna(subset=["transaction_type"])

    # Filter instrument types and transaction types (same as backend)
    df = df[~df["instrument_type"].str.lower().str.strip().isin(EXCLUDED_INSTRUMENT_TYPES)]
    df = df[~df["transaction_type"].str.lower().str.strip().isin(EXCLUDED_TRANSACTION_TYPES)]
    df = df[df["status"].str.lower().str.strip() == "aktuell"]

    # Parse numeric columns
    df["shares"] = df["shares"].apply(_parse_swedish_number).astype("Int64")
    df["price"] = df["price"].apply(_parse_swedish_number)

    # Parse dates
    df["publishing_date"] = pd.to_datetime(df["publishing_date"], errors="coerce")
    df["transaction_date"] = pd.to_datetime(
        df["transaction_date"].str[:10], format="%Y-%m-%d", errors="coerce"
    )

    # Derived / normalized fields
    df["company_name"] = df["company_raw"].apply(_clean_company)
    df["position"] = df["position_raw"].apply(_normalize_position)
    df["total_value"] = df["price"] * df["shares"].astype(float)
    df["reporting_lag_days"] = (
        df["publishing_date"].dt.normalize() - df["transaction_date"]
    ).dt.days

    # Boolean flags
    df["is_related_party"] = df["related_party"].str.lower().str.strip() == "ja"
    df["is_stock_program"] = df["stock_program"].str.lower().str.strip() == "ja"
    df["is_first_time"] = df["first_time_reporting"].str.lower().str.strip() == "ja"

    return df


def load_fi_csvs(paths: list[str]) -> pd.DataFrame:
    """Load and concatenate multiple FI CSV files."""
    frames = [load_fi_csv(p) for p in paths]
    df = pd.concat(frames, ignore_index=True)
    df = df.drop_duplicates(
        subset=["insider_name", "company_raw", "transaction_date", "shares", "price"]
    )
    return df.sort_values("publishing_date").reset_index(drop=True)


def load_from_api(base_url: str, max_pages: int = 200) -> pd.DataFrame:
    """Fetch all trades from the AktieKoll REST API."""
    rows = []
    page = 1
    page_size = 100
    base_url = base_url.rstrip("/")
    while page <= max_pages:
        resp = requests.get(
            f"{base_url}/api/InsiderTrades/page",
            params={"page": page, "pageSize": page_size},
            timeout=30,
        )
        resp.raise_for_status()
        batch = resp.json()
        if not batch:
            break
        rows.extend(batch)
        if len(batch) < page_size:
            break
        page += 1

    if not rows:
        return pd.DataFrame()

    df = pd.DataFrame(rows)
    df.columns = [_to_snake(c) for c in df.columns]
    df["publishing_date"] = pd.to_datetime(df["publishing_date"], errors="coerce")
    df["transaction_date"] = pd.to_datetime(df["transaction_date"], errors="coerce")
    df["total_value"] = df["price"] * df["shares"].astype(float)
    df["reporting_lag_days"] = (
        df["publishing_date"].dt.normalize() - df["transaction_date"]
    ).dt.days
    return df.sort_values("publishing_date").reset_index(drop=True)


def _to_snake(name: str) -> str:
    s = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", name)
    s = re.sub(r"([a-z\d])([A-Z])", r"\1_\2", s)
    return s.lower()


def load_data(csv_glob: str | None = None, api_url: str | None = None) -> pd.DataFrame:
    """
    Load insider trade data.  Priority: csv_glob > api_url > env vars.
    """
    if csv_glob is None:
        csv_glob = os.getenv("CSV_PATH", "")
    if api_url is None:
        api_url = os.getenv("API_BASE_URL", "")

    if csv_glob:
        paths = sorted(glob.glob(csv_glob))
        if not paths:
            raise FileNotFoundError(f"No CSV files matched: {csv_glob!r}")
        print(f"Loading {len(paths)} CSV file(s)...")
        return load_fi_csvs(paths)

    if api_url:
        print(f"Fetching from API: {api_url}")
        return load_from_api(api_url)

    raise ValueError(
        "Provide CSV_PATH or API_BASE_URL (env or argument). "
        "Copy .env.example to .env and fill in your values."
    )
