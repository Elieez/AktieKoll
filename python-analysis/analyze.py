#!/usr/bin/env python3
"""
analyze.py — entry point for AktieKoll insider-trading analysis.

Usage examples
--------------
# From FI CSV files (one or many, glob supported):
  python analyze.py --csv "../data/*.csv"
  python analyze.py --csv "data/Insyn2025-06-24.csv"

# From the AktieKoll REST API:
  python analyze.py --api https://your-api.azurewebsites.net

# Using .env for config:
  cp .env.example .env   # fill in CSV_PATH or API_BASE_URL
  python analyze.py
"""

import argparse
import sys
from pathlib import Path

# Support running from the repo root too
sys.path.insert(0, str(Path(__file__).parent))

try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

from src.data_loader import load_data
from src.eda import run_full_eda


def parse_args():
    p = argparse.ArgumentParser(description="Insider trading EDA")
    p.add_argument("--csv", metavar="GLOB",
                   help="Glob pattern for FI CSV file(s), e.g. 'data/*.csv'")
    p.add_argument("--api", metavar="URL",
                   help="AktieKoll API base URL, e.g. https://api.example.com")
    p.add_argument("--output", metavar="DIR", default="output",
                   help="Directory for chart PNGs (default: output/)")
    return p.parse_args()


def main():
    args = parse_args()

    import os
    if args.output:
        os.environ["OUTPUT_DIR"] = args.output

    df = load_data(csv_glob=args.csv, api_url=args.api)
    print(f"Loaded {len(df):,} transactions.")
    run_full_eda(df)


if __name__ == "__main__":
    main()
