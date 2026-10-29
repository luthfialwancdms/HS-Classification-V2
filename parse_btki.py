"""
parse_btki.py — Parse BTKI 2022 DOCX → JSON + CSV
ponytail: single-pass table scan, no fancy abstraction
"""
import json, csv, re, sys
from datetime import date
from pathlib import Path
from docx import Document

DOCX = Path(r"d:\Work\Project\Project_AI\docs\26~PMK.010~2022 BTKI 2022.docx")
OUT_JSON = Path(r"d:\Work\Project\Project_AI\data\btki_2022_full.json")
OUT_CSV  = Path(r"d:\Work\Project\Project_AI\data\btki_2022_full.csv")
OUT_NATIONAL = Path(r"d:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json")
OUT_REPORT = Path(r"d:\Work\Project\Project_AI\data\btki_parse_report.md")

# ── helpers ────────────────────────────────────────────────────────────────

def clean(s):
    """Strip whitespace, normalize dashes and spaces."""
    if not s:
        return ""
    s = s.strip()
    s = re.sub(r'\s+', ' ', s)
    # common OCR artifacts
    s = s.replace('\u00a0', ' ').replace('\u2013', '-').replace('\u2014', '-')
    return s

def parse_hs(raw):
    """Return (formatted, canonical, digits) or None if not a HS code."""
    s = clean(raw).replace(' ', '')
    # remove trailing dots
    s = s.rstrip('.')
    # keep only digits and dots
    digits_only = re.sub(r'[^\d]', '', s)
    if len(digits_only) not in (2, 4, 6, 8, 10):
        return None
    if not digits_only:
        return None
    return digits_only

def format_hs(canonical):
    n = len(canonical)
    if n == 2:   return canonical
    if n == 4:   return f"{canonical[:2]}.{canonical[2:]}"
    if n == 6:   return f"{canonical[:4]}.{canonical[4:]}"
    if n == 8:   return f"{canonical[:4]}.{canonical[4:6]}.{canonical[6:]}"
    if n == 10:  return f"{canonical[:4]}.{canonical[4:6]}.{canonical[6:8]}.{canonical[8:]}"
    return canonical

def is_header_row(cells):
    """Detect table header rows (column labels or [1][2]... rows)."""
    text = ' '.join(clean(c) for c in cells).lower()
    if re.search(r'\[1\].*\[2\]', text):
        return True
    if re.search(r'hs\s*code|uraian|description|bea masuk|no\.?\s', text) and len(text) < 200:
        return True
    return False

def parse_bm(raw):
    """Normalize bea masuk: '5,00%' → '5%', '0,00%' → '0%'."""
    s = clean(raw)
    if not s:
        return ""
    # extract number
    m = re.search(r'([\d]+)[,\.]([\d]+)\s*%', s)
    if m:
        intpart = int(m.group(1))
        fracpart = int(m.group(2))
        if fracpart == 0:
            return f"{intpart}%"
        return f"{intpart}.{m.group(2)}%"
    # fallback: keep as-is if it has %
    if '%' in s:
        return s
    return s

# ── main parse ─────────────────────────────────────────────────────────────

print(f"Loading {DOCX.name} …", flush=True)
doc = Document(str(DOCX))

rows_out = []
errors = []
table_count = 0
skipped_headers = 0
prev_row = None  # for continuation rows

for tbl_idx, table in enumerate(doc.tables):
    table_count += 1
    for row in table.rows:
        cells = [c.text for c in row.cells]
        # need at least 5 cells; merge duplicates from merged cells
        if len(cells) < 4:
            continue

        # docx sometimes duplicates merged cells; take first 5
        c0 = clean(cells[0])   # No
        c1 = clean(cells[1])   # HS Code
        c2 = clean(cells[2])   # Uraian (ID)
        c3 = clean(cells[3])   # Description (EN)
        c4 = clean(cells[4]) if len(cells) > 4 else ""  # Bea Masuk

        # skip header rows
        if is_header_row(cells[:5]):
            skipped_headers += 1
            continue

        # continuation row: no HS code, append uraian to previous
        if not c1 and not c0 and prev_row is not None and (c2 or c3):
            if c2:
                prev_row['uraian_id'] = (prev_row['uraian_id'] + ' ' + c2).strip()
            if c3:
                prev_row['uraian_en'] = (prev_row['uraian_en'] + ' ' + c3).strip()
            continue

        if not c1:
            continue

        canonical = parse_hs(c1)
        if not canonical:
            # might be a section/chapter title row without proper HS
            errors.append({"table": tbl_idx+1, "raw_hs": c1, "uraian": c2[:60]})
            continue

        n = len(canonical)
        if n == 2:
            node_type = "chapter"
        elif n == 4:
            node_type = "heading"
        elif n == 6:
            node_type = "subheading"
        elif n == 8:
            node_type = "tariff_line"
        else:
            node_type = "national_subpos"

        formatted = format_hs(canonical)
        bab = canonical[:2]
        hs4 = canonical[:4] if n >= 4 else ""
        hs6 = canonical[:6] if n >= 6 else ""
        hs8 = formatted if n >= 8 else ""

        entry = {
            "hs8": hs8 if hs8 else formatted,
            "canonical": canonical,
            "hs6": format_hs(hs6) if hs6 else "",
            "hs4": format_hs(hs4) if hs4 else "",
            "bab": bab,
            "uraian_id": c2,
            "uraian_en": c3,
            "bea_masuk": parse_bm(c4),
            "node_type": node_type,
        }
        rows_out.append(entry)
        prev_row = entry

