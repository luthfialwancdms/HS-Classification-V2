# UI Summary — AI HS Classification Intelligence V2
**Untuk: AI Frontend Generator / Frontend Developer**

Dokumen ini berisi ringkasan lengkap tampilan UI, flow, komponen, state, dan interaksi sistem AI HS Classification Intelligence V2. Cocok untuk generate frontend UI atau memahami struktur aplikasi secara menyeluruh.

---

## 1. Ringkasan Sistem

**Apa sistem ini?**
Aplikasi web untuk klasifikasi kode HS (Harmonized System) barang impor berdasarkan BTKI 2022. Pengguna mengisi data barang, sistem mencari kode HS yang tepat di database BTKI menggunakan logika KUM HS 1-6, menampilkan tarif dan reasoning, serta memfasilitasi review ahli kepabeanan.

**Teknologi Stack (Reference):**
- Backend: C# ASP.NET Core + MySQL
- Frontend: React + Vite + Tailwind CSS + shadcn/ui (atau equivalent)
- AI: Groq API (untuk terjemahan istilah saja, bukan generasi kode HS)

**Prinsip UI:**
- Database-first: semua kode HS dan deskripsi dari database, bukan AI
- Ask, don't guess: sistem bertanya jika fakta kurang, tidak default
- Audit trail: setiap keputusan tercatat dan bisa ditelusuri
- Role-based access: tampilan disesuaikan dengan peran user

---

## 2. Peran Pengguna (User Roles)

| Peran | Akses Menu | Fungsi Utama |
|-------|-----------|--------------|
| **Classifier** | Klasifikasi, Riwayat | Mengisi form barang, mendapat hasil klasifikasi, minta expert review |
| **Expert Reviewer** | Klasifikasi, Riwayat, Review Ahli | Semua fungsi Classifier + memutuskan case yang masuk antrian review |
| **Admin** | Semua (Klasifikasi, Riwayat, Review Ahli, Admin Data Center) | Semua fungsi Expert + mengelola data BTKI (upload, validasi, aktivasi) |
| **Viewer** | Riwayat (read-only, hanya miliknya) | Melihat riwayat klasifikasi milik sendiri |

**Login:**
- Email + Password
- Setelah login, header menampilkan nama user, peran, dan menu sesuai role

---

## 3. Layout Utama

### 3.1 Header (Persistent)

```
┌─────────────────────────────────────────────────────────────────┐
│ [HS Logo] AI HS Classification Intelligence     [BETA V2]       │
│                                                                   │
│ [Klasifikasi] [Riwayat] [Review Ahli*] [Admin Data Center**]    │
│                                                                   │
│                           [User Avatar] [Randi A.] [Logout▼]    │
└─────────────────────────────────────────────────────────────────┘
```

**Catatan:**
- `*` hanya untuk Expert dan Admin
- `**` hanya untuk Admin
- Badge "BETA V2" berwarna amber/warning
- User dropdown: Profile, Settings, Logout

### 3.2 Color Scheme & Design Tokens

**Colors:**
- Primary: `#2563eb` (blue-600) — untuk CTA buttons, active states
- Success: `#16a34a` (green-600) — untuk confidence TINGGI, approved
- Warning: `#d97706` (amber-600) — untuk confidence SEDANG, ambiguities
- Error: `#dc2626` (red-600) — untuk confidence RENDAH, rejected
- Neutral: `#64748b` (slate-500) — untuk secondary text
- Background: `#f8fafc` (slate-50)
- Card: `#ffffff` with `border: 1px solid #e2e8f0`

**Typography:**
- Font: Inter, system-ui, sans-serif
- HS Code Display: `font-size: 32px`, `font-weight: 800`, monospace
- Section Titles: `font-size: 14px`, `font-weight: 600`
- Body: `font-size: 13px`
- Labels: `font-size: 12px`, `text-transform: uppercase`

**Spacing:**
- Container max-width: `1280px`
- Card padding: `20px`
- Form field gap: `12px`
- Section gap: `20px`

---

## 4. Screen 1: Klasifikasi (Halaman Utama)

**URL:** `/classify`  
**Layout:** 2 kolom (Form kiri, Result kanan)

### 4.1 Kolom Kiri: Form Data Barang

**Card Header:**
```
📦 Data Barang                                        [Reset]
```

**Form Sections:**

#### Section 1: Identitas Barang
```
IDENTITAS BARANG (uppercase, muted)

Label: Nama Dagang / Brand / Model
Input: text, placeholder: "cth: MacBook Air M2, Komatsu PC200"

Label: Nama Generik Barang ★ (required)
Input: text, placeholder: "cth: Laptop, Excavator, Crane"
```

#### Section 2: Fisik & Material
```
FISIK & MATERIAL

Label: Material Utama
Chips (multi-select):
[Baja/Besi] [Aluminium] [Plastik] [Karet] [Tembaga] [Kimia]
(selected state: blue bg, blue border)

Label: Kondisi Saat Impor
Select dropdown:
- Lengkap dan jadi (siap pakai)
- Belum lengkap / belum selesai
- Terurai / CKD
- SKD
- Curah
- Campuran bahan
- Bagian/Part
- Set/Kit
```

#### Section 3: Fungsi & Cara Kerja
```
FUNGSI & CARA KERJA

Label: Fungsi Utama Barang
Select dropdown: (long list, ~30 options)
- Menggali / Earthmoving
- Mengangkat / Lifting
- Mengolah data / Komputasi
- ... (see wireframe for full list)

Label: Cara Kerja / Prinsip Operasi
Chips (multi-select):
[Diesel/Motor bakar] [Hidrolik] [Elektrik] [Pneumatik]

Label: Mobilitas
Chips (multi-select):
[Self-propelled] [Ditarik] [Portabel] [Statis] [Terapung]
```

#### Section 4: Konteks Penggunaan
```
KONTEKS & PENGGUNAAN

Label: Area / Lingkungan Operasi
Select dropdown:
- Off-highway / Perkebunan / Pertambangan
- On-road / Jalan raya
- Perairan / Laut
- ...

Label: Sektor / Industri Pengguna
Select dropdown:
- Pertambangan
- Konstruksi
- Manufaktur
- ...
```

#### Section 5: Karakteristik Tambahan
```
KARAKTERISTIK TAMBAHAN

Label: Apakah ini bagian/part dari barang yang lebih besar?
Radio: ( ) Ya  ( ) Tidak

Label: Apakah disajikan sebagai set/kit lengkap?
Radio: ( ) Ya  ( ) Tidak

Label: Apakah ada komponen/fungsi tambahan terintegrasi?
Radio: ( ) Ya  ( ) Tidak

Label: Spesifikasi Teknis Utama
Input: text, placeholder: "cth: GVW 4.5 ton, Output 550 kVA"

Label: Keterangan Tambahan
Textarea: rows 3, placeholder: "Informasi lain yang relevan..."
```

