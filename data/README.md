# Seed Data — AI HS Classification Intelligence (MVP)

## File dalam folder ini

| File | Isi | Status |
|------|-----|--------|
| `hs_headings_seed.csv` | ~40 heading/subheading HS yang paling umum | Seed manual, perlu diperluas |
| `catatan_bab_seed.json` | Catatan Bagian XVI, XVII + Catatan Bab 84, 87, 89 | Seed manual, perlu dilengkapi semua bab |
| `kumhs_rules.json` | KUM HS 1-6 lengkap dengan trigger dan implikasi AI | Lengkap untuk MVP |
| `dropdown_master.json` | Master data untuk semua dropdown di UI | Lengkap untuk MVP |

## Coverage Seed Data Saat Ini

Heading yang sudah ada di seed:
- **Bab 84**: Mesin ADP (8471), Motor/Generator (8501, 8502), Pompa (8413, 8414), Crane (8426), Alat berat (8429, 8431)
- **Bab 85**: Elektronik (8517, 8525, 8528, 8536, 8544)
- **Bab 87**: Kendaraan (8701 traktor, 8704 dumper/truk)
- **Bab 89**: Kapal (8901 kargo, 8905 floating crane/crane barge)
- **Bab 72-74**: Baja (7208, 7209, 7304), Tembaga (7407)
- **Bab 27**: Minyak bumi (2709, 2710)
- **Bab 30**: Farmasi (3002, 3004)

## Yang Perlu Ditambahkan (prioritas selanjutnya)

1. Heading Bab 84-85 lainnya (masih banyak)
2. Catatan Bab untuk semua bab yang akan dipakai
3. EN ringkasan per heading utama
4. Data BTKI 8-digit nasional (tunggu akses resmi DJBC)

## Cara Extend

Tambah baris di `hs_headings_seed.csv` dengan format:
```
hs4, hs6, uraian_id, uraian_en, seksi, bab, catatan_penting
```

Tambah catatan bab di `catatan_bab_seed.json` → array `catatan_bab`.
