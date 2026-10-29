# AI HS Classification Intelligence
## Deck Presentasi — End-to-End System Overview

**SMART Internal · BTKI 2022 · PMK 26/2022**  
**Status:** MVP Live · Versi 1.0

---

## 1. Gambaran Besar — Apa yang Dibangun

Sistem ini bukan mesin pencari kode HS. Sistem ini adalah **reasoning engine** yang meniru cara ahli kepabeanan mengklasifikasikan barang:

```
Fakta Barang  →  Karakterisasi Legal  →  Kandidat BTKI  →  Uji KUM HS  →  Rekomendasi
```

**Perbedaan mendasar vs pencarian biasa:**

| Pencarian Biasa | Sistem Ini |
|----------------|------------|
| Ketik nama → cari kata mirip | Isi fakta → AI reasoning hukum |
| Hasil = kode yang namanya mirip | Hasil = kode yang faktanya cocok |
| Tidak ada penjelasan | Ada reasoning KUM HS + eliminasi kandidat |
| Nama marketing menentukan | Fakta teknis yang menentukan |
| Tidak tahu kenapa | Explainable + auditable |

---

## 2. Alur Proses End-to-End

```
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 1: INPUT                                                     │
│                                                                     │
│  User mengisi form terstruktur:                                     │
│  • Identitas barang (nama dagang, nama generik)                     │
│  • Fisik & material (material utama, kondisi impor)                 │
│  • Fungsi & cara kerja (fungsi utama, prinsip operasi, mobilitas)  │
│  • Konteks penggunaan (area operasi, sektor industri)               │
│  • Karakteristik tambahan (part/set/komponen, spesifikasi teknis)   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 2: PROMPT BUILDER (classifier.js · buildPrompt())           │
│                                                                     │
│  Mengkonversi input form menjadi structured prompt dengan 3 bagian:│
│                                                                     │
│  [A] FAKTA BARANG                                                   │
│      Serialisasi semua field yang terisi → paragraf fakta teknis   │
│                                                                     │
│  [B] REFERENSI BTKI NASIONAL                                        │
│      105 subpos nasional digit 7-8 dari btki_national.json         │
│      di-inject sebagai teks referensi                               │
│                                                                     │
│  [C] ATURAN KUM HS 1–6                                              │
│      6 rules dari kumhs_rules.json, masing-masing dengan trigger   │
│      dan implikasi → AI tahu kapan tiap rule digunakan             │
└───────────────────────────┬─────────────────────────────────────────┘
                            │  ~2.300–3.500 tokens
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 3: AI REASONING ENGINE                                       │
│                                                                     │
│  Model: openai/gpt-oss-120b via Groq API                           │
│  Temperature: 0.1 (deterministik, bukan kreatif)                   │
│                                                                     │
│  AI melakukan:                                                      │
│  1. Baca fakta → identifikasi karakter legal barang                 │
│  2. Tentukan heading pos 4-digit via KUM HS 1                      │
│  3. Trigger KUM HS 2/3 jika kondisinya terpenuhi                   │
│  4. Pilih subpos 6-digit via KUM HS 6 (prinsip spesifisitas)       │
│  5. Tentukan digit nasional 7-8 dari referensi BTKI                │
│  6. Eliminasi kandidat yang tidak lolos dengan alasan               │
│  7. Tentukan confidence level dan flag expert review jika perlu    │
│                                                                     │
│  Output: JSON terstruktur                                           │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  LAYER 4: OUTPUT PANEL                                              │
│                                                                     │
│  • Kode HS 8-digit + uraian BTKI                                    │
│  • Confidence badge (TINGGI / SEDANG / RENDAH)                     │
│  • Reasoning steps per KUM HS yang digunakan                       │
│  • Daftar kandidat yang dieliminasi + alasan                        │
│  • Fakta yang masih perlu dikonfirmasi                              │
│  • Flag "Perlu Expert Review" jika triggered                        │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Basis Pengambilan Input — Dari Mana Setiap Field Berasal

### 3.1 Mengapa Form, Bukan Teks Bebas?

Input berbasis form/dropdown dipilih secara sengaja karena:
- **Kontrol kualitas fakta** — user tidak bisa isi dengan nama marketing atau deskripsi ambigu
- **Mengurangi hallucination** — AI tidak perlu menebak fakta, fakta sudah tersedia terstruktur
- **Konsistensi** — format yang sama untuk semua barang memudahkan audit

### 3.2 Basis Setiap Field

| Field | Dasar Pemilihan | Relevansi KUM HS |
|-------|----------------|-----------------|
| **Nama Generik** | Identitas teknis, bukan marketing | Dasar pencarian pos |
| **Material Utama** | Faktor penentu di banyak pos (Bab 39,40,72-76) | KUM HS 2(b), 3(b) |
| **Kondisi Impor** | CKD/SKD/lengkap sangat mempengaruhi pos | KUM HS 2(a) |
| **Fungsi Utama** | Prinsip utama klasifikasi WCO | KUM HS 1, 3(b) |
| **Cara Kerja** | Membedakan subpos dalam heading yang sama | KUM HS 6 |
| **Mobilitas** | Self-propelled vs towed menentukan pos (87.01 vs 87.04) | KUM HS 1 |
| **Area Operasi** | On-road vs off-highway beda pos (87.04.10 vs 87.04.21) | KUM HS 6 |
| **Sektor** | Konteks penggunaan memperkuat karakterisasi | KUM HS 1 |
| **Apakah Part** | Bagian/spare part punya pos berbeda (84.09, 84.31) | KUM HS 1 |
| **Apakah Set** | Set penjualan eceran → essential character | KUM HS 3(b), 5(b) |
| **Spesifikasi** | Parameter numerik (GVW, kVA, kg) → subpos nasional | KUM HS 6 |

### 3.3 Contoh: Bagaimana Input Membentuk Reasoning

**Input:** Dumper · Baja · Lengkap · Mengangkut barang · Diesel+Hidrolik · Self-propelled · Off-highway · GVW 4.5 ton

**Hasil prompt facts:**
```
Nama generik: Dumper
Material utama: Baja/Besi
Kondisi saat impor: Lengkap dan jadi (siap pakai)
Fungsi utama: Mengangkut barang (transportasi darat)
Cara kerja: Diesel / Motor bakar, Hidrolik
Mobilitas: Self-propelled (bergerak sendiri)
Area operasi: Off-highway / Perkebunan / Pertambangan
Spesifikasi teknis: GVW 4.5 ton
```

**AI reasoning:**
- KUM HS 1 → kendaraan angkut → Pos 87.04
- KUM HS 6 → off-highway = 8704.10, bukan 8704.21
- BTKI ref → GVW ≤5 ton + non-CKD → **8704.10.31** ✅

---

## 4. AI Agent yang Digunakan

### 4.1 Kondisi Saat Ini (MVP)

| Komponen | Nilai |
|----------|-------|
| **Provider** | Groq Cloud |
| **Model** | `openai/gpt-oss-120b` |
| **Arsitektur** | 117B parameter, Mixture-of-Experts |
| **Activated params** | 5.1B per forward pass |
| **Temperature** | 0.1 (sangat deterministik) |
| **Max tokens output** | 2.048 |
| **Biaya saat ini** | **Gratis** (Groq free tier org) |
| **Rate limit** | 30 req/menit · 1.000 req/hari · 8K token/menit |
| **Latency** | ~2–5 detik per request |

### 4.2 Perbandingan Model — Rekomendasi untuk Production

| Model | Provider | Kualitas Reasoning | Bahasa Indonesia | Biaya/1M token | Rekomendasi |
|-------|----------|--------------------|-----------------|----------------|-------------|
| `openai/gpt-oss-120b` | Groq | ⭐⭐⭐⭐ | ⭐⭐⭐ | Gratis (terbatas) | ✅ MVP sekarang |
| `llama-3.3-70b-versatile` | Groq | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Gratis (terbatas) | ✅ Alternatif MVP |
| `gpt-4o` | OpenAI | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | $2.50 input / $10 output | 🎯 Production |
| `gpt-4o-mini` | OpenAI | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | $0.15 input / $0.60 output | 🎯 Production hemat |
| `claude-3.5-sonnet` | Anthropic | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | $3.00 input / $15 output | 🎯 Premium |
| `gemini-2.0-flash` | Google | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | $0.075 input / $0.30 output | 🎯 Production murah |

### 4.3 Estimasi Biaya AI (Production)

Asumsi rata-rata 1 request = ~3.000 token input + ~500 token output:

| Model | Volume 500/bln | Volume 2.000/bln | Volume 10.000/bln |
|-------|---------------|-----------------|-------------------|
| Groq free tier | Gratis | Gratis (ada limit) | Perlu upgrade |
| GPT-4o-mini | ~$0.38 | ~$1.50 | ~$7.50 |
| GPT-4o | ~$6.25 | ~$25 | ~$125 |
| Gemini 2.0 Flash | ~$0.04 | ~$0.19 | ~$0.94 |

Rekomendasi production: **Gemini 2.0 Flash** (murah) atau **GPT-4o-mini** (kualitas tinggi).

---

## 5. Security

### 5.1 Kondisi Saat Ini (MVP — Perlu Diperbaiki Sebelum Production)

| Aspek | Status | Risiko |
|-------|--------|--------|
| API key di browser | ⚠️ Exposed | Siapapun yang inspect network bisa lihat key |
| Tidak ada autentikasi user | ⚠️ Open | Siapapun bisa akses |
| Tidak ada rate limiting | ⚠️ Open | Bisa di-abuse |
| Data tidak disimpan | ✅ Aman | Tidak ada data bocor |
| HTTPS | ✅ (via Vercel) | Enkripsi transport sudah ada |

### 5.2 Arsitektur Security untuk Production

```
User Browser
    │
    │ HTTPS
    ▼
