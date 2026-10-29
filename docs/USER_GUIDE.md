# AI HS Classification Intelligence

**Versi:** MVP v1.0  
**Untuk:** Tim operasional, staf kepabeanan, pengguna internal SMART  
**Referensi:** BTKI 2022 (PMK 26/2022)

---

> ⚠️ **Penting:** Hasil dari sistem ini adalah **rekomendasi**, bukan penetapan resmi DJBC.  
> Selalu verifikasi dengan ahli kepabeanan sebelum digunakan untuk keperluan dokumen resmi.

---

## Daftar Isi

1. [Apa itu AI HS Classification Intelligence?](#1-apa-itu-ai-hs-classification-intelligence)
2. [Tampilan Aplikasi](#2-tampilan-aplikasi)
3. [Cara Mengisi Form — Step by Step](#3-cara-mengisi-form--step-by-step)
4. [Membaca Hasil Analisis](#4-membaca-hasil-analisis)
5. [Contoh Penggunaan Nyata](#5-contoh-penggunaan-nyata)
6. [Tips agar Hasil Akurat](#6-tips-agar-hasil-akurat)
7. [Memahami Confidence Level](#7-memahami-confidence-level)
8. [Kapan Harus Minta Expert Review?](#8-kapan-harus-minta-expert-review)
9. [Pertanyaan Umum (FAQ)](#9-pertanyaan-umum-faq)
10. [Batasan Sistem Saat Ini](#10-batasan-sistem-saat-ini)

---

## 1. Apa itu AI HS Classification Intelligence?

Sistem ini membantu menentukan **kode HS (Harmonized System)** yang tepat untuk barang impor berdasarkan **BTKI 2022** (Buku Tarif Kepabeanan Indonesia).

**Cara kerjanya berbeda dari pencarian biasa:**

❌ **Bukan ini:** Mengetik nama barang → sistem cari kata yang mirip di database HS

✅ **Ini yang terjadi:**
1. Kamu isi fakta-fakta objektif barang lewat form
2. AI membaca fakta tersebut dan menyusun karakterisasi legal barang
3. AI menguji kandidat kode HS secara berurutan menggunakan **KUM HS 1–6** (Ketentuan Umum Menafsir HS)
4. AI menghasilkan rekomendasi + penjelasan reasoning-nya

Hasilnya bukan sekadar kode — tapi juga **kenapa** kode itu dipilih dan **kenapa** kode lain dieliminasi.

> ⚠️ **[FOTO: Screenshot halaman utama aplikasi — tampilkan layout 2 kolom penuh]**

---

## 2. Tampilan Aplikasi

Aplikasi terbagi menjadi **2 kolom utama:**

```
┌─────────────────────┬──────────────────────┐
│  📋 DATA BARANG    │   🔍 HASIL ANALISIS  │
│                     │                      │
│  [Form input]       │  [Hasil AI muncul    │
│                     │   di sini setelah    │
│                     │   form disubmit]     │
└─────────────────────┴──────────────────────┘
```

- **Kolom kiri:** Form pengisian data barang
- **Kolom kanan:** Hasil analisis AI (akan muncul setelah klik tombol Analisis)

Di bagian atas terdapat header dengan nama aplikasi dan badge **"MVP · Data Terbatas"** yang menunjukkan sistem ini masih dalam tahap pengembangan.

> ⚠️ **[FOTO: Screenshot header aplikasi]**

---

## 3. Cara Mengisi Form — Step by Step

Form terbagi dalam **5 bagian**. Isi dari atas ke bawah.

---

### 📦 Bagian 1: Identitas Barang

> ⚠️ **[FOTO: Screenshot Bagian 1 — Identitas Barang]**

**Nama Dagang / Brand / Model**
- Isi dengan nama resmi produk dari pabrik/katalog
- Contoh: `MacBook Air M2`, `Komatsu PC200`, `Crane Barge Majulah`
- Boleh dikosongkan jika tidak relevan

**Nama Generik Barang** ⭐ *Wajib diisi*
- Isi dengan nama umum/teknis barang — ini yang paling penting untuk AI
- Contoh: `Laptop`, `Excavator`, `Floating Crane`, `Generating Set`
- Hindari nama marketing — gunakan nama teknis

> 💡 **Tips:** Nama generik menentukan arah reasoning AI. Tulis "Dumper" bukan "Agricultural Transport Vehicle".

---

### 🔩 Bagian 2: Fisik & Material

> ⚠️ **[FOTO: Screenshot Bagian 2 — Fisik & Material dengan beberapa material dipilih]**

**Material Utama** *(bisa pilih lebih dari satu)*
- Pilih semua material yang menjadi komponen dominan barang
- Klik chip untuk memilih (chip berubah warna biru), klik lagi untuk batal
- Contoh untuk laptop: pilih `Aluminium` + `Plastik`

**Kondisi Saat Impor**
- Pilih sesuai kondisi fisik barang saat memasuki Indonesia

| Pilihan | Kapan digunakan |
|---------|----------------|
| Lengkap dan jadi | Barang utuh, siap pakai |
| Belum lengkap / belum selesai | Ada komponen yang kurang tapi karakter utama sudah ada |
| Terurai / CKD | Semua komponen ada tapi belum dirakit |
| SKD | Sebagian sudah dirakit |
| Curah | Material/komoditas dalam jumlah besar tanpa kemasan khusus |
| Campuran bahan | Mixture dari beberapa zat/bahan |
| Bagian/Part | Hanya komponen dari mesin/barang lain |
| Set/Kit | Beberapa item berbeda yang dijual sebagai satu paket |

---

### ⚙️ Bagian 3: Fungsi & Cara Kerja

> ⚠️ **[FOTO: Screenshot Bagian 3 — Fungsi & Cara Kerja]**

**Fungsi Utama Barang**
- Pilih **satu** fungsi yang paling dominan
- Ini adalah salah satu faktor penentu terpenting dalam KUM HS
- Contoh: genset → pilih "Menghasilkan/Mengkonversi energi listrik"

**Cara Kerja / Prinsip Operasi** *(bisa pilih lebih dari satu)*
- Pilih semua prinsip kerja yang berlaku
- Contoh untuk excavator: pilih `Diesel / Motor bakar` + `Hidrolik`

**Mobilitas**
- Pilih bagaimana barang berpindah tempat

| Pilihan | Contoh barang |
|---------|---------------|
| Self-propelled | Excavator, truck, traktor |
| Ditarik (towed) | Crane barge, trailer |
| Portabel | Laptop, kamera, alat ukur |
| Statis / Tidak bergerak | Genset permanen, mesin pabrik |
| Terapung | Kapal, crane barge |

---

### 🏭 Bagian 4: Konteks Penggunaan

> ⚠️ **[FOTO: Screenshot Bagian 4 — Konteks Penggunaan]**

**Area / Lingkungan Operasi**
- Di mana barang ini digunakan secara utama?
- Penting untuk membedakan kendaraan on-road vs off-highway

**Sektor / Industri Pengguna**
- Pilih sektor industri yang paling relevan dengan penggunaan barang ini

---

### 📋 Bagian 5: Karakteristik Tambahan

> ⚠️ **[FOTO: Screenshot Bagian 5 — Karakteristik Tambahan]**

**Apakah ini bagian/part dari barang yang lebih besar?**
- Pilih "Ya" jika barang yang diimpor adalah komponen/spare part
- Contoh: cylinder head engine, blade excavator, screen laptop
- Pemilihan "Ya" akan memicu AI untuk mempertimbangkan pos spare part

**Apakah disajikan sebagai set/kit lengkap?**
- Pilih "Ya" jika beberapa item berbeda dikemas dan dijual bersama
- Mempengaruhi penerapan KUM HS 3(b) tentang essential character

**Apakah ada komponen/fungsi tambahan terintegrasi?**
- Pilih "Ya" jika barang punya fungsi tambahan selain fungsi utamanya
- Contoh: laptop dengan kamera built-in, smartphone dengan GPS
- Jika "Ya", sebutkan di kolom Keterangan Tambahan

**Spesifikasi Teknis Utama**
- Isi data numerik dan teknis yang relevan
- Contoh: `GVW 4.5 ton`, `Output 550 kVA`, `Layar 15 inci berat 1.8 kg`
- Semakin lengkap semakin akurat hasilnya

**Keterangan Tambahan**
- Informasi lain yang belum tertampung di field sebelumnya
- Contoh: `Ditarik dengan kapal Jaewon 2`, `Dilengkapi attachement bucket dan hammer`

---

### Tombol Aksi

> ⚠️ **[FOTO: Screenshot tombol Analisis HS Code dan Reset]**

**🔍 Analisis HS Code**
- Klik setelah semua field relevan terisi
- AI akan mulai menganalisis — proses membutuhkan 3–8 detik
- Selama proses, tombol berubah menjadi "Menganalisis..." dan tidak bisa diklik

**Reset**
- Mengosongkan seluruh form
- Hasil analisis di kolom kanan tidak ikut terhapus

---

## 4. Membaca Hasil Analisis

Setelah analisis selesai, kolom kanan menampilkan hasil dalam beberapa blok:

---

### Blok 1: Rekomendasi HS Code

> ⚠️ **[FOTO: Screenshot blok Rekomendasi HS Code dengan confidence TINGGI (hijau)]**

```
Rekomendasi HS Code
8471.30.20                    [Confidence: TINGGI]

Laptop termasuk notebook dan subnotebook
```

- **Kode HS** ditampilkan besar di tengah
- **Confidence badge** menunjukkan tingkat keyakinan AI (lihat Bagian 7)
- **Uraian** adalah deskripsi resmi pos/subpos BTKI
- Jika ada risiko, ditampilkan di bawah uraian dengan ikon ⚠️

---

### Blok 2: Reasoning KUM HS

> ⚠️ **[FOTO: Screenshot blok Reasoning KUM HS dengan beberapa step]**

```
⚖️ Reasoning KUM HS

[KUM HS 1]  Barang memenuhi Catatan 5 Bab 84 sebagai mesin pengolah
            data otomatis portabel. Pos 84.71 menggambarkan barang
            secara langsung.

[KUM HS 6]  Setelah pos 84.71 ditentukan, subpos 8471.30 dipilih
            karena barang portabel, berat ≤10kg, memiliki CPU +
            keyboard + display terintegrasi dalam satu unit.
```

Ini menjelaskan **urutan logika** yang digunakan AI. Hanya KUM HS yang relevan yang ditampilkan — bukan semua 6 rule.

---

### Blok 3: Kandidat yang Dieliminasi

> ⚠️ **[FOTO: Screenshot blok Kandidat Dieliminasi]**

```
❌ Kandidat yang Dieliminasi

8525    Kamera bukan fungsi utama. Catatan 3 Bagian XVI →
        fungsi utama = ADP (pengolahan data).

8528    Display tidak menjadikan keseluruhan laptop sebagai
        monitor standalone.
```

Ini menjelaskan kode-kode yang **sempat dipertimbangkan tapi dieliminasi** beserta alasannya. Sangat berguna untuk verifikasi dan audit.

---

### Blok 4: Fakta yang Perlu Dikonfirmasi *(jika ada)*

> ⚠️ **[FOTO: Screenshot blok Fakta Perlu Konfirmasi (warna kuning)]**

```
❓ Fakta yang Masih Perlu Dikonfirmasi

• Konfirmasi berat unit tidak melebihi 10 kg
• Pastikan barang memiliki kemampuan ADP sesuai Catatan Bab 84
```

Ini adalah **PR yang harus kamu verifikasi** sebelum menyimpulkan kode HS final. AI tidak bisa memastikan fakta ini dari input yang diberikan.

---

### Blok 5: Expert Review Flag *(jika ada)*

> ⚠️ **[FOTO: Screenshot banner Expert Review (warna merah)]**

```
👨‍⚖️ Perlu Expert Review
Kasus ini melibatkan KUM HS 4 atau terdapat konflik bukti
yang tidak dapat diselesaikan secara otomatis.
```

Jika banner ini muncul, jangan gunakan hasil ini tanpa konsultasi ahli kepabeanan terlebih dahulu.

---

### State Lainnya

**Saat loading:**

> ⚠️ **[FOTO: Screenshot loading state dengan spinner]**

Spinner berputar dengan teks "AI sedang menganalisis barang..."

**Saat ada error:**

> ⚠️ **[FOTO: Screenshot error state]**

Kotak merah dengan pesan error. Penyebab umum: koneksi internet terputus atau server AI sedang gangguan.

---

## 5. Contoh Penggunaan Nyata

### Contoh A: Laptop

**Skenario:** Tim impor menerima dokumen untuk MacBook Air M2, ingin verifikasi kode HS.

**Isi form:**
- Nama dagang: `MacBook Air M2`
- Nama generik: `Laptop`
- Material: `Aluminium`
- Kondisi impor: `Lengkap dan jadi`
- Fungsi utama: `Mengolah data / Komputasi`
- Cara kerja: `Digital / Elektronik`
- Mobilitas: `Portabel (dibawa tangan)`
- Spesifikasi: `Layar 13 inci, berat 1.24 kg, Apple M2`

**Hasil yang diharapkan:** `8471.30.20` — Laptop termasuk notebook (Confidence: TINGGI)

> ⚠️ **[FOTO: Screenshot hasil analisis untuk laptop]**

---

### Contoh B: Dumper Off-Highway

**Skenario:** Impor alat berat perkebunan, nama katalog "Agricultural Tractor WEA08" tapi fungsinya mengangkut dan menumpahkan.

**Isi form:**
- Nama dagang: `WEA08`
- Nama generik: `Dumper`
- Material: `Baja/Besi`
- Kondisi impor: `Lengkap dan jadi`
- Fungsi utama: `Mengangkut barang (transportasi darat)`
- Cara kerja: `Diesel / Motor bakar` + `Hidrolik`
- Mobilitas: `Self-propelled`
- Area operasi: `Off-highway / Perkebunan / Pertambangan`
- Spesifikasi: `GVW 4.5 ton, hydraulic dump body, ban off-highway, tanpa axle suspension`

**Hasil yang diharapkan:** `8704.10.31` — Dumper off-highway, GVW ≤5 ton (Confidence: TINGGI)

> 💡 **Catatan:** Meskipun nama dagang mengandung kata "Tractor", AI akan mengklasifikasikan berdasarkan fakta teknis (dump body, GVW, area operasi), bukan nama. Ini adalah salah satu keunggulan sistem ini.

> ⚠️ **[FOTO: Screenshot hasil analisis untuk dumper — tampilkan juga blok kandidat dieliminasi yang menunjukkan 8701 dieliminasi]**

---

## 6. Tips agar Hasil Akurat

### ✅ Yang Harus Dilakukan

**Gunakan nama generik teknis, bukan nama marketing**
- ✅ `Dumper` bukan `Agricultural Transport Machine`
- ✅ `Generating Set` bukan `Power Backup Solution`
- ✅ `Cylinder Head` bukan `Engine Component Part A`

**Isi kondisi impor dengan benar**
- CKD dan barang lengkap bisa menghasilkan kode yang berbeda
- KUM HS 2(a) berlaku untuk CKD — AI akan memperlakukannya seperti barang lengkap jika karakter pokok sudah ada

**Masukkan spesifikasi numerik**
- GVW, kapasitas, daya, berat sangat mempengaruhi subpos
- Contoh: GVW 4.5 ton vs 6 ton bisa beda subpos di 87.04

**Tandai jika ini spare part**
- Spare part punya pos tersendiri (umumnya xx.09 atau pos khusus "parts")
- Jangan biarkan AI menebak sendiri

**Sebutkan fungsi tambahan jika ada**
- Laptop dengan kamera built-in → sebutkan di keterangan
- AI perlu tahu ini untuk menerapkan Catatan 3 Bagian XVI

### ❌ Yang Harus Dihindari

- Jangan isi nama generik dengan nama produk marketing
- Jangan pilih fungsi utama yang salah hanya karena pilihan lain tidak ada — gunakan "Lainnya" dan jelaskan di keterangan
- Jangan abaikan blok "Fakta Perlu Dikonfirmasi" — itu informasi penting
- Jangan langsung gunakan kode HS dari hasil jika confidence-nya RENDAH

---

## 7. Memahami Confidence Level

| Badge | Warna | Artinya |
|-------|-------|---------|
| **TINGGI** | 🟢 Hijau | AI sangat yakin. Fakta cukup, reasoning KUM HS jelas, tidak ada konflik kandidat. Tetap verifikasi sebelum digunakan dokumen resmi. |
| **SEDANG** | 🟡 Kuning | AI cukup yakin tapi ada ketidakpastian. Bisa karena spesifikasi kurang lengkap atau ada 2 kandidat yang sama kuat. Perlu verifikasi lebih teliti. |
| **RENDAH** | 🔴 Merah | AI tidak cukup yakin. Fakta tidak memadai atau ada konflik logika. **Wajib dikonsultasikan dengan ahli sebelum digunakan.** |

---

## 8. Kapan Harus Minta Expert Review?

Sistem akan **otomatis menampilkan banner "Perlu Expert Review"** dalam kondisi:
- AI harus menggunakan KUM HS 4 (barang tidak terklasifikasi oleh KUM HS 1–3)
- Ada konflik bukti yang tidak bisa diselesaikan otomatis
- Confidence RENDAH

Selain itu, **kamu sendiri harus meminta expert review** jika:
- Barang baru yang belum pernah diklasifikasi sebelumnya
- Nilai bea masuk sangat besar — risiko salah klasifikasi tinggi
- Barang composite/multifungsi yang kompleks
- Ada perbedaan antara nama di dokumen dan fungsi aktual barang
- Blok "Fakta Perlu Konfirmasi" berisi banyak item

---

## 9. Pertanyaan Umum (FAQ)

**Q: Apakah hasil dari sistem ini bisa langsung dipakai untuk dokumen PIB/BC?**  
A: Tidak langsung. Hasil ini adalah rekomendasi untuk membantu analisis. Penetapan resmi tetap oleh DJBC. Gunakan sebagai referensi dan cross-check dengan ahli kepabeanan.

**Q: Kenapa nama barang yang sama bisa menghasilkan kode berbeda?**  
A: Karena kode HS ditentukan oleh fakta teknis, bukan nama. Sebuah "Tractor" bisa jadi 8701 (traktor pertanian) atau 8704.10 (dumper off-highway) tergantung konstruksi dan fungsinya. Semakin lengkap data yang diisi, semakin tepat hasilnya.

**Q: Apakah data yang saya masukkan disimpan?**  
A: Tidak. Versi MVP ini stateless — tidak ada data yang disimpan ke server. Setiap session baru dimulai dari kosong.

**Q: Berapa lama proses analisis berlangsung?**  
A: Biasanya 3–8 detik, tergantung kecepatan koneksi internet dan load server AI.

**Q: Apakah bisa digunakan untuk semua jenis barang?**  
A: Sistem ini didesain untuk semua jenis barang impor. Coverage terbaik saat ini ada pada:

| Kategori | Bab | Contoh Barang |
|----------|-----|---------------|
| Mesin & alat berat | 84 | Excavator, bulldozer, crane, pompa, kompresor, genset, komputer |
| Elektronik & listrik | 85 | Smartphone, laptop, motor listrik, kabel, switch, router |
| Kendaraan | 87 | Mobil, motor, truk, dumper off-highway, traktor pertanian |
| Kapal & struktur terapung | 89 | Kapal kargo, tanker, crane barge, kapal keruk |
| Minyak bumi | 27 | Crude oil, solar, bensin, avtur |
| Farmasi | 30 | Obat-obatan, vaksin |
| Plastik | 39 | PE, PP, PVC bentuk primer |
| Karet | 40 | Ban pneumatik |
| Baja & logam | 72–76 | Plat baja HRC/CRC, pipa OCTG, tembaga, aluminium |

Untuk bab di luar tabel di atas (tekstil, agrikultur, kimia khusus, furnitur, dll), AI tetap bisa memberikan rekomendasi tapi verifikasi digit nasional lebih ketat diperlukan.

**Q: Apa yang dimaksud "KUM HS"?**  
A: KUM HS adalah singkatan dari Ketentuan Umum Menafsir HS — 6 aturan resmi yang digunakan secara berurutan untuk menentukan klasifikasi barang. Sistem ini mengimplementasikan logika ini di dalam prompt AI.

**Q: Kenapa ada barang yang AI-nya rekomendasikan "perlu konfirmasi GVW"?**  
A: Karena subpos BTKI untuk kendaraan sering dibagi berdasarkan GVW (Gross Vehicle Weight). AI tidak bisa memastikan berat dari input yang ada, sehingga meminta konfirmasi.

---

## 10. Batasan Sistem Saat Ini

Sistem MVP ini memiliki beberapa keterbatasan yang perlu dipahami:

| Batasan | Penjelasan |
|---------|-----------|
| **Data BTKI parsial** | 105 subpos nasional dari 9 bab prioritas sudah di-inject ke prompt (Bab 27, 30, 39, 40, 72–76, 84–85, 87–89). Untuk bab di luar coverage ini AI mengandalkan training data. Target selanjutnya: lengkapi semua 97 bab. |
| **Tidak ada EN (Explanatory Notes)** | Analisis menggunakan KUM HS tapi belum menyertakan EN lengkap per heading. Untuk kasus complex, EN bisa mengubah hasil. |
| **Tidak ada history** | Riwayat klasifikasi tidak disimpan. Tidak ada audit trail untuk saat ini. |
| **Input hanya form** | Belum bisa upload dokumen, foto, atau invoice. Input visual dan dokumen direncanakan untuk versi berikutnya. |
| **Hanya rekomendasi** | Output tidak mengikat secara hukum dan tidak menggantikan keputusan pejabat DJBC. |

---

*Dokumen ini akan diperbarui seiring pengembangan sistem.*  
*Pertanyaan atau laporan bug: hubungi Tim IT SMART.*