**Action Buttons:**
```
[🔍 Analisis HS Code]  (primary button, full-width)
```

**States:**
- Normal: Primary blue button
- Loading: Button disabled, text "Menganalisis...", spinner icon
- Error: Show red banner above button with error message

---

### 4.2 Kolom Kanan: Hasil Analisis

**Card Header:**
```
🔍 Hasil Analisis                              [Export PDF] (hidden until result)
```

#### State 1: Empty (Initial)
```
       🔎 (large icon)

  Belum ada analisis

  Isi data barang di sebelah kiri,
  lalu klik Analisis HS Code
```

#### State 2: Loading
```
       🔄 (spinner)

  AI sedang menganalisis barang...
```

#### State 3: Klarifikasi (Need User Input)
```
Info box (blue border):
Sistem menemukan beberapa kandidat yang perlu dikonfirmasi.

Kandidat sementara: 84.29, 84.30

┌───────────────────────────────────────────────────────────┐
│ Pertanyaan 1 — Pos 8429 (Mesin penggali...)               │
│ Pilih subpos yang paling sesuai dengan barang Anda:       │
│                                                            │
│ ( ) 8429.1 — Buldoser dan angledoser                      │
│ ( ) 8429.2 — Grader dan mesin perata                      │
│ ( ) 8429.3 — Mesin pengikis                               │
│ ( ) 8429.4 — Mesin pemadat dan mesin gilas jalan          │
│ (•) 8429.5 — Sekop mekanik, ekskavator dan shovel loader  │
│ ( ) Tidak ada yang sesuai / tidak yakin                   │
│                                                            │
│                                    [Lanjutkan]             │
└───────────────────────────────────────────────────────────┘
```

**Jika pilih "Tidak yakin":**
- Case otomatis masuk antrian Expert Review
- Tampilkan notifikasi: "Case Anda telah dikirim ke Expert Reviewer"

**Jika pilih salah satu:**
- Sistem lanjut ke pertanyaan berikutnya (jika ada)
- Atau langsung ke State 4 (Result)

#### State 4: Result Determined (Success)

##### 4a. HS Code Block
```
┌──────────────────────────────────────────────────────────┐
│ KODE HS REKOMENDASI                                      │
│                                                           │
│           8429.52.00                                     │
│                                                           │
│ Mesin dengan superstructure berputar 360° — Shovel      │
│ loader dan backhoe loader                                │
│                                                           │
│ [✓ TINGGI] (green badge)                                │
└──────────────────────────────────────────────────────────┘
```

**Confidence Badges:**
- `TINGGI`: green bg `#dcfce7`, green text `#166534`
- `SEDANG`: yellow bg `#fef9c3`, yellow text `#854d0e`
- `RENDAH`: red bg `#fee2e2`, red text `#991b1b`

##### 4b. Tarif & Pajak Impor
```
💰 Tarif & Pajak

┌────────────────┬────────────────┐
│ Bea Masuk MFN  │  FTA ASEAN     │
│ 0%             │  0% (ref)      │
├────────────────┼────────────────┤
│ PPN Impor      │  PPh 22 Impor  │
│ 11%            │  7.5%          │
└────────────────┴────────────────┘

⚠️ Disclaimer:
Tarif FTA tidak menjamin eligibilitas. Bergantung pada Rules of Origin, 
Certificate of Origin, dan persyaratan prosedural. PPN & PPh 22 adalah 
tarif standar BTKI — fasilitas khusus PMK tidak dicakup.
```

##### 4c. Reasoning KUM HS
```
⚖️ Reasoning KUM HS

▼ [KUM HS 1] 
  Barang adalah mesin penggali self-propelled dengan superstructure 
  berputar 360°. Pos 84.29 menggambarkan barang secara langsung.
  
  Source: btki:2022:8429 | chapter_note:84:5

▼ [KUM HS 6]
  Subpos 8429.52 dipilih karena superstructure berputar 360° (bukan 
  8429.51 front-end shovel). Kode nasional .00 karena tidak ada 
  pembedaan CKD/non-CKD untuk subpos ini.
  
  Source: btki:2022:8429.52.00
```

**Interactions:**
- Each reasoning step is collapsible (accordion)
- Source IDs are clickable (opens modal with original BTKI text)

##### 4d. Kandidat Dieliminasi
```
❌ Kandidat yang Dieliminasi

┌────────────────────────────────────────────────────────┐
│ 84.30                                                  │
│ Pos 84.30 untuk moving, grading, levelling tanpa      │
│ drive mechanism untuk attachment. Excavator punya      │
│ drive mechanism hidrolik sendiri.                      │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│ 8429.59                                                │
│ Subpos residual — tidak digunakan karena 8429.52      │
│ lebih spesifik (KUM HS 3a).                           │
└────────────────────────────────────────────────────────┘
```

##### 4e. Action Buttons
```
[Export PDF]  [Lihat Audit Trail]  [Minta Expert Review]
```

**Minta Expert Review Button:**
- Opens modal:
  ```
  Alasan meminta review ahli: (required)
  [textarea]
  
  [Batal]  [Kirim ke Review]
  ```
- Setelah submit: case masuk antrian expert, tampilkan success toast

#### State 5: Need Expert Review
```
┌──────────────────────────────────────────────────────────┐
│ 👨‍⚖️ Perlu Expert Review                                  │
│                                                           │
│ Kasus ini tidak dapat diselesaikan otomatis:            │
│ • Tidak ditemukan pos yang cocok setelah pencarian       │
│ • KUM HS 4 (barang paling mirip) diperlukan             │
│                                                           │
│ Case Anda telah dikirim ke antrian Expert Reviewer.     │
│ Anda bisa memantau statusnya di menu Riwayat.           │
│                                                           │
│ Kandidat untuk ditinjau:                                 │
│ • 8429.5 — Sekop mekanik, ekskavator                    │
│ • 8430.4 — Mesin pemecah batu                           │
│                                                           │
│                            [Lihat di Riwayat]            │
└──────────────────────────────────────────────────────────┘
```

#### State 6: AI Suggestion (Special Case)

Jika nama barang tidak dikenal dan AI membantu terjemahan:

```
⚖️ Reasoning KUM HS

▼ [AI_SUGGESTION]
  Nama "Handphone Samsung Galaxy" tidak ditemukan di database BTKI.
  AI menyarankan istilah standar: "telepon untuk jaringan seluler".
  
  Usulan pos: 8517
  Verifikasi: Pos 8517 ada di BTKI 2022 ✓
  
  Source: groq:llama-3.3-70b | btki:2022:8517

▼ [KUM HS 1]
  ...
```

