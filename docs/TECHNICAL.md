# Dokumentasi Teknis — AI HS Classification Intelligence (MVP)

**Versi:** MVP v1.0  
**Stack:** React + Vite + Tailwind CSS + Groq API (Llama 3.3 70B)  
**Referensi hukum:** BTKI 2022 (PMK 26/2022), KUM HS 1–6  
**Status:** Development / Internal Use

---

## Daftar Isi

1. [Arsitektur Sistem](#1-arsitektur-sistem)
2. [Struktur Folder](#2-struktur-folder)
3. [Data Layer](#3-data-layer)
4. [Komponen UI](#4-komponen-ui)
5. [Service Layer — Prompt Engine](#5-service-layer--prompt-engine)
6. [AI Integration — Groq API](#6-ai-integration--groq-api)
7. [Alur Data End-to-End](#7-alur-data-end-to-end)
8. [Environment & Konfigurasi](#8-environment--konfigurasi)
9. [Cara Menjalankan](#9-cara-menjalankan)
10. [Cara Extend & Scale](#10-cara-extend--scale)
11. [Known Limitations](#11-known-limitations)

---

## 1. Arsitektur Sistem

```
┌─────────────────────────────────────────────────────┐
│                  Browser (Client)                   │
│                                                     │
│  ┌──────────────────┐     ┌───────────────────────┐ │
│  │ClassificationForm│───▶│    ResultPanel         │ │
│  │ (Input Layer)    │     │    (Output Layer)     │ │
│  └────────┬─────────┘     └───────────────────────┘ │
│           │                          ▲              │
│           ▼                          │              │
│  ┌──────────────────────────────────┤               │
│  │      classifier.js (Service)     │               │
│  │  buildPrompt() ──▶ callGroq()────┘              │
│  └──────────────────────────────────────────────────┘
│                          │
└──────────────────────────┼──────────────────────────┘
                           │ HTTPS POST
                           ▼
              ┌────────────────────────┐
              │   Groq API             │
              │   model: llama-3.3-70b │
              │   temperature: 0.1     │
              └────────────────────────┘
```

**Catatan arsitektur:**
- Tidak ada backend server — seluruh logic berjalan di browser
- API key Groq di-inject via Vite env variable (`VITE_GROQ_API_KEY`)
- Tidak ada database — MVP ini stateless, tidak menyimpan history
- AI dipanggil sekali per submit, tidak ada streaming

> ⚠️ **[FOTO: Screenshot arsitektur diagram atau tampilan DevTools Network tab menunjukkan request ke api.groq.com]**

---

## 2. Struktur Folder

```
hs-classifier/
├── public/
├── src/
│   ├── components/
│   │   ├── App.jsx                 ← Root layout, state management
│   │   ├── ClassificationForm.jsx  ← Form utama, section-based
│   │   ├── SelectField.jsx         ← Komponen dropdown single
│   │   ├── MultiSelectField.jsx    ← Komponen chip multi-select
│   │   ├── TextField.jsx           ← Input teks & textarea
│   │   └── ResultPanel.jsx         ← Panel output hasil AI
│   ├── data/
│   │   ├── dropdown_master.json    ← Master semua pilihan dropdown
│   │   ├── kumhs_rules.json        ← KUM HS 1–6 (digunakan dalam prompt)
│   │   └── btki_national.json      ← 105 subpos nasional BTKI digit 7-8 (di-inject ke prompt)
│   ├── services/
│   │   └── classifier.js           ← buildPrompt() + callGroq()
│   ├── index.css                   ← Tailwind import
│   └── main.jsx                    ← React entry point
├── .env                            ← API key (TIDAK di-commit)
├── .env.example                    ← Template env
├── vite.config.js
└── package.json
```

---

## 3. Data Layer

### 3.1 `dropdown_master.json`

File ini adalah **single source of truth** untuk semua pilihan dropdown di UI. Struktur:

```json
{
  "dropdown_fields": {
    "material_utama": {
      "label": "Material Utama",
      "multi": true,
      "options": ["Baja/Besi", "Aluminium", ...]
    },
    "kondisi_impor": {
      "label": "Kondisi Saat Impor",
      "multi": false,
      "options": ["Lengkap dan jadi", "CKD", ...]
    }
    // ... 8 dropdown fields total
  },
  "text_fields": [
    { "key": "nama_dagang", "label": "...", "placeholder": "...", "required": false },
    { "key": "nama_generik", "label": "...", "placeholder": "...", "required": true }
    // ... 4 text fields total
  ]
}
```

**Field yang ada:**

| Key | Tipe | Multi | Keterangan |
|-----|------|-------|------------|
| `nama_dagang` | text | — | Brand/model barang |
| `nama_generik` | text | — | **Wajib diisi** |
| `material_utama` | dropdown | ✅ | Multi-select chip |
| `kondisi_impor` | dropdown | ❌ | Lengkap/CKD/SKD/dll |
| `fungsi_utama` | dropdown | ❌ | Fungsi primer barang |
| `cara_kerja` | dropdown | ✅ | Mekanis/Elektrik/dll |
| `mobilitas` | dropdown | ❌ | Self-propelled/towed/dll |
| `area_operasi` | dropdown | ❌ | On-road/off-highway/laut/dll |
| `sektor` | dropdown | ❌ | Industri pengguna |
| `apakah_part` | dropdown | ❌ | Ya/Tidak/Tidak yakin |
| `apakah_set` | dropdown | ❌ | Ya/Tidak |
| `ada_komponen_tambahan` | dropdown | ❌ | Ya/Tidak |
| `spesifikasi` | textarea | — | Spesifikasi teknis bebas |
| `keterangan_tambahan` | textarea | — | Catatan bebas |

### 3.2 `kumhs_rules.json`

Berisi KUM HS 1–6 dalam format machine-readable. Setiap rule punya:
- `rule` — label (KUM HS 1, KUM HS 2(a), dst)
- `trigger` — kapan rule ini diaktifkan
- `isi` — teks resmi rule
- `implikasi_ai` — instruksi untuk AI cara menggunakan rule ini
- `contoh` — contoh konkret

File ini **di-inject langsung ke dalam prompt** oleh `buildPrompt()`, bukan disimpan di database.

### 3.3 `btki_national.json`

Dataset subpos nasional BTKI 2022 (digit 7-8) yang di-inject langsung ke dalam prompt. **105 entries** dari 9 grup bab prioritas:

| Bab | Komoditas | Entries |
|-----|-----------|---------|
| 27 | Minyak bumi — crude, solar, bensin, avtur | 6 |
| 30 | Farmasi & vaksin | 3 |
| 39 | Plastik — PE, PP, PVC dalam bentuk primer | 4 |
| 40 | Ban karet pneumatik | 3 |
| 72–76 | Baja (HRC/CRC), pipa OCTG, tembaga, aluminium | 9 |
| 84 | Mesin, alat berat, pompa, crane, komputer, genset | 25 |
| 85 | Elektronik, motor listrik, kabel, switch | 15 |
| 87 | Kendaraan — mobil, motor, truk, dumper, traktor | 28 |
| 89 | Kapal — kargo, tanker, crane barge, kapal keruk | 12 |

Format per entry: `{ "hs8": "XXXX.XX.XX", "uraian": "deskripsi subpos nasional" }`

Seluruh data di-serialize menjadi teks dan di-inject ke dalam prompt via `buildPrompt()`. Token footprint: ~2.300 token (aman untuk limit 8K token/menit Groq).

### 3.4 `hs_headings_seed.csv` (referensi)

Seed data 43 heading/subheading level 4-6 digit untuk referensi pengembangan. Tidak di-inject ke prompt — sudah digantikan oleh `btki_national.json`.

---

## 4. Komponen UI

### 4.1 `App.jsx` — Root Component

Memegang state utama:
```js
const [result, setResult] = useState(null)    // Hasil dari AI
const [isLoading, setIsLoading] = useState(false)
```

Flow:
1. `ClassificationForm` → `onSubmit(form)` dipanggil
2. `App` set `isLoading = true`, panggil `classifyHS(form)`
3. Hasil disimpan di `result`, dikirim ke `ResultPanel`

Layout: **2 kolom** di desktop (lg breakpoint), stack di mobile. Kolom kanan sticky.

### 4.2 `ClassificationForm.jsx`

Form dibagi dalam **5 section** yang dirender secara loop dari array `SECTIONS`:

```js
const SECTIONS = [
  { title: '1. Identitas Barang', icon: '📦', fields: ['nama_dagang', 'nama_generik'] },
  { title: '2. Fisik & Material', icon: '🔩', fields: ['material_utama', 'kondisi_impor'] },
  { title: '3. Fungsi & Cara Kerja', icon: '⚙️', fields: ['fungsi_utama', 'cara_kerja', 'mobilitas'] },
  { title: '4. Konteks Penggunaan', icon: '🏭', fields: ['area_operasi', 'sektor'] },
  { title: '5. Karakteristik Tambahan', icon: '📋', fields: [...] },
]
```

`renderField(key)` otomatis memilih komponen yang tepat berdasarkan tipe di `dropdown_master.json`.

Validasi: hanya `nama_generik` yang wajib diisi (client-side `alert`).

> ⚠️ **[FOTO: Screenshot ClassificationForm lengkap di browser — tampilkan semua 5 section]**

### 4.3 `SelectField.jsx`

Dropdown HTML native `<select>`. Props: `label, fieldKey, options, value, onChange, required`.

### 4.4 `MultiSelectField.jsx`

Render setiap option sebagai `<button>` toggle. State dipanggil sebagai array. Warna chip: biru jika selected, abu-abu jika tidak.

> ⚠️ **[FOTO: Screenshot MultiSelectField dengan beberapa chip terpilih (biru)]**

### 4.5 `ResultPanel.jsx`

Merender 5 blok kondisional:

| Kondisi | Yang ditampilkan |
|---------|-----------------|
| `isLoading` | Spinner + teks "AI sedang menganalisis..." |
| `!result` | Empty state dengan ikon 🔍 |
| `result.error` | Error message merah |
| `result` normal | Semua blok hasil |

Blok hasil:
1. **Primary Result** — HS code besar + confidence badge (hijau/kuning/merah)
2. **Reasoning KUM HS** — timeline step per rule yang digunakan
3. **Kandidat Dieliminasi** — tabel kode + alasan eliminasi
4. **Fakta Perlu Konfirmasi** — list kuning
5. **Expert Review Flag** — banner merah jika `perlu_expert_review: true`

> ⚠️ **[FOTO: Screenshot ResultPanel dengan hasil lengkap — tampilkan semua 5 blok]**

---

## 5. Service Layer — Prompt Engine

### 5.1 `buildPrompt(form)`

Mengkonversi form object menjadi structured prompt string. Logika:

```js
const facts = [
  form.nama_dagang && `Nama dagang/model: ${form.nama_dagang}`,
  form.nama_generik && `Nama generik: ${form.nama_generik}`,
  // ... semua field, skip jika kosong
].filter(Boolean).join('\n')
```

KUM HS rules di-inject dari `kumhs_rules.json`:
```js
const kumhsSummary = kumhsRules.kumhs
  .map((r) => `${r.rule} (trigger: ${r.trigger}): ${r.isi}`)
  .join('\n')
```

Data BTKI nasional di-inject dari `btki_national.json`:
```js
const btkiref = btki.data
  .map((d) => `${d.hs8} — ${d.uraian}`)
  .join('\n')
```

Ketiganya digabung dalam satu prompt dengan instruksi eksplisit ke AI untuk menggunakan referensi BTKI saat menentukan digit 7-8 nasional.

Prompt berakhir dengan instruksi format output JSON yang strict:
```
Berikan jawaban HANYA dalam format JSON berikut (tanpa markdown):
{
  "hs_code": "XXXX.XX.XX",
  "uraian": "...",
  "confidence": "TINGGI"|"SEDANG"|"RENDAH",
  "reasoning_steps": [...],
  "kandidat_eliminasi": [...],
  "fakta_perlu_konfirmasi": [...],
  "perlu_expert_review": boolean,
  ...
}
```

### 5.2 Response Parsing

```js
const cleaned = text
  .replace(/^```json\s*/i, '')  // strip opening fence
  .replace(/```\s*$/i, '')      // strip closing fence
  .trim()

return JSON.parse(cleaned)
```

Jika parse gagal, return `{ error: "Gagal parse..." }` yang akan ditampilkan sebagai error state di UI.

---

## 6. AI Integration — Groq API

**Endpoint:** `https://api.groq.com/openai/v1/chat/completions`  
**Model:** `llama-3.3-70b-versatile`  
**Temperature:** `0.1` (rendah → output deterministik, cocok untuk tugas legal)  
**Max tokens:** `2048`

Request body:
```json
{
  "model": "llama-3.3-70b-versatile",
  "temperature": 0.1,
  "max_tokens": 2048,
  "messages": [
    { "role": "user", "content": "<full prompt>" }
  ]
}
```

Response diambil dari:
```js
data.choices[0].message.content
```

**Rate limit Groq (free tier):**
- 14.400 request/hari
- 30 request/menit
- 6.000 token/menit untuk Llama 3.3 70B

---

## 7. Alur Data End-to-End

```
User mengisi form
        │
        ▼
ClassificationForm.handleSubmit()
        │ form object (14 fields)
        ▼
App.handleSubmit()
        │ setIsLoading(true)
        ▼
classifyHS(form)              [classifier.js]
        │
        ├── buildPrompt(form)
        │       ├── Serialize form fields → facts string
        │       └── Inject KUM HS 1–6 dari kumhs_rules.json
        │
        ├── callGroq(prompt, apiKey)
        │       ├── POST https://api.groq.com/openai/v1/chat/completions
        │       ├── Model: llama-3.3-70b-versatile
        │       └── Parse JSON response
        │
        ▼
result object:
{
  hs_code, uraian, confidence, risk,
  reasoning_steps[], kandidat_eliminasi[],
  fakta_perlu_konfirmasi[], perlu_expert_review
}
        │
        ▼
App.setResult(result)
        │
        ▼
ResultPanel render hasil
```

> ⚠️ **[FOTO: Screenshot DevTools Network tab — tampilkan request POST ke api.groq.com dengan response JSON]**

---

## 8. Environment & Konfigurasi

File `.env` di root project `hs-classifier/`:

```env
VITE_GROQ_API_KEY=gsk_xxxxxxxxxxxxxxxxxxxxx
```

Vite expose semua `VITE_*` variable ke browser via `import.meta.env`. Variable tanpa prefix `VITE_` tidak akan tersedia di client.

**Fallback behavior:**
- Jika `VITE_GROQ_API_KEY` tidak ada → `mockResponse()` digunakan
- Mock response hanya mengenali 3 kata kunci: "laptop", "dumper", "crane"
- Semua input lain → response `RENDAH` confidence

> ⚠️ **Jangan commit file `.env` ke repository.** File ini sudah ada di `.gitignore` default Vite.

---

## 9. Cara Menjalankan

### Prerequisites
- Node.js ≥ 18
- npm ≥ 9

### Development
```bash
cd hs-classifier
cp .env.example .env
# Edit .env, isi VITE_GROQ_API_KEY
npm install
npm run dev
# Buka http://localhost:5173
```

### Production Build
```bash
npm run build
# Output di dist/
# Deploy dist/ ke static hosting (Nginx, Vercel, Netlify, dll)
```

### Struktur Build Output
```
dist/
├── index.html         (~0.5 KB)
├── assets/
│   ├── index-*.css    (~17 KB gzip: 4.4 KB)
│   └── index-*.js     (~216 KB gzip: 68 KB)
```

---

## 10. Cara Extend & Scale

### Tambah Field Dropdown Baru
Edit `src/data/dropdown_master.json` → tambah key baru di `dropdown_fields` atau `text_fields`. `ClassificationForm` otomatis render field baru jika ditambahkan ke array `SECTIONS`.

### Ganti Model AI
Di `classifier.js`, ganti nilai `model`:
```js
model: 'llama-3.3-70b-versatile',  // ganti ke model lain
// Groq model tersedia: llama-3.1-8b-instant, mixtral-8x7b, gemma2-9b-it
```

### Tambah Backend (Recommended sebelum production)
Untuk menyembunyikan API key dari browser:
1. Buat Express/FastAPI server
2. Pindahkan `callGroq()` ke server endpoint (misal `POST /api/classify`)
3. Di frontend, ganti `callGroq()` dengan `fetch('/api/classify', { body: form })`

### Integrasi BTKI Database
Saat data BTKI lengkap tersedia:
1. Simpan di PostgreSQL dengan full-text search
2. Di `buildPrompt()`, query heading yang relevan berdasarkan kata kunci dari form
3. Inject daftar kandidat heading ke dalam prompt sebelum dikirim ke AI

### Simpan History Klasifikasi
Tambah state `history` di `App.jsx` atau integrasikan dengan localStorage/IndexedDB untuk menyimpan riwayat tanpa backend.

---

## 11. Known Limitations

| Limitation | Dampak | Upgrade Path |
|------------|--------|--------------|
| API key exposed di browser | Risiko key dicuri jika deploy publik | Pindahkan ke backend server |
| Tidak ada BTKI database | AI mengandalkan training data, bisa salah subpos nasional | Integrasi BTKI resmi dari DJBC |
| Tidak ada EN (Explanatory Notes) | Analisis kandidat kurang mendalam | Tambahkan EN sebagai context di prompt |
| Stateless — tidak ada history | Tidak bisa audit trail | Tambahkan localStorage atau backend DB |
| Validasi hanya client-side | User bisa submit form kosong (kecuali nama_generik) | Tambahkan validasi lebih ketat |
| Free tier Groq 14.400 req/hari | Cukup untuk development, batas untuk production | Upgrade ke Groq paid atau ganti ke backend dengan rate limiter |
| Output tidak 100% terjamin | AI bisa hallucinate kode HS | Tambahkan gold dataset + regression test |
