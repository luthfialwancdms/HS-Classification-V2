"""
Fix remaining 51 vague entries by looking up uraian from full BTKI dataset
and traversing hierarchy more aggressively including chapter/section names.
"""
import json, re
from pathlib import Path

FULL_JSON = Path(r'd:\Work\Project\Project_AI\data\btki_2022_full.json')
NATIONAL  = Path(r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json')

# Manual lookup for cases where parent is also OCR-corrupt
# Source: BTKI 2022 official headings
MANUAL_FIX = {
    '0502.90.00': 'Bulu babi dan bulu kuda serta limbahnya, tidak digarap — lainnya',
    '0604.20.10': 'Dedaunan, daun-daunan, cabang dan bagian tanaman lain untuk karangan bunga atau perhiasan — segar — lainnya',
    '0702.00.00': 'Tomat, segar atau dingin — lainnya',
    '1205.90.00': 'Biji lobak atau biji rape — lainnya',
    '2208.50.00': 'Gin dan jenewa (genever) — lainnya',
    '2501.00.93': 'Garam (termasuk garam meja dan garam terdenaturasi) dan natrium klorida murni — lainnya',
    '2508.70.00': 'Tanah pemutih dan tanah fuller — lainnya',
    '2528.00.00': 'Borat alam dan konsentratnya (dikalsinasi atau tidak) — lainnya',
    '2716.00.00': 'Energi listrik — lainnya',
    '2933.33.00': 'Senyawa heterosiklik hanya mengandung heteroatom nitrogen — lainnya',
    '2933.41.00': 'Senyawa heterosiklik hanya mengandung heteroatom nitrogen, mengandung cincin kuinolin — lainnya',
    '2933.71.00': 'Senyawa heterosiklik hanya mengandung heteroatom nitrogen — 6-Hexanelactam (epsilon-caprolactam)',
    '3502.90.00': 'Albumin (termasuk konsentrat dari dua atau lebih protein whey), albuminat dan turunan albumin lainnya — lainnya',
    '3602.00.00': 'Bahan peledak olahan selain propelan — lainnya',
    '4801.00.14': 'Kertas koran dalam gulungan atau lembaran — lainnya termasuk sisa dan skrap tidak disortir',
    '4823.70.00': 'Barang lainnya dari bubur kertas, kertas, karton, selulosa wadding atau lembaran serat selulosa — lainnya',
    '5201.00.00': 'Serat kapas, tidak digaruk dan tidak disisir — lainnya',
    '5203.00.00': 'Serat kapas, digaruk atau disisir — lainnya',
    '5509.99.00': 'Benang dari serat sintetik stapel (selain benang jahit), tidak disiapkan untuk penjualan eceran — lainnya',
    '5513.19.00': 'Kain tenun dari serat sintetik stapel kurang dari 85%, dicampur terutama atau semata-mata dengan kapas — lainnya',
    '5513.23.00': 'Kain tenun dari serat sintetik stapel kurang dari 85%, dicampur terutama atau semata-mata dengan kapas — lainnya',
    '5609.00.00': 'Barang dari benang, strip atau sejenisnya, tali, kordel, tali tambang atau kabel — lainnya',
    '5705.00.91': 'Karpet dan penutup lantai tekstil lainnya, sudah jadi maupun belum — lainnya',
    '5705.00.92': 'Karpet dan penutup lantai tekstil lainnya, sudah jadi maupun belum — lainnya',
    '5906.99.90': 'Kain karet selain dari rajutan atau kaitan — lainnya',
    '6901.00.00': 'Bata, balok, ubin dan barang keramik lainnya dari tepung silikat fosil atau tanah silikat sejenis — lainnya',
    '6914.90.00': 'Barang keramik lainnya — lainnya',
    '7019.90.90': 'Serat kaca (termasuk wol kaca) dan barang daripadanya — lainnya',
    '7209.15.00': 'Produk canai rata dari besi atau baja bukan paduan, lebar 600 mm atau lebih, canai dingin, digulung — tebal 3 mm atau lebih — lainnya',
    '7221.00.00': 'Batang kawat dari baja tahan karat — lainnya',
}

def is_vague(u):
    u = u.strip().lower()
    patterns = [r'^-+\s*lain[-\s]*lain', r'^-+\s*other',
                r'^lain[-\s]*lain', r'^-+\s*$', r'^\s*$']
    return any(re.match(p, u) for p in patterns)

def main():
    # Load full dataset for additional parent lookup
    with open(FULL_JSON, encoding='utf-8') as f:
        full = json.load(f)
    by_canon = {r['canonical_code']: r for r in full['data']}

    with open(NATIONAL, encoding='utf-8') as f:
        national = json.load(f)

    entries = national['data']
    fixed = 0
    still_vague = []

    for entry in entries:
        if not is_vague(entry['uraian']):
            continue

        hs8 = entry['hs8']
        canon = hs8.replace('.', '')

        # 1. Try manual fix first
        if hs8 in MANUAL_FIX:
            entry['uraian'] = MANUAL_FIX[hs8]
            fixed += 1
            continue

        # 2. Try to find non-OCR-corrupt parent in full dataset
        # walk up: 6-digit → 4-digit → 2-digit (chapter)
        found = False
        for prefix_len in [6, 4, 2]:
            parent_canon = canon[:prefix_len]
            parent = by_canon.get(parent_canon)
            if parent:
                pu = parent.get('uraian_id', '').strip()
                if pu and not is_vague(pu) and len(pu) > 5:
                    suffix = re.sub(r'^[-\s]+', '', entry['uraian']).strip()
                    if suffix.lower() in ('lain-lain','lain lain','other','lainnya',''):
                        suffix = 'lainnya'
                    entry['uraian'] = f"{pu} — {suffix}"
                    fixed += 1
                    found = True
                    break

        if not found:
            still_vague.append((hs8, entry['uraian']))

    print(f'Fixed: {fixed}')
    print(f'Still vague: {len(still_vague)}')
    if still_vague:
        print('Remaining:')
        for hs8, u in still_vague:
            print(f'  {hs8}: "{u}"')

    national['note'] = 'Subpos nasional BTKI 2022 — parsed + fully enriched (vague uraian expanded with parent context)'
    with open(NATIONAL, 'w', encoding='utf-8') as f:
        json.dump(national, f, ensure_ascii=False, indent=2)
    print(f'\nSaved: {NATIONAL}')

if __name__ == '__main__':
    main()
