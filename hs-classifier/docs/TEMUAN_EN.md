# TEMUAN_EN — inspeksi sumber Explanatory Notes dan Catatan (Task 1)

Hasil membaca dua berkas Word di `docs/` tanpa mengubahnya. Angka diambil dari struktur XML berkas (skrip sekali pakai, tidak ada di repo).

## 1. `EN HS 2022 KPU TANJUNG PRIOK v2 (AutoRecovered).docx`

| Hal | Temuan |
|---|---|
| Isi | WCO *Explanatory Notes*, Seventh edition (2022), **berbahasa Inggris**. |
| Ukuran | 33,3 MB, 77.004 paragraf (44.685 tidak kosong), 4,6 juta karakter, 430 tabel. |
| Asal teks | Hasil OCR dari PDF. Ada salah baca (contoh: "Inpu�", "tlieir", "housmg", "Job" untuk "job") dan paragraf sisa berisi satu karakter "(". |
| Tracked changes, teks tersembunyi, footnote | Tidak ada (0). |
| Baris terpotong | Satu kalimat sering terbelah menjadi beberapa paragraf, misalnya "…classified in headings 02.01 to 02.05, 02.07," lalu "02.08 or 04.10 if…". |
| Penanda pos | Bentuk bertitik dan tebal: "84.71- Automatic data processing machines…". Tidak ada style judul yang dipakai (Heading1–9 hanya 20 paragraf). |
| Subpos | Baris daftar "8471.30 - Portable automatic data processing machines…" (3.595 baris). |
| Cakupan pos | Terlihat 936 dari 1.230 pos aktif di database lewat dua pola sederhana (278 dari penanda "84.71", 859 dari baris subpos). 297 pos tidak terlihat; sebagian besar kemungkinan karena salah baca OCR pada penanda, bukan karena EN-nya tidak ada. Tiga kode di EN tidak ada di database: 3530, 8333, 9928 (kemungkinan salah baca). |
| Catatan Bagian/Bab | Ada, dalam bahasa Inggris, di awal tiap Bagian dan Bab ("Notes." atau "Note."). Bagian dan Bab di berkas ini dapat dikenali dari "Section N" (21) dan "Chapter N" (101 kemunculan). |

Akibat untuk parser yang ada (`DocxParser.ParseENAsync`):

1. Pola penanda pos `^\d{4}` tidak cocok dengan "84.71". Hanya 13 baris berbentuk "8471", dan tidak ada yang tebal, sehingga hampir semua paragraf akan jatuh ke pos "0000".
2. Paragraf tidak digabung, jadi kalimat terpotong tersimpan sebagai paragraf terpisah.
3. Baris sampah ("(", nomor halaman) ikut tersimpan.
4. Pengetikan paragraf (Exclusion/Inclusion) memakai kata kunci di mana saja dalam teks, bukan kalimat pembuka.

Akibat untuk mesin: EN berbahasa Inggris, sedangkan nama barang yang diketik pengguna umumnya Indonesia. Pencocokan "nama barang disebut di kalimat pengecualian" hanya akan jalan untuk kata serapan (laptop, tractor). Nilai utama EN saat ini adalah bukti bagi ahli dan komponen "EN ditinjau" pada skor, bukan penyaring otomatis.

## 2. `26~PMK.010~2022 BTKI 2022.docx` (PMK 26/PMK.010/2022)

| Hal | Temuan |
|---|---|
| Ukuran | 5,3 MB, 168.352 paragraf (95.426 tidak kosong), 468 tabel. |
| Lampiran II | "CATATAN BAGIAN, CATATAN BAB, DAN CATATAN SUBPOS". Inilah sumber Catatan resmi berbahasa Indonesia. |
| Susunan | Dwibahasa, berurutan, per Bagian dan Bab: blok Indonesia ("Bab 84", judul, "Catatan.", "1.- Bab ini tidak meliputi:" …) lalu blok Inggris ("Chapter 84", "Notes.", "1.- This Chapter does not cover:" …). Penanda Bagian bergabung dengan teks Inggrisnya ("BAGIAN ISECTION I"). |
| Butir | Nomor catatan tertulis di teks ("1.-", "2.-"). Butir (a), (b) berupa paragraf daftar otomatis, jadi huruf butirnya tidak ada di teks. |
| Tracked changes, tersembunyi, footnote | Tidak ada (0). |
| Catatan Subpos | Ada ("Catatan Subpos." di bawah bab tertentu). |

## 3. Keputusan setelah inspeksi

- Berkas EN dapat dipakai. Mutunya OCR, jadi hasil parse harus dilaporkan apa adanya (pos tak terdeteksi, paragraf yatim) dan tidak boleh dianggap lengkap.
- Catatan Bagian/Bab/Subpos diambil dari Lampiran II PMK DOCX, dengan teks Indonesia (`TextId`) dan Inggris (`TextEn`) dipasangkan lewat nomor catatan.
- Tidak perlu menghentikan pekerjaan. Risiko yang tersisa: 297 pos tanpa EN, teks EN ber-noise, dan EN berbahasa Inggris.