**Note:** AI_SUGGESTION step diberi border kuning tipis untuk membedakan dari step reguler.

---

## 5. Screen 2: Riwayat

**URL:** `/history`  
**Layout:** Master-Detail (List kiri 40%, Detail kanan 60%)

### 5.1 Stats Bar (Top)
```
┌────────────┬────────────┬────────────┬────────────┐
│    247     │    94%     │     14     │     3      │
│   Total    │ Confidence │   Perlu    │   Versi    │
│Klasifikasi │   TINGGI   │  Review    │   BTKI     │
└────────────┴────────────┴────────────┴────────────┘
```

### 5.2 Filter & Search
```
┌─────────────────────────────────────────────────────────┐
│ 🔍 [Cari nama barang atau kode HS...]                   │
│                                                          │
│ Status: [Semua ▼]  Tanggal: [30 hari terakhir ▼]      │
│                                                          │
│                              [Export Excel]             │
└─────────────────────────────────────────────────────────┘
```

### 5.3 List Cases (Kiri)
```
┌──────────────────────────────────────────────┐
│ 03 Sep 2026  14:32                          │
│ Komatsu PC210LC-11 Excavator                │
│ [8429.52.00]  [✓ TINGGI]                   │
│ Disetujui ahli                              │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│ 02 Sep 2026  09:15                          │
│ MacBook Air M2                              │
│ [8471.30.20]  [✓ TINGGI]                   │
│                                              │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│ 01 Sep 2026  16:48                          │
│ Motor listrik 3-phase industri              │
│ [8501.52.00]  [! SEDANG]                   │
│ Menunggu Expert Review                      │
└──────────────────────────────────────────────┘
```

**Indicators:**
- Badge berwarna untuk confidence
- Label status: "Ditentukan", "Disetujui ahli", "Dikoreksi ahli", "Menunggu Expert Review", "Ditolak ahli"
- Active case: blue left border + slightly darker bg

### 5.4 Detail Panel (Kanan)

Ketika case dipilih, tampilkan:

#### Header
```
🔍 Komatsu PC210LC-11 Excavator

Case ID: CLS-2026-09-03-00247
Tanggal: 03 Sep 2026 14:32:18
User: Randi Anggoro (Classifier)
Status: Ditentukan · Disetujui ahli
```

#### Tabs
```
[Hasil] [Fakta Barang] [Reasoning] [Tarif] [Keputusan Ahli] [Audit Trail]
```

**Tab: Hasil**
- Sama dengan Result card di Screen 1

**Tab: Fakta Barang**
- Display all extracted facts in read-only form (same structure as input form)

**Tab: Reasoning**
- Full reasoning chain with expand/collapse
- Show all KUM HS steps, AI suggestions (if any), eliminated candidates

**Tab: Tarif**
- Display all tariff info (MFN, FTA, PPN, PPh 22) with effective dates

**Tab: Keputusan Ahli**
- Jika ada expert decision, tampilkan:
  ```
  🧑‍⚖️ Disetujui oleh Dr. Sari Mulyani (Expert Reviewer)
  Tanggal: 03 Sep 2026 15:10:45
  
  Kode sebelum: 8429.52.00 (dari kandidat sistem)
  Kode final: 8429.52.00
  
  Alasan:
  Kandidat sistem sudah tepat. Barang memenuhi kriteria 
  superstructure 360° dan tidak termasuk front-end shovel.
  
  Tipe Keputusan: EXPERT_APPROVED
  ```

- Jika override:
  ```
  🧑‍⚖️ Dikoreksi oleh Dr. Sari Mulyani (Expert Reviewer)
  Tanggal: 03 Sep 2026 15:10:45
  
  Kode dari kandidat sistem: 8429.51.00
  Kode final (override): 8429.52.00
  
  Alasan:
  Spesifikasi barang menunjukkan superstructure berputar 360°, 
  bukan front-end shovel terbatas. Sistem salah memilih kandidat 
  awal karena fakta kurang lengkap.
  
  Tipe Keputusan: EXPERT_OVERRIDE
  ```

**Tab: Audit Trail**
- Timeline of all events:
  ```
  📝 03 Sep 2026 14:32:18 — Case dibuat oleh Randi Anggoro
  🔍 03 Sep 2026 14:32:20 — Pencarian kandidat: 3 pos ditemukan
  ⚖️ 03 Sep 2026 14:32:22 — KUM HS 1 diterapkan: 2 pos retained
  ❓ 03 Sep 2026 14:32:25 — Pertanyaan subpos diajukan
  ✅ 03 Sep 2026 14:35:10 — User menjawab: 8429.52 (superstructure 360°)
  ✓ 03 Sep 2026 14:35:12 — Validasi: kode 8429.52.00 lolos
  💰 03 Sep 2026 14:35:14 — Tarif dihitung
  📊 03 Sep 2026 14:35:15 — Hasil ditampilkan (confidence: TINGGI)
  👤 03 Sep 2026 14:40:30 — User meminta Expert Review
  🧑‍⚖️ 03 Sep 2026 15:10:45 — Expert menyetujui (Dr. Sari Mulyani)
  ```

---

## 6. Screen 3: Review Ahli (Expert Only)

**URL:** `/expert-review`  
**Access:** Expert Reviewer, Admin only

### 6.1 Queue View (Default)

#### Stats Bar
```
┌────────────┬────────────┬────────────┬────────────┐
│    14      │     8      │     3      │     3      │
│  Pending   │ Hari Ini   │ Usia >3hr  │  Urgent    │
└────────────┴────────────┴────────────┴────────────┘
```

#### Filter & Sort
```
Status: [Pending ▼]  Prioritas: [Urgent first ▼]  User: [Semua ▼]
```

#### Queue List
```
┌──────────────────────────────────────────────────────────┐
│ 🔴 URGENT · 03 Sep 2026 11:20 (4 jam lalu)              │
│                                                           │
│ Motor listrik 3-phase industri                           │
│ User: Budi Santoso (Classifier)                          │
│ Alasan: KUM HS 4 dipakai, berisiko tinggi                │
│                                                           │
│ Kandidat: 8501.52, 8501.53                              │
│                                                [Buka]     │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ 02 Sep 2026 16:30 (1 hari lalu)                         │
│                                                           │
│ Crane Barge Majulah                                      │
│ User: Randi Anggoro (Classifier)                         │
│ Alasan: Tidak yakin antara kapal vs mesin crane          │
│                                                           │
│ Kandidat: 8426.11, 8905.90                              │
│                                                [Buka]     │
└──────────────────────────────────────────────────────────┘
```

