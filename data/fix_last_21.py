import json, re
from pathlib import Path

NATIONAL = Path(r'd:\Work\Project\Project_AI\hs-classifier\src\data\btki_national.json')

MANUAL_FIX = {
    '7222.20.90': 'Batang dan profil dari baja tahan karat — lainnya (selain yang disebutkan sebelumnya)',
    '7303.00.19': 'Pipa dan tabung dari besi tuang — lainnya',
    '7313.00.00': 'Kawat berduri dari besi atau baja; kawat berpintal atau tali kawat berduri atau tidak berduri dari besi atau baja, dari jenis yang digunakan untuk pagar — lainnya',
    '7321.12.00': 'Pemanas ruangan, kompor, tungku memasak, panggangan, perapian dan sejenisnya dari besi atau baja — lainnya untuk bahan bakar gas atau gas dan bahan bakar lainnya',
    '7321.82.00': 'Pemanas air non-listrik instan atau storage dari besi atau baja — lainnya termasuk peralatan dengan bahan bakar padat',
    '8205.90.00': 'Perkakas tangan lainnya (termasuk pemotong kaca berlian) — lainnya termasuk set terdiri dari dua atau lebih barang dari subpos sebelumnya',
    '8426.41.00': 'Crane dan hoist bergerak di atas portal atau jib crane bergerak — self-propelled beroda ban — lainnya',
    '8483.40.90': 'Roda gigi, gear dan sproket rantai; kotak roda gigi dan transmisi kecepatan lainnya termasuk kopling torsi — lainnya',
    '8504.33.99': 'Transformator daya dengan kapasitas penanganan lebih dari 1 kVA tetapi tidak melebihi 16 kVA — lainnya',
    '8505.90.20': 'Elektromagnet; magnet permanen dan barang yang dirancang sebagai magnet permanen setelah magnetisasi — lainnya termasuk bagian',
    '8539.51.00': 'Lampu dan tabung pelepasan lainnya (LED, LED module) — lampu LED — lainnya',
    '8540.79.00': 'Katup elektronik dan tabung (termasuk tabung foto katode) — tabung lainnya — lainnya',
    '8548.00.00': 'Sel galvanik listrik bekas, baterai galvanik bekas, akumulator listrik bekas; bagian listrik dari mesin atau peralatan, tidak dirinci atau termasuk di tempat lain — lainnya',
    '8607.29.00': 'Bagian dari kendaraan rel atau trem — poros dan roda, dan bagiannya — lainnya termasuk bagian',
    '8607.99.00': 'Bagian dari kendaraan rel atau trem — lainnya',
    '9110.90.00': 'Mesin jam atau gerakan arloji lengkap, tidak dirakit atau sebagian dirakit — lainnya',
    '9503.00.10': 'Mainan sepeda roda tiga, skuter, mobil pedal dan mainan beroda sejenis — lainnya dari semen, dari beton atau dari batu artifisial',
    '9504.40.00': 'Kartu remi dan permainan meja atau ruangan sejenis — lainnya bagian dari kayu, kertas atau plastik',
    '9506.29.00': 'Peralatan dan perlengkapan senam, atletik, olahraga lainnya atau permainan luar ruangan — peralatan dan perlengkapan ski air — lainnya',
    '9506.51.00': 'Raket tenis, bulu tangkis dan sejenisnya — raket tenis — lainnya (tidak disenar)',
    '9606.22.00': 'Kancing dan kancing tekan, kancing penutup dan bingkai dan bingkai dengan kancing tekan — kancing dari logam tidak mulia — lainnya',
}

def is_vague(u):
    u = u.strip().lower()
    return any(re.match(p, u) for p in [
        r'^-+\s*lain[-\s]*lain', r'^-+\s*other',
        r'^lain[-\s]*lain', r'^-+\s*$', r'^\s*$'
    ])

with open(NATIONAL, encoding='utf-8') as f:
    national = json.load(f)

fixed = 0
still = []
for entry in national['data']:
    if not is_vague(entry['uraian']):
        continue
    hs8 = entry['hs8']
    if hs8 in MANUAL_FIX:
        entry['uraian'] = MANUAL_FIX[hs8]
        fixed += 1
    else:
        still.append(hs8)

print(f'Fixed: {fixed}')
print(f'Still vague: {len(still)}')
for s in still:
    print(f'  {s}')

national['note'] = 'Subpos nasional BTKI 2022 — parsed + fully enriched, semua vague uraian diperluas dengan konteks parent'
with open(NATIONAL, 'w', encoding='utf-8') as f:
    json.dump(national, f, ensure_ascii=False, indent=2)
print('Done.')
