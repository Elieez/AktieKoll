"""
Exploratory Data Analysis for Swedish insider trading data.

Run via analyze.py or import individual functions for ad-hoc use.
"""

from __future__ import annotations

import os
from pathlib import Path

import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np
import pandas as pd
import seaborn as sns
from tabulate import tabulate

sns.set_theme(style="whitegrid", palette="muted", font_scale=1.1)

_SEK_M = 1_000_000
_OUTPUT_DIR = Path(os.getenv("OUTPUT_DIR", "output"))


def _savefig(fig: plt.Figure, name: str) -> None:
    _OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    path = _OUTPUT_DIR / f"{name}.png"
    fig.savefig(path, bbox_inches="tight", dpi=150)
    print(f"  Saved: {path}")
    plt.close(fig)


def _fmt_sek(x, _=None) -> str:
    if abs(x) >= 1e9:
        return f"{x/1e9:.1f}B"
    if abs(x) >= 1e6:
        return f"{x/1e6:.1f}M"
    if abs(x) >= 1e3:
        return f"{x/1e3:.0f}k"
    return f"{x:.0f}"


# ── 1. Overview ────────────────────────────────────────────────────────────────

def overview(df: pd.DataFrame) -> None:
    print("\n" + "=" * 60)
    print("DATASET OVERVIEW")
    print("=" * 60)

    date_col = "transaction_date" if "transaction_date" in df.columns else None
    total_val = df["total_value"].sum() if "total_value" in df.columns else None

    rows = [
        ["Total transactions", f"{len(df):,}"],
        ["Unique companies", f"{df['company_name'].nunique() if 'company_name' in df.columns else df.get('company_name', pd.Series()).nunique():,}"],
        ["Unique insiders", f"{df['insider_name'].nunique() if 'insider_name' in df.columns else '—':,}"],
        ["Date range",
         f"{df[date_col].min().date()} → {df[date_col].max().date()}" if date_col else "—"],
        ["Total value (SEK)", f"{total_val:,.0f}" if total_val else "—"],
        ["Total value (SEK M)", f"{total_val/_SEK_M:,.1f} M" if total_val else "—"],
    ]
    print(tabulate(rows, tablefmt="simple"))

    print("\nTransaction type counts:")
    tt_col = "transaction_type"
    if tt_col in df.columns:
        counts = df[tt_col].value_counts()
        print(tabulate(counts.reset_index().values.tolist(), headers=["Type", "Count"], tablefmt="simple"))

    if "currency" in df.columns:
        print("\nCurrency distribution:")
        print(tabulate(df["currency"].value_counts().reset_index().values.tolist(),
                       headers=["Currency", "Count"], tablefmt="simple"))


# ── 2. Transaction type distribution ──────────────────────────────────────────

def plot_transaction_types(df: pd.DataFrame) -> None:
    print("\n[Chart] Transaction type distribution")
    counts = df["transaction_type"].value_counts()

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    fig.suptitle("Transaction Type Distribution", fontsize=14, fontweight="bold")

    # By count
    counts.plot.bar(ax=axes[0], color=sns.color_palette("muted", len(counts)))
    axes[0].set_title("By number of transactions")
    axes[0].set_ylabel("Transactions")
    axes[0].set_xlabel("")
    axes[0].tick_params(axis="x", rotation=30)

    # By total value
    val_by_type = df.groupby("transaction_type")["total_value"].sum().sort_values(ascending=False)
    val_by_type.plot.bar(ax=axes[1], color=sns.color_palette("muted", len(val_by_type)))
    axes[1].set_title("By total SEK value")
    axes[1].set_ylabel("Total value (SEK)")
    axes[1].yaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    axes[1].set_xlabel("")
    axes[1].tick_params(axis="x", rotation=30)

    fig.tight_layout()
    _savefig(fig, "01_transaction_types")


# ── 3. Buy vs Sell deep-dive ───────────────────────────────────────────────────