[Auth Layer]
    │ JWT / Session
    ▼
[Backend API Server]  ← API key AI disimpan di sini (tidak di browser)
    │
    ├── Rate Limiter (per user/per IP)
    ├── Input Validation & Sanitization
    ├── Audit Logger
    │
    ▼
[AI Provider (Groq/OpenAI)]
    │
    ▼
[Database]
    ├── User & Auth data (terenkripsi)
    ├── Classification history (per tenant)
    └── Audit log
```

### 5.3 Checklist Security Production

- [ ] **API key pindah ke backend** — tidak pernah expose ke browser
- [ ] **Autentikasi user** — minimal email/password atau SSO
- [ ] **Authorization (RBAC)** — role: Admin, Reviewer, User
- [ ] **Rate limiting** — per user: max 50 req/hari free, unlimited premium
- [ ] **Tenant isolation** — data user A tidak bisa diakses user B
- [ ] **Audit log** — setiap request dicatat (siapa, kapan, input apa, output apa)
- [ ] **Input sanitization** — cegah prompt injection
- [ ] **HTTPS only** — semua komunikasi terenkripsi
- [ ] **Secret management** — API key di environment variable server, bukan hardcode

---

## 6. Deployment

### 6.1 Kondisi Saat Ini (MVP)

| Komponen | Platform |
|----------|----------|
| Frontend (React) | Vercel (konfigurasi sudah ada) |
| Backend | Belum ada — perlu dibangun |
| Database | Belum ada — perlu disiapkan |
| AI API | Groq free tier |

### 6.2 Target Deployment Production

Stack yang akan digunakan mengikuti infrastruktur yang sudah dimiliki:

| Komponen | Platform | Keterangan |
|----------|----------|------------|
| **Backend API** | C# (.NET) | Sesuai stack yang sudah ada |
| **Database** | MySQL | Sesuai stack yang sudah ada |
| **Server** | Vultr (sudah ada) | Deploy di VPS yang sama dengan landing page |
| **Frontend** | React (saat ini) atau on-prem | Bisa ikut di server Vultr atau tetap di Vercel — perlu keputusan lebih lanjut |
| **AI API** | Groq / OpenAI | Key disimpan di backend C#, tidak di frontend |

### 6.3 Arsitektur Deploy di Vultr

```
Vultr VPS
├── Nginx (reverse proxy)
│   ├── /          → Landing page (sudah ada)
│   ├── /app       → Frontend React (static build)
│   └── /api       → Backend C# (.NET API)
│
├── .NET Backend
│   ├── Endpoint: POST /api/classify
│   ├── Endpoint: POST /api/auth/login
│   ├── Endpoint: GET  /api/history
│   └── Koneksi ke MySQL + Groq/OpenAI API
│
└── MySQL
    ├── Database: hs_classifier
    └── Tables: users, classification_cases, expert_reviews, audit_logs, btki_reference
