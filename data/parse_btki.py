"""
BTKI 2022 Parser
Parse 26~PMK.010~2022 BTKI 2022.docx → JSON + CSV
"""
import json
import csv
import re
from pathlib import Path
from docx import Document

DOCX_PATH = Path(r'd:\Work\Project\Project_AI\docs\26~PMK.010~2022 BTKI 2022.docx')
OUT_JSON = Path(r'd:\Work\Project\Project_AI\data\btki_2022_full.json')
OUT_CSV = Path(r'd:\Work\Project\Project_AI\data\btki_2022_full.csv')
OUT_NATIONAL = Path(r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json')
OUT_REPORT = Path(r'd:\Work\Project\Project_AI\data\btki_parse_report.md')

# ── helpers ──────────────────────────────────────────────────────────────────

HS_PATTERN_8 = re.compile(r'^\d{4}\.\d{2}\.\d{2}$')
HS_PATTERN_6 = re.compile(r'^\d{4}\.\d{2}$')
HS_PATTERN_4 = re.compile(r'^\d{2}\.\d{2}$')

def clean(text: str) -> str:
    """Strip whitespace and normalize common OCR artifacts."""
    t = text.replace('\n', ' ').replace('\t', ' ').strip()
    t = re.sub(r' {2,}', ' ', t)
    # common OCR fixes
    t = t.replace('■', 's').replace('1n1', 'ini').replace('1m1', 'ini')
    t = t.replace('Menter!', 'Menteri').replace('1mpor', 'impor')
    return t

def normalize_hs(code: str) -> str:
    """Normalize HS code: remove spaces, dots noise."""
    c = code.strip().replace(' ', '')
    # remove trailing non-digit
    c = re.sub(r'[^0-9\.]', '', c)
    return c

def canonical(formatted: str) -> str:
    return formatted.replace('.', '')

def get_bab(hs4: str) -> str:
    """Get 2-digit chapter from 4-digit heading."""
    digits = hs4.replace('.', '')
    return digits[:2] if len(digits) >= 2 else ''

def get_seksi(bab: int) -> str:
    """Map chapter number to Section (Seksi) Roman numeral."""
    mapping = [
        (range(1, 6), 'I'),
        (range(6, 15), 'II'),
        (range(15, 16), 'III'),
        (range(16, 25), 'IV'),
        (range(25, 28), 'V'),
        (range(28, 39), 'VI'),
        (range(39, 41), 'VII'),
        (range(41, 44), 'VIII'),
        (range(44, 47), 'IX'),
        (range(47, 50), 'X'),
        (range(50, 64), 'XI'),
        (range(64, 68), 'XII'),
        (range(68, 71), 'XIII'),
        (range(71, 72), 'XIV'),
        (range(72, 84), 'XV'),
        (range(84, 86), 'XVI'),
        (range(86, 90), 'XVII'),
        (range(90, 93), 'XVIII'),
        (range(93, 94), 'XIX'),
        (range(94, 97), 'XX'),
        (range(97, 100), 'XXI'),
    ]
    for r, s in mapping:
        if bab in r:
            return s
    return '?'

def node_type(hs_formatted: str) -> str:
    digits = hs_formatted.replace('.', '')
    if len(digits) == 8: return 'tariff_line'
    if len(digits) == 6: return 'subheading'
    if len(digits) == 4: return 'heading'
    if len(digits) == 2: return 'chapter'
    return 'unknown'

# ── parser ────────────────────────────────────────────────────────────────────

def is_tariff_table(table) -> bool:
    """Detect if a table contains tariff data (5 cols, header has HS Code)."""
    if len(table.columns) < 4:
        return False
    if len(table.rows) < 3:
        return False
    first_row_text = ' '.join(c.text.strip() for c in table.rows[0].cells).lower()
    return ('pos' in first_row_text or 'hs code' in first_row_text or 'uraian' in first_row_text)

def parse_table(table, records: list, errors: list):
    """Parse one tariff table, append results to records list."""
    prev_hs = ''
    prev_uraian_id = ''
    prev_uraian_en = ''

    for r_idx, row in enumerate(table.rows):
        if r_idx < 2:  # skip header rows (row 0 = labels, row 1 = [1][2][3]...)
            continue

        cells = [c.text for c in row.cells]
        if len(cells) < 4:
            continue

        # col 1 = HS Code, col 2 = Uraian ID, col 3 = Uraian EN, col 4 = Bea Masuk
        raw_hs = normalize_hs(cells[1]) if len(cells) > 1 else ''
        raw_id = clean(cells[2]) if len(cells) > 2 else ''
        raw_en = clean(cells[3]) if len(cells) > 3 else ''
        raw_bm = clean(cells[4]) if len(cells) > 4 else ''

        # continuation row (HS blank, description continues from prev)
        if not raw_hs and raw_id and prev_hs:
            # append to previous record
            if records and records[-1]['formatted_code'] == prev_hs:
                records[-1]['uraian_id'] += ' ' + raw_id
                if raw_en:
                    records[-1]['uraian_en'] += ' ' + raw_en
            continue

        if not raw_hs:
            continue

        # validate HS format
        valid = (HS_PATTERN_8.match(raw_hs) or
                 HS_PATTERN_6.match(raw_hs) or
                 HS_PATTERN_4.match(raw_hs))
        if not valid:
            errors.append(f'Invalid HS: "{raw_hs}" | row {r_idx}')
            continue

        # derive fields
        digits = raw_hs.replace('.', '')
        hs4 = raw_hs[:4] if '.' in raw_hs else raw_hs[:4]
        # normalize hs4 to dotted form (e.g. "8471" stays, "01.01" → "0101" → "01.01")
        hs4_clean = digits[:4]
        bab_str = digits[:2]
        try:
            bab_int = int(bab_str)
            seksi = get_seksi(bab_int)
        except ValueError:
            bab_int = 0
            seksi = '?'

        record = {
            'formatted_code': raw_hs,
            'canonical_code': digits,
            'digits': len(digits),
            'node_type': node_type(raw_hs),
            'bab': bab_str,
            'seksi': seksi,
            'uraian_id': raw_id,
            'uraian_en': raw_en,
            'bea_masuk_mfn': raw_bm,
        }
        records.append(record)
        prev_hs = raw_hs
        prev_uraian_id = raw_id
        prev_uraian_en = raw_en

# ── main ──────────────────────────────────────────────────────────────────────

def main():
    print('Loading DOCX...')
    doc = Document(str(DOCX_PATH))
    print(f'Tables: {len(doc.tables)}')

    records = []
    errors = []
    table_count = 0

    for t_idx, table in enumerate(doc.tables):
        if is_tariff_table(table):
            parse_table(table, records, errors)
            table_count += 1

    print(f'Parsed {table_count} tariff tables → {len(records)} records')

    # ── deduplicate (keep last occurrence which tends to be more complete) ──
    seen = {}
    for r in records:
        seen[r['canonical_code']] = r
    deduped = list(seen.values())
    # sort by canonical code
    deduped.sort(key=lambda x: x['canonical_code'])

    print(f'After dedup: {len(deduped)} unique codes')

    # ── save full JSON ──
    with open(OUT_JSON, 'w', encoding='utf-8') as f:
        json.dump({
            'note': 'BTKI 2022 parsed from 26~PMK.010~2022 BTKI 2022.docx',
            'source': 'PMK 26/PMK.010/2022',
            'total': len(deduped),
            'generated_at': '2026-08-11',
            'data': deduped
        }, f, ensure_ascii=False, indent=2)
    print(f'Saved: {OUT_JSON}')

    # ── save CSV ──
    with open(OUT_CSV, 'w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'formatted_code','canonical_code','digits','node_type',
            'bab','seksi','uraian_id','uraian_en','bea_masuk_mfn'
        ])
        writer.writeheader()
        writer.writerows(deduped)
    print(f'Saved: {OUT_CSV}')

    # ── update btki_national.json (only 8-digit tariff lines) ──
    tariff_lines = [r for r in deduped if r['digits'] == 8 and r['uraian_id']]
    national_data = [
        {'hs8': r['formatted_code'], 'uraian': r['uraian_id']}
        for r in tariff_lines
    ]
    with open(OUT_NATIONAL, 'w', encoding='utf-8') as f:
        json.dump({
            'note': 'Subpos nasional BTKI 2022 (PMK 26/2022) — parsed dari DOCX resmi, bukan manual seed',
            'source': 'PMK 26/PMK.010/2022',
            'total_entries': len(national_data),
            'generated_at': '2026-08-11',
            'data': national_data
        }, f, ensure_ascii=False, indent=2)
    print(f'Updated btki_national.json: {len(national_data)} tariff lines')

    # ── stats per chapter ──
    from collections import Counter
    bab_counts = Counter(r['bab'] for r in deduped if r['digits'] == 8)

    # ── write report ──
    lines_8 = [r for r in deduped if r['digits'] == 8]
    lines_6 = [r for r in deduped if r['digits'] == 6]
    lines_4 = [r for r in deduped if r['digits'] == 4]

    report = f"""# BTKI 2022 Parse Report
Generated: 2026-08-11

## Summary
| Item | Count |
|------|-------|
| Total tariff tables parsed | {table_count} |
| Total unique codes | {len(deduped)} |
| 8-digit tariff lines | {len(lines_8)} |
| 6-digit subheadings | {len(lines_6)} |
| 4-digit headings | {len(lines_4)} |
| Parse errors/anomalies | {len(errors)} |

## Coverage per Bab (8-digit lines only)
| Bab | Count |
|-----|-------|
"""
    for bab in sorted(bab_counts.keys()):
        report += f'| {bab} | {bab_counts[bab]} |\n'

    report += f"""
## Sample Data (first 10 tariff lines)
| HS Code | Uraian ID | Bea Masuk |
|---------|-----------|-----------|
"""
    for r in lines_8[:10]:
        uid = r['uraian_id'][:60].replace('|','/')
        report += f"| {r['formatted_code']} | {uid} | {r['bea_masuk_mfn']} |\n"

    if errors:
        report += f"\n## Parse Errors ({len(errors)} items)\n"
        for e in errors[:20]:
            report += f'- {e}\n'
        if len(errors) > 20:
            report += f'... and {len(errors)-20} more\n'

    report += f"""
## Output Files
- `data/btki_2022_full.json` — Full dataset with all node types
- `data/btki_2022_full.csv` — CSV version for spreadsheet review
- `hs-classifier/src/data/btki_national.json` — Updated with {len(national_data)} real tariff lines
"""

    with open(OUT_REPORT, 'w', encoding='utf-8') as f:
        f.write(report)
    print(f'Report: {OUT_REPORT}')
    print('Done.')

if __name__ == '__main__':
    main()