### 6.2 Review Detail View

Setelah klik [Buka], tampilkan full-page modal atau navigate ke `/expert-review/:caseId`:

#### Header
```
🔍 Motor listrik 3-phase industri

Case ID: CLS-2026-09-03-00125
User: Budi Santoso (Classifier)
Tanggal Submit: 03 Sep 2026 11:20:18
Prioritas: [🔴 URGENT]
```

#### Layout: 2 Kolom

**Kolom Kiri (60%): Informasi Case**

Tabs:
```
[Fakta Barang] [Reasoning Sistem] [Kandidat]
```

**Tab: Fakta Barang**
- Read-only form, sama seperti di Riwayat

**Tab: Reasoning Sistem**
- Full reasoning chain sampai titik sistem tidak bisa lanjut
- Highlighted step yang menyebabkan review:
  ```
  ⚠️ [KUM HS 4 — MOST AKIN]
  Barang tidak dapat diklasifikasikan dengan KUM HS 1-3.
  
  Sistem membandingkan barang dengan:
  • 8501.52 — Motor AC multi-phase 750W-75kW
  • 8501.53 — Motor AC multi-phase >75kW
  
  Fakta kurang: output daya motor tidak disebutkan user.
  
  ⚠️ CASE ESCALATED TO EXPERT REVIEW
  ```

**Tab: Kandidat**
- List kandidat dari sistem dengan score/reason:
  ```
  ┌─────────────────────────────────────────────────────┐
  │ 8501.52.00                                         │
  │ Motor listrik AC multi-phase 750W–75kW             │
  │                                                     │
  │ Reason: Fakta "multi-phase" cocok, tapi daya       │
  │         tidak diketahui apakah <75kW              │
  │                                                     │
  │ Source: btki:2022:8501.52.00                       │
  └─────────────────────────────────────────────────────┘
  
  ┌─────────────────────────────────────────────────────┐
  │ 8501.53.00                                         │
  │ Motor listrik AC multi-phase >75kW                 │
  │                                                     │
  │ Reason: Sama seperti di atas                       │
  │                                                     │
  │ Source: btki:2022:8501.53.00                       │
  └─────────────────────────────────────────────────────┘
  ```

**Kolom Kanan (40%): Panel Keputusan Expert**

```
┌───────────────────────────────────────────────────────┐
│ KEPUTUSAN EXPERT                                      │
│                                                        │
│ Pilih dari kandidat sistem:                           │
│ ( ) 8501.52.00 — Motor AC multi-phase 750W–75kW      │
│ ( ) 8501.53.00 — Motor AC multi-phase >75kW          │
│                                                        │
│ ATAU                                                   │
│                                                        │
│ Masukkan kode lain:                                   │
│ [____.__.__]  [Cek di BTKI]                          │
│                                                        │
│ (setelah klik Cek di BTKI, tampilkan uraian resmi)   │
│                                                        │
│ ┌─────────────────────────────────────────────────┐  │
│ │ 8501.52.00                                      │  │
│ │ Motor listrik AC multi-phase 750W–75kW          │  │
│ │                                                  │  │
│ │ Status: ✓ Ada di BTKI 2022 (Aktif)            │  │
│ └─────────────────────────────────────────────────┘  │
│                                                        │
│ Alasan keputusan: (wajib)                             │
│ [textarea]                                             │
│                                                        │
│ [Tolak Case]        [Setujui Kode]                   │
└───────────────────────────────────────────────────────┘
```

**Validasi:**
- Alasan wajib diisi (min 10 karakter)
- Jika input kode manual, harus klik "Cek di BTKI" dulu sebelum bisa Setujui
- Jika kode tidak ditemukan di BTKI aktif, tombol Setujui disabled + tampilkan error

**Action Buttons:**

**[Tolak Case]**
- Opens confirmation modal:
  ```
  Yakin menolak case ini?
  
  Alasan penolakan akan dikirim ke user.
  User perlu memperbaiki data barang dan submit ulang.
  
  [Batal]  [Tolak]
  ```
- Setelah tolak: status case jadi "Ditolak ahli", user dapat notif

**[Setujui Kode]**
- Validation checks:
  - Kode dipilih atau diinput?
  - Kode ada di BTKI aktif?
  - Alasan diisi?
- Jika lolos: status case jadi "Ditentukan", label "Disetujui ahli" atau "Dikoreksi ahli (override)" tergantung kode dari kandidat atau manual
- Tarif dihitung otomatis
- User dapat notif

---

## 7. Screen 4: Admin Data Center

**URL:** `/admin`  
**Access:** Admin only  
**Layout:** Sidebar kiri + Content kanan

### 7.1 Sidebar Menu
```
┌────────────────────────┐
│ 📋 Versi Nomenklatur   │ (active)
│ 📤 Upload DOCX         │
│ 📄 Template CSV/Excel  │
│ ⚠️ Antrian Ambiguitas  │
│ 📊 Laporan Validasi    │
│ 🔍 Browse BTKI         │
└────────────────────────┘
```

### 7.2 Tab: Versi Nomenklatur

#### Stats Bar
```
┌────────────┬────────────┬────────────┬────────────┐
│     3      │     1      │  14,273    │    247     │
│  Total     │   Aktif    │  Tariff    │ Cases      │
│  Versi     │            │  Lines     │ Bulan Ini  │
└────────────┴────────────┴────────────┴────────────┘
```

#### Action
```
[+ Tambah Versi Baru]
```

Klik → opens modal:
```
┌──────────────────────────────────────────────────────┐
│ Tambah Versi Baru                                    │
│                                                       │
│ Nama Versi: *                                        │
│ [BTKI 2022]                                          │
│                                                       │
│ Edisi HS: *                                          │
│ [2022]                                               │
│                                                       │
│ Edisi AHTN: *                                        │
│ [2022]                                               │
│                                                       │
│ Nomor Regulasi: *                                    │
│ [PMK 26/2022]                                        │
│                                                       │
│ Tanggal Berlaku: *                                   │
│ Dari: [2022-01-01]  Sampai: [2027-12-31]           │
│                                                       │
│ [Batal]                         [Buat Versi]         │
└──────────────────────────────────────────────────────┘
```