```

---

## 7. Database

Detail lengkap schema ada di **[docs/DATABASE_SCHEMA.md](./DATABASE_SCHEMA.md)**.

### Ringkasan Tabel

| Tabel | Fungsi | Fase |
|-------|--------|------|
| users | Autentikasi, role, plan | Fase 1 |
| classification_cases | Input form + output AI per kolom | Fase 1 |
| daily_usage | Rate limiting free tier | Fase 1 |
| expert_reviews | Workflow review approve/koreksi/reject | Fase 2 |
| audit_logs | Log semua aksi user | Fase 2 |
| btki_reference | Master BTKI 8-digit (ganti JSON di Fase 3) | Fase 3 |

### Prioritas Setup

Fase 1: users, classification_cases, daily_usage
Fase 2: expert_reviews, audit_logs
Fase 3: btki_reference (migrasi dari JSON ke DB)

---

## 8. Fitur Free vs Premium

### 8.1 Pembatasan yang Direkomendasikan

| Fitur | Free | Premium |
|-------|------|---------|
| Klasifikasi per hari | **10 req/hari** | Unlimited |
| History tersimpan | **7 hari** | Selamanya |
| Export hasil (PDF/Excel) | ❌ | ✅ |
| Expert review request | ❌ | ✅ |
| Bulk classification (CSV upload) | ❌ | ✅ |
| API access | ❌ | ✅ |
| Audit trail lengkap | ❌ | ✅ |
| Support prioritas | ❌ | ✅ |
| Multi-user / team | ❌ | ✅ |
| Custom BTKI notes per perusahaan | ❌ | ✅ |

### 8.3 Cara Implementasi Pembatasan (C# Backend)

```csharp
// Rate limiting free tier
var today = DateTime.Today;
var usage = await _db.DailyUsage
    .FirstOrDefaultAsync(u => u.UserId == userId && u.UsageDate == today);

