"""
enrich_hs_headings_seed.py
Merges all 6-digit subheadings from Hscode.csv into hs_headings_seed.csv.

Strategy:
- Read existing seed (keep all rows + their catatan_penting annotations intact)
- From Hscode.csv, extract unique (hs4, hs6) pairs with bilingual descriptions
- Derive seksi/bab from hs4 (bab = hs4[:2], seksi derived from bab range)
- Append rows for any hs6 NOT already in seed (catatan_penting = "")
- Sort by hs4 then hs6

ponytail: uses stdlib csv only, no pandas needed
"""

import csv
import re
from pathlib import Path

SEED_FILE = Path(__file__).parent / "hs_headings_seed.csv"
HS_CSV    = Path(__file__).parent.parent / "Hscode.csv"
OUT_FILE  = SEED_FILE  # overwrite in place

# HS Section map: bab (chapter) number → seksi (section) roman numeral
# Based on WCO HS 2022 section structure
SECTION_MAP = {
    range(1, 6):    "I",
    range(6, 15):   "II",
    range(15, 16):  "III",
    range(16, 25):  "IV",
    range(25, 28):  "V",
    range(28, 39):  "VI",
    range(39, 41):  "VII",
    range(41, 44):  "VIII",
    range(44, 47):  "IX",
    range(47, 50):  "X",
    range(50, 64):  "XI",
    range(64, 68):  "XII",
    range(68, 71):  "XIII",
    range(71, 72):  "XIV",
    range(72, 84):  "XV",
    range(84, 86):  "XVI",
    range(86, 93):  "XVII",
    range(93, 94):  "XVIII",
    range(94, 97):  "XIX",
    range(97, 98):  "XX",
    range(98, 100): "XXI",
}

def get_seksi(bab: int) -> str:
    for r, s in SECTION_MAP.items():
        if bab in r:
            return s
    return ""

def load_existing_seed(path: Path) -> tuple[list[dict], set[str]]:
    """Returns (rows, set_of_hs6_already_present)"""
    rows = []
    hs6_present = set()
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            rows.append(row)
            hs6_present.add(row["hs6"].strip())
    return rows, hs6_present

def load_hs6_from_csv(path: Path) -> list[dict]:
    """Extract unique hs6 entries from Hscode.csv, active only."""
    seen = set()
    entries = []
    with open(path, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f, delimiter=";")
        for row in reader:
            if row.get("Status", "A").strip() == "I":
                continue
            hs4 = row.get("Code4Digit", "").strip()
            hs6_raw = row.get("Code6Digit", "").strip()
            if not hs4 or not hs6_raw:
                continue  # skip rows without a 6-digit grouping
            # Format hs6: 010121 → 010121 (keep as-is, seed uses 6-digit no dots)
            key = hs6_raw
            if key in seen:
                continue
            seen.add(key)
            # hs4 formatted (no dots in seed file)
            bab = int(hs4[:2])
            seksi = get_seksi(bab)
            entries.append({
                "hs4":            hs4,
                "hs6":            hs6_raw,
                "uraian_id":      row.get("IdDescription6Digit", "").strip(),
                "uraian_en":      row.get("EnDescription6Digit", "").strip(),
                "seksi":          seksi,
                "bab":            str(bab),
                "catatan_penting": "",
            })
    return entries

def main():
    existing_rows, existing_hs6 = load_existing_seed(SEED_FILE)
    new_entries = load_hs6_from_csv(HS_CSV)

    added = 0
    for entry in new_entries:
        if entry["hs6"] not in existing_hs6:
            existing_rows.append(entry)
            existing_hs6.add(entry["hs6"])
            added += 1

    # Sort by hs4 then hs6
    existing_rows.sort(key=lambda r: (r["hs4"], r["hs6"]))

    fieldnames = ["hs4", "hs6", "uraian_id", "uraian_en", "seksi", "bab", "catatan_penting"]
    with open(OUT_FILE, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(existing_rows)

    print(f"Done.")
    print(f"  Existing annotated rows kept : {len(existing_rows) - added}")
    print(f"  New rows added from Hscode.csv: {added}")
    print(f"  Total rows in seed now        : {len(existing_rows)}")
    print(f"  Output → {OUT_FILE}")

if __name__ == "__main__":
    main()
