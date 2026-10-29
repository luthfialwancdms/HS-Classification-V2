# AI HS Classification Intelligence
## Ringkasan Proyek & Rencana Pengembangan

**Untuk:** Pengguna & Stakeholder  
**Status saat ini:** V1 (MVP) sudah berjalan · V2 dalam tahap perencanaan  
**Referensi regulasi:** BTKI 2022 · PMK 26/2022

---

## Apa Itu Sistem Ini?

Sistem ini adalah **asisten klasifikasi kode HS berbasis AI** yang membantu menentukan kode HS (Harmonized System) yang tepat untuk barang impor sesuai BTKI 2022.

Berbeda dari pencarian kode HS biasa yang hanya mencocokkan kata, sistem ini **meniru cara kerja ahli kepabeanan** — menganalisis fakta teknis barang, lalu mengujinya menggunakan aturan hukum KUM HS (Ketentuan Umum Menafsir HS) secara berurutan, persis seperti yang dilakukan pejabat DJBC.

> ⚠️ Output sistem ini adalah **rekomendasi**, bukan penetapan resmi DJBC. Selalu verifikasi dengan ahli kepabeanan untuk keperluan dokumen resmi.

---

## Kondisi Saat Ini — V1 (Sudah Berjalan)

V1 sudah dapat digunakan dan mencakup:

| Fitur | Keterangan |
|-------|-----------|
| ✅ Form input terstruktur | Isi fakta barang: nama, material, fungsi, kondisi, spesifikasi |
| ✅ Analisis KUM HS 1–6 | AI reasoning menggunakan aturan hukum secara berurutan |
| ✅ Rekomendasi kode HS 8-digit | Sesuai format BTKI nasional Indonesia |
| ✅ Penjelasan reasoning | Kenapa kode dipilih, kenapa kode lain dieliminasi |
| ✅ Confidence level | TINGGI / SEDANG / RENDAH |
| ✅ Flag "Perlu Expert Review" | Otomatis jika kasus kompleks |
| ✅ Bilingual | Indonesia & Inggris |

**Keterbatasan V1 yang perlu diketahui:**
- Hasil bisa tidak akurat jika AI "mengarang" kode HS yang tidak ada di BTKI *(akan diperbaiki di V2)*
- Belum ada informasi tarif, bea masuk, dan pajak impor *(fitur baru di V2)*
- Tidak ada history / riwayat klasifikasi
- Data barang yang diisi tidak tersimpan (setiap session baru dari awal)
- Hanya bisa diakses via browser, belum ada login/akun

---

## Yang Akan Dibangun — V2

V2 adalah pengembangan besar yang mengubah sistem dari "asisten berbasis AI saja" menjadi **sistem klasifikasi legal-grade** yang bisa dipertanggungjawabkan secara hukum.

### Perubahan Utama V1 → V2

```
V1 (Sekarang)                        V2 (Yang Akan Dibangun)
─────────────────────────            ─────────────────────────────────
AI yang menentukan kode HS    →      AI hanya bantu ekstrak fakta,
                                     kode HS wajib dari database BTKI

Data BTKI hanya ~105 entri    →      Database BTKI lengkap 11.491+ kode

Tidak ada tarif & pajak       →      Info MFN, FTA, PPN, PPh 22 tersedia

Tidak ada riwayat/akun        →      Login, history, audit trail

Tidak ada verifikasi          →      Setiap kode HS divalidasi ke database
```

---

## Fitur-Fitur V2 yang Akan Dikembangkan

### 1. Database BTKI Lengkap
Semua **11.491 kode HS** BTKI 2022 tersimpan di database, bilingual (Indonesia & Inggris), terstruktur per seksi → bab → heading → subheading.

### 2. Mesin Klasifikasi Deterministik (Rules Engine)
Tidak lagi bergantung sepenuhnya pada AI untuk memutuskan kode. KUM HS 1–6 diimplementasi sebagai aturan program yang jelas dan bisa diaudit.

### 3. Informasi Tarif & Pajak *(Fitur Baru)*
Setelah kode HS ditentukan, sistem langsung menampilkan:

| Informasi | Keterangan |
|-----------|-----------|
| **Tarif MFN (Bea Masuk Umum)** | Tarif yang berlaku dari BTKI |
| **Tarif FTA (Perjanjian Dagang)** | ASEAN, CAFTA, EPA, dll — ditampilkan sebagai *referensi*, bukan kepastian eligibilitas |
| **PPN Impor** | Tarif standar dari BTKI |
| **PPh Pasal 22 Impor** | Tarif standar dari BTKI |

> **Catatan penting tentang FTA:** Tarif FTA yang ditampilkan adalah referensi ketersediaan, bukan jaminan keberlakuan. Eligibilitas FTA tergantung asal barang, pemenuhan Rules of Origin, kepemilikan Certificate of Origin, dan persyaratan prosedural lainnya.

> **Catatan tentang PPN & PPh 22:** Tarif ditampilkan berdasarkan BTKI standar. Fasilitas pembebasan, pengurangan, atau tarif khusus berdasarkan PMK tertentu tidak dicakup di fase ini.

### 4. Penelusuran Sumber (Audit Trail)
Setiap rekomendasi kode HS dan tarif dapat ditelusuri kembali ke sumber dokumennya — dari paragraf mana di BTKI, dari pasal mana di Catatan Bab/Bagian, dari EN (Explanatory Notes) yang mana.

### 5. Riwayat Klasifikasi & Akun
Login, history klasifikasi, ekspor hasil ke PDF/Excel.