#### Version List
```
┌──────────────────────────────────────────────────────────┐
│ BTKI 2022                               [✓ AKTIF]        │
│ PMK 26/2022 · Berlaku: 01 Jan 2022 - 31 Des 2027        │
│ Total: 14,273 tariff lines · Uploaded: 15 Jan 2022      │
│ State: INDEXED                                           │
│                                                           │
│ [Lihat Detail] [Nonaktifkan] [Export CSV]               │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ BTKI 2017                               [INACTIVE]       │
│ PMK 6/2017 · Berlaku: 01 Jan 2017 - 31 Des 2021         │
│ Total: 11,084 tariff lines · Uploaded: 10 Feb 2017      │
│ State: COMMITTED                                         │
│                                                           │
│ [Lihat Detail] [Aktifkan Kembali] [Export CSV]          │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ BTKI 2024 Draft                      [VALIDATING]        │
│ PMK xx/2024 · Berlaku: 01 Jan 2024 - (belum diset)      │
│ Total: 0 tariff lines (belum commit)                     │
│ State: VALIDATING                                        │
│ Issues: 3 errors, 12 warnings, 45 ambiguities           │
│                                                           │
│ [Lanjutkan Validasi] [Hapus Draft]                      │
└──────────────────────────────────────────────────────────┘
```

**State Machine Visual:**
```
UPLOADED → PARSING → MAPPED → VALIDATING → DRY_RUN_PASSED
                                          ↓
                                    DRY_RUN_FAILED → (fix & retry)
                                          ↓
                                  READY_TO_COMMIT
                                          ↓
                                      COMMITTED
                                          ↓
                                       INDEXED
                                          ↓
                                       ACTIVE
```

### 7.3 Tab: Upload DOCX

```
┌──────────────────────────────────────────────────────────┐
│ Upload BTKI dari File DOCX                               │
│                                                           │
│ Pilih Versi Target: *                                    │
│ [BTKI 2024 Draft ▼]                                     │
│                                                           │
│ Upload File DOCX:                                        │
│ ┌──────────────────────────────────────────────────┐    │
│ │  📤 Drag & drop file .docx di sini               │    │
│ │     atau klik untuk browse                        │    │
│ │                                                   │    │
│ │  Format: .docx (Word 2010+)                      │    │
│ │  Ukuran maks: 50 MB                              │    │
│ └──────────────────────────────────────────────────┘    │
│                                                           │
│ File terpilih: BTKI_2024_full.docx (12.4 MB)            │
│                                                           │
│                           [Parse & Dry Run]              │
└──────────────────────────────────────────────────────────┘
```

**Setelah Parse & Dry Run:**

```
✓ Parsing selesai dalam 45 detik

┌──────────────────────────────────────────────────────────┐
│ Laporan Dry Run                                          │
│                                                           │
│ ✓ Total kode ditemukan: 14,273                          │
│ ✓ Struktur hierarki: Valid                              │
│ ⚠️ Errors: 3 (BLOCKING)                                  │
│ ⚠️ Warnings: 12                                          │
│ ⚠️ Ambiguitas: 45 (perlu approval admin)                │
│                                                           │
│ [Lihat Detail Laporan] [Lihat Antrian Ambiguitas]      │
│                                                           │
│ State: DRY_RUN_FAILED (ada errors)                       │
│                                                           │
│ ❌ Commit diblokir. Perbaiki errors terlebih dahulu.     │
└──────────────────────────────────────────────────────────┘
```

**Setelah errors diperbaiki dan DRY_RUN_PASSED:**

```
✓ Validasi lolos

┌──────────────────────────────────────────────────────────┐
│ Siap untuk Commit                                        │
│                                                           │
│ File: BTKI_2024_full.docx                                │
│ Checksum: a3f8c2d1e4b7...                                │
│ Total kode: 14,273                                       │
│ Ambiguitas tersisa: 0 (semua sudah di-approve)          │
│                                                           │
│ ⚠️ Commit akan menulis 14,273 records ke database.       │
│    Proses tidak bisa di-undo.                            │
│                                                           │
│ [Batal]                      [Commit ke Database]        │
└──────────────────────────────────────────────────────────┘
```

### 7.4 Tab: Template CSV/Excel

```
┌──────────────────────────────────────────────────────────┐
│ Import Data via Template                                 │
│                                                           │
│ Gunakan template ini untuk:                              │
│ • Koreksi data manual                                    │
│ • Penambahan baris tanpa DOCX                           │
│ • Update tarif/deskripsi                                 │
│                                                           │
│ [Download Template CSV] [Download Template Excel]        │
│                                                           │
│ ───────────────────────────────────────────────────────  │
│                                                           │
│ Upload Template yang Sudah Diisi:                        │
│ ┌──────────────────────────────────────────────────┐    │
│ │  📤 Drag & drop file CSV/Excel di sini           │    │
│ └──────────────────────────────────────────────────┘    │
│                                                           │
│ File terpilih: btki_corrections.xlsx (240 KB)            │
│                                                           │
│ Pilih Versi Target: *                                    │
│ [BTKI 2022 (Aktif) ▼]                                   │
│                                                           │
│                              [Validasi Template]         │
└──────────────────────────────────────────────────────────┘
```

**Setelah Validasi:**

```
✓ Validasi selesai

┌──────────────────────────────────────────────────────────┐
│ Hasil Validasi Template                                  │
│                                                           │
│ Total baris: 120                                         │
│ ✓ Valid: 115 baris                                      │
│ ⚠️ Warnings: 3 baris (bisa di-commit)                   │
│ ❌ Errors: 2 baris (harus diperbaiki)                    │
│                                                           │
│ [Lihat Detail Errors]                                    │
│                                                           │
│ Errors:                                                  │
│ • Baris 47: Invalid parent code (8471.99 tidak ada)     │
│ • Baris 82: Duplicate code (8517.13.00 sudah ada)       │
│                                                           │
│ ❌ Commit diblokir. Perbaiki errors di file Excel.       │
└──────────────────────────────────────────────────────────┘
```

**Setelah errors diperbaiki:**

```
✓ Validasi lolos

┌──────────────────────────────────────────────────────────┐
│ Preview Perubahan                                        │
│                                                           │
│ INSERT: 45 baris baru                                    │
│ UPDATE: 70 baris existing                                │
│ SKIP: 5 baris (tidak ada perubahan)                     │
│                                                           │
│ [Lihat Diff Detail]                                      │
│                                                           │
│ ☑ Saya sudah mereview diff dan yakin untuk commit       │
│                                                           │
│ [Batal]                              [Commit Changes]    │
└──────────────────────────────────────────────────────────┘
```

### 7.5 Tab: Antrian Ambiguitas

List semua kode yang struktur induknya tidak jelas dari parsing DOCX:

```
┌──────────────────────────────────────────────────────────┐
│ Filter: [Belum diputuskan ▼]  [Cari kode...]            │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ 8471.30.20                                               │
│ Laptop termasuk notebook dan subnotebook                 │
│                                                           │
│ Issue: Indentasi tidak konsisten, parent bisa:          │
│ • 8471.30 (dari indentasi)                              │
│ • 8471.90 (dari numbering)                              │
│                                                           │
│ Source: btki_2024_full.docx:paragraph_1847               │
│                                                           │
│ Pilih parent yang benar:                                 │
│ ( ) 8471.30 — Mesin pengolah data portabel              │
│ ( ) 8471.90 — Lainnya                                    │
│                                                           │
│ Catatan (opsional):                                      │
│ [Indentasi lebih masuk akal, numbering salah ketik]     │
│                                                           │
│                         [Reject] [Approve]               │
└──────────────────────────────────────────────────────────┘
```

**Actions:**
- **Reject**: Kode ini tidak akan masuk database
- **Approve**: Kode masuk database dengan parent yang dipilih admin

### 7.6 Tab: Laporan Validasi

List semua issues dari dry run terakhir:

```
┌──────────────────────────────────────────────────────────┐
│ Versi: [BTKI 2024 Draft ▼]                              │
│ Dry Run: 03 Sep 2026 14:52:30                           │
│                                                           │
│ Filter: [Errors ▼]                                       │
└──────────────────────────────────────────────────────────┘

Stats:
┌────────────┬────────────┬────────────┬────────────┐
│     3      │    12      │    45      │  14,273    │
│  Errors    │  Warnings  │ Ambiguities│  Total OK  │
└────────────┴────────────┴────────────┴────────────┘

Issue List:
┌──────────────────────────────────────────────────────────┐
│ [ERROR] DUPLICATE_CODE                                   │
│ Code: 8471.30.20                                         │
│ Message: Kode ini muncul 2 kali dalam file               │
│ Location: paragraph_1847, paragraph_2103                 │
│                                                           │
│ [Lihat di Source]                                        │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ [ERROR] MISSING_PARENT                                   │
│ Code: 8517.13.00                                         │
│ Message: Parent 8517.13 tidak ditemukan                 │
│ Location: paragraph_2450                                 │
│                                                           │
│ [Lihat di Source]                                        │
└──────────────────────────────────────────────────────────┘
```

### 7.7 Tab: Browse BTKI

Search & browse active BTKI database:

```
┌──────────────────────────────────────────────────────────┐
│ 🔍 [Cari kode atau deskripsi...]                        │
│                                                           │
│ Versi: [BTKI 2022 (Aktif) ▼]                           │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ 8471.30.20                                               │
│ Laptop termasuk notebook dan subnotebook                 │
│                                                           │
│ Hierarki:                                                │
│ Section XVI → Chapter 84 → Heading 8471 → 8471.30       │
│                                                           │
│ Tarif:                                                   │
│ • MFN: 0%                                               │
│ • FTA ASEAN: 0%                                         │
│ • PPN: 11%                                              │
│ • PPh 22: 7.5%                                          │
│                                                           │
│ Effective: 01 Jan 2022 - 31 Des 2027                   │
│ Status: ACTIVE                                           │
│                                                           │
│ [Edit Tarif] [View History] [Deactivate]               │
└──────────────────────────────────────────────────────────┘
```

---

## 8. Komponen UI Reusable

### 8.1 Confidence Badge
```tsx
// Props: level: "TINGGI" | "SEDANG" | "RENDAH"
<Badge variant={level === "TINGGI" ? "success" : level === "SEDANG" ? "warning" : "error"}>
  {level === "TINGGI" ? "✓" : level === "SEDANG" ? "!" : "✗"} {level}
</Badge>
```

### 8.2 HS Code Display
```tsx
<div className="hs-code-block">
  <div className="text-xs font-semibold text-blue-700 mb-1">KODE HS REKOMENDASI</div>
  <div className="text-4xl font-extrabold text-blue-900 tracking-wider font-mono">
    {code} {/* e.g. 8429.52.00 */}
  </div>
  <div className="text-sm text-blue-800 mt-1">
    {description}
  </div>
  <ConfidenceBadge level={confidence} className="mt-2" />
</div>
```

### 8.3 Reasoning Step (Accordion)
```tsx
<Accordion>
  <AccordionItem>
    <AccordionTrigger>
      <span className="font-semibold">[KUM HS 1]</span>
    </AccordionTrigger>
    <AccordionContent>
      <p className="text-sm text-slate-700">
        {reasoningText}
      </p>
      <div className="text-xs text-slate-500 mt-2">
        Source: <SourceLink id={sourceId} />
      </div>
    </AccordionContent>
  </AccordionItem>
</Accordion>
```

### 8.4 Tariff Grid
```tsx
<div className="grid grid-cols-2 gap-3">
  <TariffCard label="Bea Masuk MFN" value="0%" />
  <TariffCard label="FTA ASEAN" value="0%" note="referensi" />
  <TariffCard label="PPN Impor" value="11%" />
  <TariffCard label="PPh 22 Impor" value="7.5%" />
</div>
<TariffDisclaimer />
```

### 8.5 Question Banner (Subheading Selection)
```tsx
<div className="bg-yellow-50 border-2 border-yellow-300 rounded-lg p-4">
  <div className="font-semibold mb-2">
    Pertanyaan {questionNumber} — Pos {headingCode} ({headingDescription})
  </div>
  <p className="text-sm text-slate-600 mb-3">
    Pilih subpos yang paling sesuai dengan barang Anda:
  </p>
  <RadioGroup>
    {options.map(opt => (
      <RadioItem key={opt.code} value={opt.code}>
        {opt.code} — {opt.description}
      </RadioItem>
    ))}
    <RadioItem value="unsure">
      Tidak ada yang sesuai / tidak yakin
    </RadioItem>
  </RadioGroup>
  <Button className="mt-3">Lanjutkan</Button>
</div>
```

### 8.6 Expert Decision Panel
```tsx
<Card>
  <CardHeader>KEPUTUSAN EXPERT</CardHeader>
  <CardContent>
    <div className="space-y-3">
      <div>
        <Label>Pilih dari kandidat sistem:</Label>
        <RadioGroup>
          {candidates.map(c => (
            <RadioItem key={c.code} value={c.code}>
              {c.code} — {c.description}
            </RadioItem>
          ))}
        </RadioGroup>
      </div>
      
      <Separator />
      
      <div>
        <Label>ATAU masukkan kode lain:</Label>
        <div className="flex gap-2">
          <Input placeholder="____.__.__" />
          <Button variant="outline">Cek di BTKI</Button>
        </div>
        {checkedCode && (
          <div className="mt-2 p-2 bg-blue-50 rounded">
            {checkedCode.description}
          </div>
        )}
      </div>
      
      <div>
        <Label>Alasan keputusan: (wajib)</Label>
        <Textarea rows={4} required />
      </div>
      
      <div className="flex gap-2">
        <Button variant="destructive">Tolak Case</Button>
        <Button variant="default">Setujui Kode</Button>
      </div>
    </div>
  </CardContent>
</Card>
```