def plot_buy_sell_ratio(df: pd.DataFrame) -> None:
    print("\n[Chart] Buy vs Sell ratio by company (top 20)")
    buysell = df[df["transaction_type"].str.lower().isin(["förvärv", "avyttring"])].copy()
    if buysell.empty:
        print("  No buy/sell data.")
        return

    pivot = (
        buysell.groupby(["company_name", "transaction_type"])["total_value"]
        .sum()
        .unstack(fill_value=0)
    )
    # Normalize column names regardless of case
    pivot.columns = [c.lower() for c in pivot.columns]
    buy_col = "förvärv"
    sell_col = "avyttring"
    for col in [buy_col, sell_col]:
        if col not in pivot.columns:
            pivot[col] = 0.0

    pivot["net"] = pivot[buy_col] - pivot[sell_col]
    top = pivot.nlargest(10, buy_col).index.tolist() + pivot.nsmallest(10, sell_col).index.tolist()
    top = list(dict.fromkeys(top))  # deduplicate preserving order
    sub = pivot.loc[top].sort_values("net", ascending=True)

    fig, ax = plt.subplots(figsize=(11, max(6, len(sub) * 0.5)))
    colors = ["#e74c3c" if v < 0 else "#2ecc71" for v in sub["net"]]
    ax.barh(sub.index, sub["net"] / _SEK_M, color=colors)
    ax.axvline(0, color="black", linewidth=0.8)
    ax.set_xlabel("Net buy (Förvärv − Avyttring) in MSEK")
    ax.set_title("Net Insider Buy/Sell by Company (Top 20 by Activity)", fontweight="bold")
    fig.tight_layout()
    _savefig(fig, "02_buy_sell_ratio")


# ── 4. Top insiders ────────────────────────────────────────────────────────────

def plot_top_insiders(df: pd.DataFrame, n: int = 15) -> None:
    print(f"\n[Chart] Top {n} insiders by total transaction value")
    top = (
        df.groupby("insider_name")["total_value"]
        .sum()
        .nlargest(n)
        .sort_values()
    )

    fig, ax = plt.subplots(figsize=(10, 7))
    top.plot.barh(ax=ax, color=sns.color_palette("Blues_d", len(top)))
    ax.xaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    ax.set_xlabel("Total transaction value (SEK)")
    ax.set_title(f"Top {n} Insiders by Total Transaction Value", fontweight="bold")
    fig.tight_layout()
    _savefig(fig, "03_top_insiders")

    print(f"\nTop {n} insiders by transaction count:")
    cnt = df.groupby("insider_name").size().nlargest(n)
    print(tabulate(cnt.reset_index().values.tolist(), headers=["Insider", "Transactions"], tablefmt="simple"))


# ── 5. Top companies ───────────────────────────────────────────────────────────

def plot_top_companies(df: pd.DataFrame, n: int = 15) -> None:
    print(f"\n[Chart] Top {n} companies by insider activity")
    company_col = "company_name"

    fig, axes = plt.subplots(1, 2, figsize=(16, 7))
    fig.suptitle(f"Top {n} Companies by Insider Activity", fontweight="bold")

    # By count
    cnt = df.groupby(company_col).size().nlargest(n).sort_values()
    cnt.plot.barh(ax=axes[0], color=sns.color_palette("Greens_d", len(cnt)))
    axes[0].set_xlabel("Number of transactions")
    axes[0].set_title("By transaction count")

    # By value
    val = df.groupby(company_col)["total_value"].sum().nlargest(n).sort_values()
    val.plot.barh(ax=axes[1], color=sns.color_palette("Oranges_d", len(val)))
    axes[1].xaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    axes[1].set_xlabel("Total value (SEK)")
    axes[1].set_title("By total SEK value")

    fig.tight_layout()
    _savefig(fig, "04_top_companies")


# ── 6. Position analysis ───────────────────────────────────────────────────────

def plot_position_analysis(df: pd.DataFrame) -> None:
    print("\n[Chart] Position analysis")
    pos_col = "position" if "position" in df.columns else "position_raw"
    if pos_col not in df.columns:
        print("  No position column.")
        return

    fig, axes = plt.subplots(1, 2, figsize=(14, 6))
    fig.suptitle("Insider Position Analysis", fontweight="bold")

    # Transaction count by position
    cnt = df.groupby(pos_col).size().nlargest(12).sort_values()
    cnt.plot.barh(ax=axes[0], color=sns.color_palette("Set2", len(cnt)))
    axes[0].set_xlabel("Number of transactions")
    axes[0].set_title("Transaction count by position")

    # Average total value by position
    avg = df.groupby(pos_col)["total_value"].median().nlargest(12).sort_values()
    avg.plot.barh(ax=axes[1], color=sns.color_palette("Set3", len(avg)))
    axes[1].xaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    axes[1].set_xlabel("Median transaction value (SEK)")
    axes[1].set_title("Median transaction value by position")

    fig.tight_layout()
    _savefig(fig, "05_position_analysis")

    print("\nBuy/Sell split by position:")
    buysell = df[df["transaction_type"].str.lower().isin(["förvärv", "avyttring"])]
    if not buysell.empty:
        split = buysell.groupby([pos_col, "transaction_type"]).size().unstack(fill_value=0)
        print(tabulate(split.reset_index().values.tolist(), headers=["Position"] + list(split.columns), tablefmt="simple"))