if (user.Plan == "free" && usage?.ReqCount >= 10)
    return StatusCode(429, "Batas harian free tier (10 request) tercapai.");

// Feature gating
if (feature == "export" && user.Plan == "free")
    return StatusCode(403, "Fitur ini tersedia untuk pengguna Premium.");
```

---

## 9. Roadmap — Task-Based (Bukan Timeline)

### ✅ SUDAH SELESAI

- [x] UI form input terstruktur (14 fields, 5 section)
- [x] Prompt engine dengan KUM HS 1–6
- [x] Integrasi Groq API (openai/gpt-oss-120b)
- [x] BTKI national seed data 105 subpos (9 bab)
- [x] Output panel: kode HS, confidence, reasoning, eliminasi kandidat
- [x] Expert review flag otomatis
- [x] Dokumentasi teknis & user guide
- [x] Git repository + Vercel deployment config

---

### 🔴 TASK KRITIKAL (Harus selesai sebelum user pertama)

- [ ] **T1** — Pindahkan API key ke backend server (keamanan dasar)
- [ ] **T2** — Autentikasi user (email/password minimal)
- [ ] **T3** — Rate limiting free tier (10 req/hari)
- [ ] **T4** — Setup database PostgreSQL (users + classification_cases)
- [ ] **T5** — Simpan history klasifikasi per user
- [ ] **T6** — Validasi & koreksi BTKI seed data oleh ahli kepabeanan

---

### 🟡 TASK PENTING (Untuk product yang layak jual)

- [ ] **T7** — Ekspansi BTKI national ke semua bab (dari 105 → 500+ entries)
- [ ] **T8** — Expert review workbench (reviewer bisa approve/koreksi)
- [ ] **T9** — Export hasil ke PDF
- [ ] **T10** — Sistem free vs premium + payment gateway
- [ ] **T11** — Audit trail & log per user
- [ ] **T12** — Inject Catatan Bab/Bagian ke prompt (tingkatkan akurasi)
- [ ] **T13** — Gold dataset 100+ kasus tervalidasi untuk regression test

---

### 🟢 TASK NEXT LEVEL (Scale & enterprise)

- [ ] **T14** — Bulk classification (upload Excel/CSV, klasifikasi per baris)
- [ ] **T15** — API endpoint untuk integrasi sistem lain (ERP, SAP, dll)
- [ ] **T16** — Upload dokumen (invoice, datasheet) → AI extract fakta otomatis
- [ ] **T17** — Upload foto barang → AI extract karakteristik visual
- [ ] **T18** — Classification Memory (precedent dari kasus approved)
- [ ] **T19** — Multi-tenant dengan isolasi data penuh
- [ ] **T20** — Dashboard analytics (akurasi, volume, trend kode HS)
- [ ] **T21** — Notifikasi perubahan BTKI (jika ada revisi PMK)

---

## 10. Data & Seed yang Perlu Disiapkan / Diperluas

Kualitas rekomendasi AI sangat bergantung pada kelengkapan data referensi yang di-inject ke dalam prompt. Berikut status dan kebutuhan tiap dataset:

---

### 10.1 `btki_national.json` — Subpos Nasional Digit 7-8

**Status sekarang:** 105 entries, 9 bab

**Target:** Semua ~11.400 kode BTKI 2022

| Bab | Komoditas | Status | Prioritas |
|-----|-----------|--------|-----------|
| 27 | Minyak bumi (crude, solar, bensin, avtur) | ✅ Ada (6 entries) | — |
| 28–29 | Kimia anorganik & organik | ❌ Belum ada | 🔴 Tinggi |
| 30 | Farmasi & vaksin | ✅ Ada (3 entries) | Perlu diperluas |
| 31–37 | Pupuk, cat, kosmetik, fotografi | ❌ Belum ada | 🟡 Sedang |
| 38 | Produk kimia lainnya | ❌ Belum ada | 🔴 Tinggi |
| 39–40 | Plastik & karet/ban | ✅ Ada (7 entries) | Perlu diperluas |
| 44–48 | Kayu, kertas, karton | ❌ Belum ada | 🟡 Sedang |
| 50–63 | Tekstil & garmen (14 bab) | ❌ Belum ada | 🟡 Sedang |
| 64–67 | Alas kaki, topi, bulu | ❌ Belum ada | 🟢 Rendah |
| 68–70 | Batu, keramik, kaca | ❌ Belum ada | 🟡 Sedang |
| 72–76 | Baja, tembaga, aluminium | ✅ Ada (9 entries) | Perlu diperluas |
| 84 | Mesin mekanik | ✅ Ada (~25 entries) | Perlu diperluas |
| 85 | Elektronik & listrik | ✅ Ada (~15 entries) | Perlu diperluas |
| 86–88 | Kereta api, kendaraan, pesawat | ✅ Sebagian (87,89) | Perlu diperluas |
| 90–92 | Instrumen optik, jam, musik | ❌ Belum ada | 🟢 Rendah |
| 94–96 | Furnitur, mainan, peralatan | ❌ Belum ada | 🟢 Rendah |

**Cara generate:** Gunakan prompt yang sudah disediakan di `docs/BTKI_GENERATE_PROMPT.md` dengan ChatGPT/Claude, per bab, lalu validasi ahli kepabeanan sebelum masuk ke sistem.

---

### 10.2 `hs_headings_seed.csv` — Heading Level 4-6 Digit

**Status sekarang:** 43 entries, bab 27, 30, 72-74, 84-85, 87, 89

**Fungsi:** Referensi pengembangan dan validasi. Di masa depan akan dipindah ke tabel `btki_reference` di MySQL.

**Yang perlu ditambahkan:**

| Kolom | Isi | Contoh |
|-------|-----|--------|
| `hs4` | Heading 4 digit | `8471` |
| `hs6` | Subheading 6 digit | `847130` |
| `uraian_id` | Uraian Bahasa Indonesia | `"Mesin pengolah data otomatis portabel..."` |
| `uraian_en` | Uraian Bahasa Inggris | `"Portable ADP machines..."` |
| `seksi` | Seksi BTKI (I–XXI) | `XVI` |
| `bab` | Nomor bab | `84` |
| `catatan_penting` | Trigger/rule khusus subpos ini | `"Lihat Catatan 5 Bab 84"` |

**Target:** Lengkapi semua 1.300+ heading 4-digit BTKI sebagai master referensi.

---

### 10.3 `kumhs_rules.json` — KUM HS 1–6

**Status:** ✅ Lengkap, tidak perlu diubah kecuali ada revisi WCO.

---

### 10.4 `catatan_bab_seed.json` — Catatan Bagian & Bab

**Status sekarang:** Hanya Bagian XVI, XVII + Bab 84, 87, 89

**Fungsi:** Catatan ini sangat penting — menentukan inclusion/exclusion. Banyak kasus sulit diselesaikan hanya dengan KUM HS tanpa membaca Catatan Bab.

**Yang perlu ditambahkan:**

| Catatan | Isi | Prioritas |
|---------|-----|-----------|
| Catatan Bab 85 | Mesin listrik — apa yang termasuk dan dikecualikan | 🔴 Tinggi |
| Catatan Bab 87 | Kendaraan — perbedaan on-road vs off-highway | 🔴 Tinggi |
| Catatan Bagian VI | Kimia — aturan umum klasifikasi produk kimia | 🟡 Sedang |
| Catatan Bagian XI | Tekstil — definisi serat, kain, garmen | 🟡 Sedang |
| Catatan Bab 39 | Plastik — bentuk primer vs non-primer | 🟡 Sedang |
| Catatan Bab 72–76 | Logam — produk setengah jadi vs jadi | 🟡 Sedang |

**Rencana:** Setelah database MySQL siap, Catatan Bab disimpan di tabel `btki_notes` dan di-query dinamis berdasarkan bab kandidat.

---

### 10.5 Gold Dataset — Kasus Tervalidasi

**Status:** Belum ada

**Fungsi:** Digunakan untuk regression test — memastikan setiap update model/data tidak merusak kasus yang sudah benar.

**Target minimal:**

| Kategori | Jumlah Kasus | Contoh |
|----------|-------------|--------|
| Mesin & alat berat | 30 | Excavator, bulldozer, crane, genset |
| Kendaraan | 30 | Truk, dumper, traktor, motor, mobil |
| Elektronik | 20 | Laptop, smartphone, router, monitor |
| Kapal | 10 | Crane barge, kapal kargo, tanker |
| Kimia & material | 20 | CPO, minyak, plastik, baja |
| Spare part | 20 | Cylinder head, bucket, track shoe |
| Edge case (CKD/set/mixture) | 20 | Motor CKD, retail set, composite goods |
| **Total** | **150 kasus** | |

Setiap kasus format: `{ input_form, hs_code_benar, sumber_validasi, tanggal_validasi }`

---

*Dokumen ini adalah internal briefing untuk presentasi ke user/stakeholder.*  
*Detail teknis dapat berubah seiring pengembangan.*