---

## 9. State Management & API Integration

### 9.1 API Endpoints (Reference)

**Classification Flow:**
```
POST /api/classification/start
Body: { facts: {...} }
Response: { caseId, candidateHeadings, questions?, result? }

POST /api/classification/{caseId}/answer
Body: { questionId, selectedCode }
Response: { nextQuestion?, result? }

POST /api/classification/{caseId}/request-review
Body: { reason }
Response: { success, queuePosition }
```

**Expert Review:**
```
GET /api/expert/queue
Response: { cases: [...] }

GET /api/expert/case/{caseId}
Response: { case, facts, reasoning, candidates }

POST /api/expert/case/{caseId}/decision
Body: { selectedCode, reason, action: "approve" | "reject" }
Response: { success, updatedCase }
```

**Admin:**
```
GET /api/admin/versions
Response: { versions: [...] }

POST /api/admin/version/create
Body: { name, edition, regulation, effectiveDates }

POST /api/admin/docx/parse
Body: FormData with .docx file
Response: { parseResult, validationReport, ambiguities }

POST /api/admin/docx/commit
Body: { versionId, fileChecksum }
Response: { success, recordsInserted }

GET /api/admin/ambiguities/{versionId}
Response: { ambiguities: [...] }

POST /api/admin/ambiguity/{ambiguityId}/resolve
Body: { selectedParent, note, action: "approve" | "reject" }
```

**History:**
```
GET /api/history/cases
Query: ?status=&dateFrom=&dateTo=&search=
Response: { cases: [...] }

GET /api/history/case/{caseId}
Response: { case, facts, reasoning, tariff, expertDecisions, auditTrail }
```

### 9.2 Client State Structure (Reference)

```tsx
// Zustand or Context
interface ClassificationState {
  currentCase: Case | null;
  facts: Facts;
  pendingQuestion: Question | null;
  result: ClassificationResult | null;
  loading: boolean;
  error: string | null;
}

interface Case {
  id: string;
  userId: string;
  createdAt: string;
  status: "draft" | "determined" | "need_clarification" | "expert_review" | "approved" | "rejected";
  confidence: "TINGGI" | "SEDANG" | "RENDAH";
  finalCode?: string;
  finalDescription?: string;
}

interface Question {
  id: string;
  questionNumber: number;
  headingCode: string;
  headingDescription: string;
  options: Array<{
    code: string;
    description: string;
  }>;
}

interface ClassificationResult {
  code: string;
  description: string;
  confidence: "TINGGI" | "SEDANG" | "RENDAH";
  tariff: {
    mfn: string;
    fta: Array<{ scheme: string; rate: string }>;
    ppn: string;
    pph22: string;
  };
  reasoning: Array<ReasoningStep>;
  eliminatedCandidates: Array<EliminatedCandidate>;
}
```

---

## 10. Responsive Behavior

### 10.1 Breakpoints
- `sm`: 640px (mobile)
- `md`: 768px (tablet)
- `lg`: 1024px (desktop)
- `xl`: 1280px (large desktop)

### 10.2 Mobile Adaptations

**Screen 1 (Klasifikasi):**
- 2 kolom jadi 1 kolom stack (form di atas, result di bawah)
- Result auto-scroll ke view setelah analisis selesai
- Chips jadi full-width buttons dengan text wrap

**Screen 2 (Riwayat):**
- Master-detail jadi tabs: [List] [Detail]
- Filter & search collapse ke drawer

**Screen 3 (Expert Review):**
- 2 kolom jadi tabs: [Informasi Case] [Keputusan]

**Screen 4 (Admin):**
- Sidebar collapse jadi hamburger menu
- Tables jadi cards dengan horizontal scroll

**Header:**
- Menu collapse jadi hamburger
- User avatar + dropdown tetap visible

---

## 11. Accessibility & UX Notes

**Keyboard Navigation:**
- Form fields: Tab untuk navigasi, Enter untuk submit
- Reasoning accordion: Space/Enter untuk expand
- Modal: Esc untuk close
- Radio groups: Arrow keys untuk navigate options

**Screen Reader:**
- Semua form labels harus explicit
- Confidence badges dengan sr-only text: "Tingkat keyakinan tinggi"
- Loading states dengan aria-live announcements

**Error Handling:**
- Form validation: inline error messages, red border
- API errors: toast notifications + retry button
- Network offline: banner persistent di atas dengan "Koneksi terputus"

**Loading States:**
- Skeleton screens untuk list views
- Spinner + text untuk actions (submit, parse, commit)
- Progress bar untuk file uploads

**Empty States:**
- Riwayat kosong: "Belum ada klasifikasi. Mulai dari menu Klasifikasi."
- Antrian expert kosong: "Semua case sudah ditinjau. Bagus!"
- Search no results: "Tidak ditemukan. Coba kata kunci lain."

---

## 12. Wireframe Visual Reference

(Mengacu pada `ui_wireframe.html` yang sudah dibuat sebelumnya)

**Key Screens:**
1. **Klasifikasi**: 2 kolom, form + result, pertanyaan subpos inline
2. **Riwayat**: master-detail, tabs untuk detail view
3. **Expert Review**: queue list + full modal untuk review
4. **Admin**: sidebar + multi-tab content area

**Color Usage:**
- Blue: primary actions, HS codes, links
- Green: success, confidence tinggi, approved
- Yellow: warnings, klarifikasi, ambiguities
- Red: errors, confidence rendah, rejected, urgent
- Gray: secondary text, disabled states

---

## 13. Checklist untuk Frontend Generator

Setelah generate, pastikan:

- [ ] Login flow: email/password → redirect ke `/classify`
- [ ] Role-based menu rendering (Classifier vs Expert vs Admin)
- [ ] Form validation: required fields, format checks
- [ ] Analisis button: loading state, error handling
- [ ] Pertanyaan subpos: radio selection, lanjutkan button
- [ ] Result display: HS code, tarif, reasoning accordion, eliminated candidates
- [ ] Minta Expert Review: modal dengan alasan (required)
- [ ] Riwayat: list + detail, filter & search functional
- [ ] Expert Review: queue list, decision panel, approve/reject flow
- [ ] Admin: version list, upload DOCX, dry run report, ambiguity queue, commit flow
- [ ] Template CSV/Excel: download, upload, validate, preview diff, commit
- [ ] Mobile responsive: semua screen adapt ke 1 kolom
- [ ] Accessibility: keyboard nav, screen reader labels
- [ ] Error handling: API errors, network offline, validation errors
- [ ] Loading states: skeleton screens, spinners, progress bars
- [ ] Empty states: friendly messages dengan CTA

