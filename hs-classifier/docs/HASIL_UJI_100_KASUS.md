# Hasil uji 100 barang ekspor-impor

Tanggal: 2 Nov 2026. Berkas kasus: `hs-classifier-api/docs/golden/export_import_100.json`.

## Cara uji

- 100 barang yang umum diekspor atau diimpor (pangan, bahan kimia, tekstil, logam, mesin, elektronik, kendaraan).
- Isian meniru form: nama generik, nama dagang, bahan, fungsi utama (nilai dropdown asli), cara kerja bila relevan, kondisi "lengkap dan jadi".
- Dijalankan lewat mesin klasifikasi asli di database SQL Server asli, tanpa menyimpan kasus. Lima putaran: tanpa EN, setelah EN dan Catatan dimuat, lalu setelah tiga putaran perbaikan. Putaran terakhir memakai AI (deepseek-v4.1-flash); sisanya tanpa AI.
- **Kode HS yang diharapkan adalah penilaian saya (4 digit), belum diperiksa ahli bea cukai.** Satu pos dipakai sebagai patokan; beberapa barang bisa sah di pos lain.
- Alat penjalan ada di folder sementara dan belum masuk repo.

## Arti hasil

| Hasil | Arti |
|---|---|
| OK_FINAL | Ditetapkan dan posnya benar |
| ASKS_RIGHT_HEADING | Bertanya subpos dan posnya benar (jalur wajar) |
| ASKS_WRONG_HEADING | Bertanya, tetapi pos kandidatnya salah |
| WRONG_FINAL | Ditetapkan ke pos yang salah, dengan kepercayaan Medium sampai High |
| EXPERT_NO_HINT | Ke review ahli tanpa kandidat |

## Perkembangan

| Hasil | Tanpa EN | EN dimuat (awal) | + aturan Inclusion | + pencarian kandidat | + AI dan motor |
|---|---:|---:|---:|---:|---:|
| OK_FINAL | 9 | 9 | 9 | 10 | 13 |
| ASKS_RIGHT_HEADING | 29 | 30 | 29 | 36 | 59 |
| ASKS_WRONG_HEADING | 18 | 30 | 18 | 29 | 20 |
| WRONG_FINAL | 8 | 19 | 8 | 10 | 8 |
| EXPERT_NO_HINT | 36 | 12 | 36 | 15 | 0 |

Jalur benar (OK_FINAL + ASKS_RIGHT_HEADING) naik dari 38 menjadi 72 dari 100. Total yang salah pos hampir sama dengan sebelum EN dimuat (26 dan 28), karena kasus yang dulu berhenti di review ahli tanpa kandidat (36) kini diproses (0). Dibanding setelah EN dimuat (49 salah pos, 19 di antaranya ditetapkan), hasilnya jauh lebih baik: 28 salah pos, 8 di antaranya ditetapkan.

## Yang ditemukan dan sudah diperbaiki

1. **Aturan "Inclusion" di KUM HS 1 terlalu longgar setelah EN dimuat.** Pos dipertahankan hanya karena punya paragraf EN "pos ini mencakup …", padahal 729 dari 1.233 pos punya paragraf itu. Draft EN + Catatan yang sudah aktif menetapkan 11 barang ke pos yang salah (sabun, sampo, lipstik, handuk, dan lainnya menjadi penyedot debu 8508.19.10). Sekarang paragraf itu hanya berlaku bila menyebut nama barang sebagai kata utuh.
2. **Label dropdown "Fungsi utama" dipakai sebagai kata pencarian.** "Penggunaan rumah tangga" menemukan 8508.19.10, "Konstruksi bangunan" menemukan 6811, "Lainnya" menemukan pos 0410. Sekarang label dropdown tidak dicari, dan fungsi serta bahan hanya dipakai bila nama barang tidak menemukan apa pun. Dulu keduanya jalan lebih dulu dan menutup pencarian cadangan berbasis nama.
3. **Nama dua kata tidak dicari lewat kata pertamanya.** "Beras putih" tidak menemukan "Beras.". Sekarang kata pertama (inti barang) dicari sebagai kata utuh pada teks pos 4 digit.
4. **AI hanya dipanggil bila tidak ada kandidat.** Sekarang AI juga dipanggil bila semua kandidat hanya berasal dari pencarian lemah. Bila AI kosong atau tidak tersedia, hasil dari database tetap dipakai.
5. **Cara kerja tidak membedakan pos bermotor dan tidak bermotor.** Sepeda fixie dengan cara kerja "Manual / Tanpa tenaga" sekarang masuk 8712 (tidak bermotor) dan bertanya subpos sepeda biasa, bukan pertanyaan kapasitas mesin (cc) di 8711.

Tes backend: 426 lulus, 2 dilewati (celah lama yang memang ditandai).

## Yang masih salah

Ditetapkan ke pos yang salah (8 kasus):
- Mentega: ditetapkan 1804.00.00 (Medium 61), harapan 0405
- Air mineral: ditetapkan 2202.99.50 (High 80), harapan 2201
- Batu bara: ditetapkan 2705.00.00 (Medium 67), harapan 2701
- Minyak mentah: ditetapkan 1511.10.00 (Medium 69), harapan 2709
- LPG: ditetapkan 2705.00.00 (Medium 65), harapan 2711
- Kain katun: ditetapkan 5211.41.10 (Medium 71), harapan 5208/5209
- Kulkas: ditetapkan 4502.00.00 (Medium 64), harapan 8418
- Transformator: ditetapkan 8546.20.10 (Medium 73), harapan 8504

Bertanya dengan kandidat pos yang salah (20 kasus): Sepeda motor, Truk barang, Cokelat batang, Garam dapur, Bensin, Sabun mandi, Lem kayu, Kantong plastik, Botol plastik, Sarung tangan karet, Koper, Kaos katun, Celana jeans, Kemeja pria, Benang katun, Handuk, Besi beton, Paku, Monitor, Televisi.

Penyebab yang terlihat:
- Bahan dan fungsi ikut dihitung saat menilai kecocokan teks pos, sehingga "Sepeda motor" dengan bahan Baja/Besi condong ke pos 7315 (rantai dari besi atau baja).
- Kata yang sama punya arti lain di tarif: "mentega" menemukan mentega kakao (1804), "batu bara" menemukan gas batu bara (2705), "minyak mentah" menemukan CPO.
- Istilah sehari-hari berbeda dari istilah tarif: kulkas dan lemari es, transformator dan trafo, kain katun dan kain tenunan dari kapas.
- Pencocokan satu kata ("mur", "paku", "handuk") menemukan pos tetangga.

Langkah berikutnya yang saya sarankan: kurangi bobot bahan dan fungsi dibanding nama barang saat menilai pos, dan minta AI memberi sinonim istilah tarif untuk kasus yang ditetapkan dengan kepercayaan di bawah High.