# ── 7. Temporal patterns ───────────────────────────────────────────────────────

def plot_temporal_patterns(df: pd.DataFrame) -> None:
    print("\n[Chart] Temporal patterns")
    date_col = "transaction_date"
    if date_col not in df.columns:
        print("  No transaction_date column.")
        return

    fig, axes = plt.subplots(2, 2, figsize=(15, 10))
    fig.suptitle("Temporal Patterns in Insider Transactions", fontweight="bold")

    # Monthly transaction volume
    monthly = df.set_index(date_col).resample("ME")["total_value"].agg(["sum", "count"])
    monthly["sum"].plot(ax=axes[0, 0], color="#3498db")
    axes[0, 0].yaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    axes[0, 0].set_title("Monthly Total Value (SEK)")
    axes[0, 0].set_xlabel("")

    monthly["count"].plot(ax=axes[0, 1], color="#e67e22")
    axes[0, 1].set_title("Monthly Transaction Count")
    axes[0, 1].set_xlabel("")

    # Day of week
    dow_labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
    dow = df[date_col].dt.dayofweek.value_counts().sort_index()
    axes[1, 0].bar([dow_labels[i] for i in dow.index], dow.values, color=sns.color_palette("pastel"))
    axes[1, 0].set_title("Transactions by Day of Week")
    axes[1, 0].set_ylabel("Count")

    # Month of year
    moy = df[date_col].dt.month.value_counts().sort_index()
    month_labels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                    "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
    axes[1, 1].bar([month_labels[i - 1] for i in moy.index], moy.values,
                   color=sns.color_palette("pastel"))
    axes[1, 1].set_title("Transactions by Month of Year")
    axes[1, 1].set_ylabel("Count")

    fig.tight_layout()
    _savefig(fig, "06_temporal_patterns")


# ── 8. Reporting lag ──────────────────────────────────────────────────────────

def plot_reporting_lag(df: pd.DataFrame) -> None:
    print("\n[Chart] Reporting lag (PublishingDate − TransactionDate)")
    if "reporting_lag_days" not in df.columns:
        print("  No reporting_lag_days column.")
        return

    lag = df["reporting_lag_days"].dropna()
    lag = lag[(lag >= 0) & (lag <= 60)]  # clip extreme outliers for the chart

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    fig.suptitle("Reporting Lag (days from transaction to FI publication)", fontweight="bold")

    axes[0].hist(lag, bins=30, color="#9b59b6", edgecolor="white")
    axes[0].set_xlabel("Days")
    axes[0].set_ylabel("Count")
    axes[0].set_title("Distribution (capped at 60 days)")

    lag_by_type = df[df["transaction_type"].str.lower().isin(["förvärv", "avyttring"])].copy()
    if not lag_by_type.empty:
        for tt, grp in lag_by_type.groupby("transaction_type"):
            lags = grp["reporting_lag_days"].dropna()
            lags = lags[(lags >= 0) & (lags <= 60)]
            axes[1].hist(lags, bins=20, alpha=0.6, label=tt)
        axes[1].set_xlabel("Days")
        axes[1].set_ylabel("Count")
        axes[1].set_title("Lag by transaction type")
        axes[1].legend()

    fig.tight_layout()
    _savefig(fig, "07_reporting_lag")

    print(f"\nReporting lag statistics (days):")
    stats = lag.describe()
    print(tabulate([[k, f"{v:.1f}"] for k, v in stats.items()], headers=["Stat", "Days"], tablefmt="simple"))


# ── 9. Transaction value distribution ─────────────────────────────────────────