---

## 14. Contoh Flow User (End-to-End)

### Flow A: Klasifikasi Langsung (Tanpa Pertanyaan)

1. User login sebagai Classifier
2. Navigate ke menu Klasifikasi
3. Isi form:
   - Nama generik: `Laptop`
   - Fungsi: `Mengolah data / Komputasi`
   - Material: `Aluminium`
   - Kondisi: `Lengkap dan jadi`
   - Spesifikasi: `Layar 14 inci, berat 1.5 kg`
4. Klik **Analisis HS Code**
5. Loading 3 detik
6. Result langsung muncul: `8471.30.20`, confidence `TINGGI`, reasoning KUM HS 1 + 6, tarif lengkap
7. User klik **Export PDF** → download PDF

### Flow B: Klasifikasi dengan Pertanyaan

1. User isi form:
   - Nama generik: `Excavator`
   - Fungsi: `Menggali/Earthmoving`
2. Klik **Analisis HS Code**
3. Loading 3 detik
4. Muncul pertanyaan 1 (banner kuning):
   - Pilih subpos dari pos 8429
   - User pilih: `8429.5 — Sekop mekanik, ekskavator`
   - Klik **Lanjutkan**
5. Muncul pertanyaan 2:
   - Pilih: `8429.52 — Mesin yang berputar 360°`
   - Klik **Lanjutkan**
6. Result muncul: `8429.52.00`, confidence `TINGGI`

### Flow C: Klasifikasi dengan Expert Review

1. User isi form:
   - Nama dagang: `Blorptron 9000`
   - Nama generik: `mesin khusus`
2. Klik **Analisis HS Code**
3. Loading 5 detik
4. Result: "Perlu Expert Review"
   - Kandidat: `8479.89`, `8543.70`
   - Notifikasi: "Case Anda telah dikirim ke Expert Reviewer"
5. User bisa pantau di Riwayat, status: `Menunggu Expert Review`

### Flow D: Expert Memutuskan Case

1. Expert login, navigate ke **Review Ahli**
2. Lihat queue, pilih case urgent
3. Klik **Buka**
4. Review fakta barang, reasoning sistem, kandidat
5. Pilih kandidat: `8479.89.00`
6. Isi alasan: "Mesin ini termasuk mesin khusus untuk industri, sesuai 8479.89"
7. Klik **Setujui Kode**
8. Case status jadi `Ditentukan`, label `Disetujui ahli`
9. User (Classifier) dapat notifikasi: "Case Anda telah disetujui oleh Expert"

### Flow E: Admin Upload BTKI Baru

1. Admin login, navigate ke **Admin Data Center**
2. Tab **Versi** → klik **Tambah Versi Baru**
3. Isi form: `BTKI 2024`, `PMK xx/2024`, tanggal berlaku
4. Tab **Upload DOCX** → pilih versi `BTKI 2024 Draft`
5. Upload file `BTKI_2024_full.docx`
6. Klik **Parse & Dry Run**
7. Loading 45 detik
8. Result: 3 errors, 12 warnings, 45 ambiguities
9. Tab **Antrian Ambiguitas** → review 45 kode, approve/reject satu per satu
10. Setelah semua ambiguitas resolved, kembali ke **Upload DOCX**
11. Dry run ulang → semua lolos
12. Klik **Commit ke Database**
13. Confirmation modal → klik **Ya, Commit**
14. Loading 2 menit → success
15. Tab **Versi** → klik **Aktifkan** pada versi `BTKI 2024`
16. Versi lama auto-nonaktif, versi baru jadi `ACTIVE`

---

## 15. File & Folder Structure (Suggestion)

```
frontend/
├── src/
│   ├── components/
│   │   ├── classification/
│   │   │   ├── FactForm.tsx
│   │   │   ├── ResultPanel.tsx
│   │   │   ├── QuestionBanner.tsx
│   │   │   ├── ReasoningAccordion.tsx
│   │   │   └── TariffGrid.tsx
│   │   ├── history/
│   │   │   ├── CaseList.tsx
│   │   │   ├── CaseDetail.tsx
│   │   │   └── AuditTimeline.tsx
│   │   ├── expert/
│   │   │   ├── ReviewQueue.tsx
│   │   │   ├── ReviewDetail.tsx
│   │   │   └── DecisionPanel.tsx
│   │   ├── admin/
│   │   │   ├── VersionList.tsx
│   │   │   ├── UploadDocx.tsx
│   │   │   ├── TemplateUpload.tsx
│   │   │   ├── AmbiguityQueue.tsx
│   │   │   └── ValidationReport.tsx
│   │   ├── ui/
│   │   │   ├── Badge.tsx
│   │   │   ├── Button.tsx
│   │   │   ├── Card.tsx
│   │   │   ├── Input.tsx
│   │   │   ├── Select.tsx
│   │   │   ├── Textarea.tsx
│   │   │   ├── RadioGroup.tsx
│   │   │   ├── Accordion.tsx
│   │   │   ├── Modal.tsx
│   │   │   └── Toast.tsx
│   │   ├── layout/
│   │   │   ├── Header.tsx
│   │   │   ├── Sidebar.tsx
│   │   │   └── Container.tsx
│   │   └── common/
│   │       ├── ConfidenceBadge.tsx
│   │       ├── HSCodeDisplay.tsx
│   │       ├── LoadingSpinner.tsx
│   │       └── EmptyState.tsx
│   ├── pages/
│   │   ├── Login.tsx
│   │   ├── Classify.tsx
│   │   ├── History.tsx
│   │   ├── ExpertReview.tsx
│   │   └── Admin.tsx
│   ├── hooks/
│   │   ├── useClassification.ts
│   │   ├── useHistory.ts
│   │   ├── useExpertReview.ts
│   │   └── useAdmin.ts
│   ├── services/
│   │   └── api.ts
│   ├── stores/
│   │   ├── authStore.ts
│   │   ├── classificationStore.ts
│   │   └── adminStore.ts
│   ├── types/
│   │   ├── case.ts
│   │   ├── fact.ts
│   │   ├── result.ts
│   │   └── admin.ts
│   ├── utils/
│   │   ├── validation.ts
│   │   └── format.ts
│   └── App.tsx
├── public/
├── package.json
└── tailwind.config.js
```

---

**Dokumen ini adalah sumber referensi lengkap untuk generate frontend UI sistem AI HS Classification Intelligence V2. Semua komponen, flow, state, dan interaksi sudah dijelaskan secara detail.**

**Good luck building! 🚀**
