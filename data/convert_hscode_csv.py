"""
convert_hscode_csv.py
Converts Hscode.csv → enriched btki_national.json for the hs-classifier frontend.

Input : d:/Work/Project/Project_AI/Hscode.csv  (semicolon-delimited)
Output: d:/Work/Project/Project_AI/hs-classifier/src/data/btki_national.json

Column map from Hscode.csv:
  Id, Code, IdDescription, EnDescription,
  Code4Digit, Code6Digit,
  IdDescription4Digit, EnDescription4Digit,
  IdDescription6Digit, EnDescription6Digit,
  Status, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
"""

import csv
import json
import re
from pathlib import Path
from datetime import datetime

SRC = Path(__file__).parent.parent / "Hscode.csv"
DST = Path(__file__).parent.parent / "hs-classifier/src/data/btki_national.json"


def fmt_code(raw: str) -> str:
    """01012100 → 0101.21.00"""
    s = raw.strip()
    if len(s) == 8 and s.isdigit():
        return f"{s[:4]}.{s[4:6]}.{s[6:]}"
    return s


def fmt_code6(raw: str) -> str:
    """010121 → 0101.21"""
    s = raw.strip()
    if len(s) == 6 and s.isdigit():
        return f"{s[:4]}.{s[4:]}"
    return s


def fmt_code4(raw: str) -> str:
    """0101 → 0101"""
    return raw.strip()


def main():
    rows = []
    skipped_inactive = 0

    with open(SRC, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f, delimiter=";")
        for row in reader:
            status = row.get("Status", "A").strip()
            if status == "I":
                skipped_inactive += 1
                continue  # skip inactive codes

            code8 = row["Code"].strip()
            entry = {
                "hs8":        fmt_code(code8),
                "hs6":        fmt_code6(row.get("Code6Digit", "").strip()) if row.get("Code6Digit", "").strip() else None,
                "hs4":        fmt_code4(row.get("Code4Digit", "").strip()) if row.get("Code4Digit", "").strip() else None,
                "uraian_id":  row["IdDescription"].strip(),
                "uraian_en":  row["EnDescription"].strip(),
                "uraian4_id": row.get("IdDescription4Digit", "").strip() or None,
                "uraian4_en": row.get("EnDescription4Digit", "").strip() or None,
                "uraian6_id": row.get("IdDescription6Digit", "").strip() or None,
                "uraian6_en": row.get("EnDescription6Digit", "").strip() or None,
            }
            # remove None fields to keep JSON lean
            entry = {k: v for k, v in entry.items() if v is not None}
            rows.append(entry)

    output = {
        "note": "BTKI 2022 — sourced from Hscode.csv, bilingual, active codes only (Status=A)",
        "total_entries": len(rows),
        "generated_at": datetime.now().strftime("%Y-%m-%d"),
        "skipped_inactive": skipped_inactive,
        "data": rows,
    }

    DST.parent.mkdir(parents=True, exist_ok=True)
    with open(DST, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)

    print(f"Done. {len(rows)} active entries written → {DST}")
    print(f"Skipped {skipped_inactive} inactive (Status=I) entries.")


if __name__ == "__main__":
    main()
