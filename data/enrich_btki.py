"""
Enrich btki_national.json:
- Replace "- Lain-lain" / "- - Lain-lain" with full contextual description
  by prepending parent heading/subheading uraian
- Fix other common vague uraian patterns
"""
import json, re
from pathlib import Path

FULL_JSON   = Path(r'd:\Work\Project\Project_AI\data\btki_2022_full.json')
NATIONAL    = Path(r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json')

def is_vague(uraian: str) -> bool:
    """Detect uraian that is vague without parent context."""
    u = uraian.strip().lower()
    vague_patterns = [
        r'^-+\s*lain[-\s]*lain',       # - Lain-lain
        r'^-+\s*other',                # - Other
        r'^lain[-\s]*lain',            # Lain-lain
        r'^-+\s*$',                    # just dashes
        r'^\s*$',                      # empty
    ]
    for p in vague_patterns:
        if re.match(p, u):
            return True
    return False

def dash_level(uraian: str) -> int:
    """Count leading dash depth: '- -' = 2, '-' = 1, no dash = 0."""
    m = re.match(r'^(-+\s*)+', uraian.strip())
    if not m:
        return 0
    return m.group().count('-')

def main():
    # Load full dataset (all node types, sorted by canonical code)
    with open(FULL_JSON, encoding='utf-8') as f:
        full = json.load(f)
    all_records = full['data']

    # Build lookup by canonical_code
    by_canon = {r['canonical_code']: r for r in all_records}

    # Build sorted list of canonical codes for parent traversal
    sorted_codes = sorted(by_canon.keys())

    def get_parent_uraian(canon: str) -> str:
        """Find nearest parent (shorter prefix) with non-vague uraian."""
        # Try 6-digit parent, then 4-digit
        for prefix_len in [6, 4, 2]:
            parent_canon = canon[:prefix_len]
            if parent_canon in by_canon:
                parent = by_canon[parent_canon]
                puraian = parent.get('uraian_id', '').strip()
                if puraian and not is_vague(puraian):
                    return puraian
        return ''

    def enrich_uraian(record: dict) -> str:
        canon  = record['canonical_code']
        uraian = record.get('uraian_id', '').strip()

        if not is_vague(uraian):
            return uraian  # already fine

        parent_uraian = get_parent_uraian(canon)
        if not parent_uraian:
            return uraian  # no parent found, keep as-is

        # Clean dashes from uraian
        suffix = re.sub(r'^[-\s]+', '', uraian).strip()
        if suffix.lower() in ('lain-lain', 'lain lain', 'other', 'lainnya', ''):
            suffix = 'lainnya'

        return f"{parent_uraian} — {suffix}"

    # Load national (8-digit only)
    with open(NATIONAL, encoding='utf-8') as f:
        national = json.load(f)

    entries = national['data']
    enriched = 0
    before_after = []

    for entry in entries:
        old_uraian = entry['uraian']
        hs8 = entry['hs8']
        canon = hs8.replace('.', '')

        # find record in full dataset
        rec = by_canon.get(canon)
        if not rec:
            # try to find by formatted code
            rec = {'canonical_code': canon, 'uraian_id': old_uraian}

        new_uraian = enrich_uraian({'canonical_code': canon, 'uraian_id': old_uraian})

        if new_uraian != old_uraian:
            before_after.append((hs8, old_uraian, new_uraian))
            entry['uraian'] = new_uraian
            enriched += 1

    print(f'Enriched {enriched} / {len(entries)} entries')
    print(f'\nSample enrichments (first 20):')
    for hs8, before, after in before_after[:20]:
        print(f'  {hs8}')
        print(f'    BEFORE: {before}')
        print(f'    AFTER:  {after}')
        print()

    # Save
    national['note'] = 'Subpos nasional BTKI 2022 — parsed + enriched (lain-lain expanded with parent context)'
    with open(NATIONAL, 'w', encoding='utf-8') as f:
        json.dump(national, f, ensure_ascii=False, indent=2)
    print(f'Saved: {NATIONAL}')

    # Stats
    remaining_vague = sum(1 for e in entries if is_vague(e['uraian']))
    print(f'Remaining vague entries: {remaining_vague}')

if __name__ == '__main__':
    main()