def plot_value_distribution(df: pd.DataFrame) -> None:
    print("\n[Chart] Transaction value distribution")
    vals = df["total_value"].dropna()
    vals = vals[vals > 0]

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    fig.suptitle("Transaction Value Distribution", fontweight="bold")

    axes[0].hist(vals.clip(upper=vals.quantile(0.95)), bins=50, color="#1abc9c", edgecolor="white")
    axes[0].xaxis.set_major_formatter(mticker.FuncFormatter(_fmt_sek))
    axes[0].set_xlabel("Total value (SEK)")
    axes[0].set_title("Distribution (capped at 95th percentile)")

    axes[1].hist(np.log10(vals + 1), bins=50, color="#16a085", edgecolor="white")
    axes[1].set_xlabel("log₁₀(Total value + 1)")
    axes[1].set_title("Log-scale distribution")

    fig.tight_layout()
    _savefig(fig, "08_value_distribution")

    print("\nTotal value percentiles (SEK):")
    pcts = [0.10, 0.25, 0.50, 0.75, 0.90, 0.95, 0.99]
    rows = [[f"{int(p*100)}%", _fmt_sek(vals.quantile(p))] for p in pcts]
    print(tabulate(rows, headers=["Percentile", "SEK"], tablefmt="simple"))


# ── 10. Related party & stock-program flags ───────────────────────────────────

def plot_flags(df: pd.DataFrame) -> None:
    if "is_related_party" not in df.columns:
        return
    print("\n[Chart] Related-party & stock-program flags")

    fig, axes = plt.subplots(1, 2, figsize=(10, 5))
    fig.suptitle("Disclosure Flags", fontweight="bold")

    for ax, col, title in [
        (axes[0], "is_related_party", "Related Party (Närstående)"),
        (axes[1], "is_stock_program", "Linked to Stock Program"),
    ]:
        counts = df[col].value_counts()
        ax.pie(counts, labels=counts.index.map({True: "Yes", False: "No"}),
               autopct="%1.1f%%", colors=["#e74c3c", "#95a5a6"])
        ax.set_title(title)

    fig.tight_layout()
    _savefig(fig, "09_flags")


# ── 11. Correlation heatmap ───────────────────────────────────────────────────

def plot_correlation(df: pd.DataFrame) -> None:
    print("\n[Chart] Numeric feature correlation")
    num_cols = ["shares", "price", "total_value"]
    if "reporting_lag_days" in df.columns:
        num_cols.append("reporting_lag_days")

    sub = df[num_cols].dropna()
    if sub.empty:
        return

    corr = sub.corr()
    fig, ax = plt.subplots(figsize=(7, 6))
    sns.heatmap(corr, annot=True, fmt=".2f", cmap="coolwarm", center=0,
                square=True, linewidths=0.5, ax=ax)
    ax.set_title("Numeric Feature Correlation", fontweight="bold")
    fig.tight_layout()
    _savefig(fig, "10_correlation")


# ── 12. Trading venue ─────────────────────────────────────────────────────────

def plot_trading_venues(df: pd.DataFrame) -> None:
    if "trading_venue" not in df.columns:
        return
    print("\n[Chart] Trading venues")
    venues = df["trading_venue"].value_counts().nlargest(12).sort_values()
    fig, ax = plt.subplots(figsize=(10, 6))
    venues.plot.barh(ax=ax, color=sns.color_palette("Set1", len(venues)))
    ax.set_xlabel("Number of transactions")
    ax.set_title("Top Trading Venues", fontweight="bold")
    fig.tight_layout()
    _savefig(fig, "11_trading_venues")


# ── Master runner ─────────────────────────────────────────────────────────────

def run_full_eda(df: pd.DataFrame) -> None:
    print(f"\nRunning full EDA on {len(df):,} transactions…")
    overview(df)
    plot_transaction_types(df)
    plot_buy_sell_ratio(df)
    plot_top_insiders(df)
    plot_top_companies(df)
    plot_position_analysis(df)
    plot_temporal_patterns(df)
    plot_reporting_lag(df)
    plot_value_distribution(df)
    plot_flags(df)
    plot_correlation(df)
    plot_trading_venues(df)
    print(f"\nDone. All charts saved to: {_OUTPUT_DIR.resolve()}/")