print(f"Tables scanned: {table_count}")
print(f"Rows parsed: {len(rows_out)}")
print(f"Header rows skipped: {skipped_headers}")
print(f"Parse errors: {len(errors)}")

# ── write JSON ─────────────────────────────────────────────────────────────
OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
with open(OUT_JSON, 'w', encoding='utf-8') as f:
    json.dump(rows_out, f, ensure_ascii=False, indent=2)
print(f"Written: {OUT_JSON}")

# ── write CSV ──────────────────────────────────────────────────────────────
FIELDS = ["hs8","canonical","hs6","hs4","bab","uraian_id","uraian_en","bea_masuk","node_type"]
with open(OUT_CSV, 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=FIELDS)
    w.writeheader()
    w.writerows(rows_out)
print(f"Written: {OUT_CSV}")

# ── coverage per bab ───────────────────────────────────────────────────────
from collections import Counter
bab_counts = Counter(r['bab'] for r in rows_out)
tariff_lines = [r for r in rows_out if r['node_type'] == 'tariff_line']
tariff_bab = Counter(r['bab'] for r in tariff_lines)

# ── update btki_national.json ──────────────────────────────────────────────
# Only tariff_line + national_subpos entries (8+ digit)
national_data = [
    {"hs8": r["hs8"], "uraian": r["uraian_id"]}
    for r in rows_out
    if r["node_type"] in ("tariff_line", "national_subpos") and r["hs8"]
]

national_payload = {
    "note": "Subpos nasional BTKI 2022 (PMK 26/2022) — parsed dari DOCX resmi",
    "total_entries": len(national_data),
    "generated_at": str(date.today()),
    "data": national_data
}
with open(OUT_NATIONAL, 'w', encoding='utf-8') as f:
    json.dump(national_payload, f, ensure_ascii=False, indent=2)
print(f"Written: {OUT_NATIONAL} ({len(national_data)} entries)")

# ── report ─────────────────────────────────────────────────────────────────
report_lines = [
    "# BTKI 2022 Parse Report",
    f"\n**Generated:** {date.today()}  ",
    f"**Source:** `{DOCX.name}`\n",
    "## Summary",
    f"| Metric | Value |",
    f"|--------|-------|",
    f"| Tables scanned | {table_count} |",
    f"| Total rows parsed | {len(rows_out)} |",
    f"| Tariff lines (8-digit) | {len(tariff_lines)} |",
    f"| Header rows skipped | {skipped_headers} |",
    f"| Parse errors / unrecognized | {len(errors)} |",
    f"| btki_national.json entries | {len(national_data)} |",
    "",
    "## Node Type Breakdown",
    "| Node Type | Count |",
    "|-----------|-------|",
]
type_counts = Counter(r['node_type'] for r in rows_out)
for t, c in sorted(type_counts.items()):
    report_lines.append(f"| {t} | {c} |")

report_lines += [
    "",
    "## Coverage per Bab (Top 30 by tariff line count)",
    "| Bab | Total Rows | Tariff Lines |",
    "|-----|-----------|--------------|",
]
for bab in sorted(bab_counts.keys())[:30]:
    report_lines.append(f"| {bab} | {bab_counts[bab]} | {tariff_bab.get(bab, 0)} |")

if errors:
    report_lines += [
        "",
        f"## Parse Errors / Anomalies ({len(errors)} total)",
        "| Table | Raw HS | Uraian (truncated) |",
        "|-------|--------|---------------------|",
    ]
    for e in errors[:50]:  # show max 50
        report_lines.append(f"| {e['table']} | `{e['raw_hs']}` | {e['uraian']} |")
    if len(errors) > 50:
        report_lines.append(f"\n_... and {len(errors)-50} more errors (see full JSON for details)_")

report_lines += [
    "",
    "## Output Files",
    f"- `{OUT_JSON}` — full JSON array",
    f"- `{OUT_CSV}` — CSV (UTF-8 BOM for Excel)",
    f"- `{OUT_NATIONAL}` — btki_national.json updated",
]

OUT_REPORT.parent.mkdir(parents=True, exist_ok=True)
OUT_REPORT.write_text('\n'.join(report_lines), encoding='utf-8')
print(f"Report: {OUT_REPORT}")
print("Done.")