### 6. Sistem Validasi Berlapis
Kode HS yang tidak ada di database BTKI secara otomatis ditolak — menghilangkan risiko AI "mengarang" kode yang tidak valid.

---

## Rencana Pengembangan (3–4 Bulan)

### Bulan 1 — Fondasi Database & Data

**Minggu 1–2: Setup Infrastruktur**
- Setup project backend (C# ASP.NET Core)
- Setup database MySQL dengan schema lengkap
- Import semua 11.491 kode HS dari BTKI 2022 ke database

**Minggu 3–4: Manajemen Versi Data**
- Sistem versi data BTKI (untuk mengakomodasi revisi PMK di masa depan)
- Import data Explanatory Notes (EN) — panduan interpretasi resmi WCO
- Import Catatan Bab/Bagian legal

📦 **Hasil akhir Bulan 1:** Database BTKI lengkap, bisa di-query, siap dipakai oleh mesin klasifikasi

---

### Bulan 2 — Mesin Klasifikasi & AI

**Minggu 1–2: Ekstraksi Fakta**
- AI dipakai untuk mengekstrak fakta dari input teks bebas
- 35 tipe fakta (nama, material, fungsi, dimensi, kapasitas, dll)
- Deteksi konflik antar fakta, tanya konfirmasi ke user

**Minggu 3–4: Mesin KUM HS**
- Implementasi KUM HS 1 (heading terms + Catatan Bab)
- Implementasi KUM HS 2 (barang tidak lengkap, campuran material)
- Implementasi KUM HS 3 (spesifisitas, essential character, urutan numerik)
- Implementasi KUM HS 4 & 5 (most akin, kemasan & peti)
- Implementasi KUM HS 6 (descent ke subheading 6-digit → 8-digit)

📦 **Hasil akhir Bulan 2:** Mesin klasifikasi berjalan, dapat menghasilkan rekomendasi kode HS berdasarkan database, bukan tebakan AI

---

### Bulan 3 — Tarif, Validasi & API

**Minggu 1–2: Informasi Tarif & Pajak**
- Lookup tarif MFN (Bea Masuk Umum) per kode HS
- Lookup tarif FTA (semua skema perjanjian dagang) dengan disclaimer
- Lookup PPN dan PPh Pasal 22 impor dengan disclaimer
- Validasi tanggal berlaku tarif

**Minggu 3–4: Validasi & API Backend**
- Sistem validasi: kode HS wajib ada di database BTKI aktif
- API endpoint untuk klasifikasi, riwayat, tarif
- Sistem autentikasi (login/akun)
- Audit trail — setiap keputusan bisa ditelusuri ke sumbernya

📦 **Hasil akhir Bulan 3:** Sistem backend lengkap, API siap dipakai frontend

---

### Bulan 4 — Frontend, Integrasi & Peluncuran

**Minggu 1–2: Update Frontend**
- Update tampilan React untuk konek ke backend V2
- Tampilan baru: iterasi pertanyaan (AI tanya fakta yang kurang)
- Tampilan hasil tarif lengkap dengan disclaimer
- Tampilan reasoning chain yang bisa diklik ke sumber

**Minggu 3: Admin Panel**
- Dashboard admin untuk upload & kelola data BTKI
- Antarmuka validasi data sebelum diaktifkan
- Manajemen versi data (BTKI 2022, siap untuk update berikutnya)

**Minggu 4: Testing & Peluncuran**
- Testing akhir seluruh alur
- Dokumentasi update
- Deploy ke production
- Handover ke tim operasional

📦 **Hasil akhir Bulan 4:** V2 siap digunakan, semua fitur aktif

---

## Ringkasan Timeline

```
Bulan 1          Bulan 2          Bulan 3          Bulan 4
───────────────  ───────────────  ───────────────  ───────────────
Database BTKI    Mesin KUM HS     Tarif & Pajak    Frontend V2
Setup backend    Ekstraksi fakta  Validasi kode    Admin panel
Import 11.491    AI fact engine   API backend      Testing & Go-live
kode HS          KUM HS 1–6       Auth & audit     Dokumentasi
```

---

## Pertanyaan yang Sering Ditanyakan

**Q: Apakah V2 menggantikan V1?**  
Ya. V2 adalah pengganti penuh V1, dengan semua fitur V1 tetap ada plus fitur-fitur baru.

**Q: Apakah V1 masih bisa dipakai selama V2 dikembangkan?**  
Ya. V1 tetap berjalan selama proses pengembangan V2.

**Q: Apakah tarif FTA yang ditampilkan berarti saya otomatis dapat tarif tersebut?**  
Tidak. Tarif FTA ditampilkan sebagai referensi ketersediaan. Untuk dapat tarif FTA, perlu memenuhi Rules of Origin, memiliki Certificate of Origin yang valid, dan memenuhi persyaratan prosedural lainnya. Konsultasikan dengan ahli kepabeanan atau PPJK Anda.

**Q: Apakah setelah V2 hasil klasifikasi bisa langsung dipakai di dokumen resmi?**  
Tidak. Sistem ini tetap bersifat rekomendasi. Penetapan resmi kode HS tetap merupakan kewenangan DJBC. Output sistem ini membantu persiapan dan verifikasi, bukan menggantikan keputusan pejabat berwenang.

**Q: Berapa akurasi sistem ini?**  
V1 akurasinya bergantung pada kualitas input dan ketersediaan data referensi. V2 didesain untuk lebih akurat karena semua kode HS harus ada di database (tidak bisa mengarang) dan menggunakan aturan KUM HS deterministik.

---

*Dokumen ini akan diperbarui seiring perkembangan proyek.*  
*Pertanyaan: hubungi Tim IT SMART.*
