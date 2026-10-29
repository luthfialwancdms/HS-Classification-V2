# HS Classification V2 — End-to-End Workflow
## Panduan untuk AI / Automation Workflow

**Versi:** V2 Target Architecture  
**Stack:** React Frontend · C# Backend · MySQL · LLM (Groq/OpenAI)

---

## Gambaran Besar

Alur sistem terbagi menjadi **2 jalur utama** yang berjalan secara terpisah:

```
┌─────────────────────────────────────────────────────────┐
│  JALUR A: ADMIN DATA CENTER                             │
│  (Sekali setup, diulang tiap ada update BTKI)           │
│  Upload DOCX → Parse → Validasi → Aktivasi              │
└─────────────────────────────────────────────────────────┘
                          │
                          │ Database BTKI siap
                          ▼
┌─────────────────────────────────────────────────────────┐
│  JALUR B: KLASIFIKASI BARANG                            │
│  (Setiap user melakukan klasifikasi)                    │
│  Input → Ekstrak Fakta → Kandidat → Rules Engine →      │
│  Validasi → Tarif → Output                              │
└─────────────────────────────────────────────────────────┘
```

---

## JALUR A — Admin Data Center

### Tujuan
Mengisi database dengan data BTKI yang valid sebelum sistem klasifikasi bisa digunakan.

### Alur Step-by-Step

```
[1] Admin upload file DOCX
         │
         │  File: BTKI 2022 (PMK 26/2022)
         │  File: Explanatory Notes (EN) WCO 2022
         │  File: Catatan Bab/Bagian (Legal Notes)
         ▼
[2] Parser DOCX (OpenXML SDK)
         │
         │  Ekstrak dari struktur OOXML:
         │  - Kode HS (4/6/8 digit)
         │  - Uraian Bahasa Indonesia
         │  - Uraian Bahasa Inggris
         │  - Level indentasi (untuk hierarki)
         │  - Tarif MFN, FTA, PPN, PPh 22 (dari tabel)
         │  - Source location: halaman, paragraf, sel tabel
         ▼
[3] Deteksi Hierarki
         │
         │  Bangun relasi parent-child dari indentasi:
         │  8471 → 8471.30 → 8471.30.10
         │                 → 8471.30.90
         │
         │  Jika parent tidak bisa ditentukan:
         │  → Flag: STRUCTURE_AMBIGUITY → masuk review queue
         ▼
[4] Validasi Struktur
         │
         │  Cek:
         │  ✓ Format kode (XXXX.XX.XX)
         │  ✓ Tidak ada kode duplikat
         │  ✓ Semua parent ada
         │  ✓ Total kode = 14.273 (BTKI 2022)
         │  ✓ Uraian tidak kosong
         │
         │  Output: Laporan validasi (ERROR / WARNING / INFO)
         ▼
[5] Review Antrian Ambiguitas
         │
         │  Admin review entri STRUCTURE_AMBIGUITY
         │  → Approve atau koreksi manual
         │  → Wajib selesai sebelum bisa commit
         ▼
[6] Dry Run
         │
         │  Simulasi aktivasi tanpa commit ke database:
         │  - Semua parent kode ada?
         │  - Tidak ada circular reference?
         │  - Total count cocok dengan expected?
         │  - Tidak overlap dengan versi aktif?
         │
         │  Jika PASS → lanjut ke commit
         │  Jika FAIL → kembali ke review
         ▼
[7] Commit & Aktivasi
         │
         │  Status Version:
         │  UPLOADED → PARSING → MAPPED → VALIDATING
         │  → DRY_RUN_PASSED → READY_TO_COMMIT
         │  → COMMITTED → INDEXED → ACTIVE
         │
         │  Hanya SATU versi yang ACTIVE per tanggal
         ▼
[8] Database Siap
         │
         │  Tabel terisi:
         │  - btki_tariff_lines (14.273 entri)
         │  - explanatory_notes (65.833 paragraf)
         │  - section_notes, chapter_notes, subheading_notes
         │  - mfn_rates, fta_rates, tax_rates
```

---

## JALUR B — Klasifikasi Barang

### Tujuan
Menentukan kode HS 8-digit yang tepat untuk barang yang akan diimpor, lengkap dengan tarif dan reasoning yang bisa diaudit.

---

### STEP 1 — Input User

```
User mengisi form atau teks bebas:

FIELD INPUT:
├── Identitas    : nama dagang, nama generik, brand, model
├── Material     : material utama (bisa lebih dari satu)
├── Kondisi      : lengkap / CKD / SKD / tidak lengkap / campuran
├── Fungsi       : fungsi utama, cara kerja, prinsip operasi
├── Mobilitas    : self-propelled / ditarik / portabel / statis / terapung
├── Penggunaan   : area operasi, sektor industri
├── Status       : apakah part? apakah set/kit?
└── Spesifikasi  : GVW, kapasitas, daya, berat, dimensi, dll

INPUT FORMAT yang didukung:
- Form terstruktur (dropdown + multi-select)
- Teks bebas (untuk keterangan tambahan)
- Dokumen (invoice, datasheet) — fase 2
- Foto barang — fase 2
```

---

### STEP 2 — Fact Extraction Engine

**Dijalankan oleh:** LLM (hanya untuk ekstraksi, BUKAN untuk menentukan kode HS)

```
Input user
    │
    ▼
LLM mengekstrak 35 tipe fakta:

KATEGORI FAKTA:
├── Identitas      : commercial_name, generic_name, brand, model
├── Material       : material, composition, percentage, concentration
├── Fisik          : form, dimensions, weight, capacity, power_rating, voltage
├── Proses         : manufacturing_process, finishing_process
├── Fungsi         : primary_function, secondary_function, operating_principle
├── Energi         : power_source, fuel_type, energy_rating
├── Penggunaan     : primary_use, operating_area, mobility, mounting
├── Kondisi        : complete_status, finished_status, assembled_status, CKD_SKD_status
├── Presentasi     : bulk_unit, mixture_composite, set_kit, part_accessory
├── Status khusus  : used_condition, waste_scrap, packaging_container
└── Dokumen        : supporting_documents

SETIAP FAKTA MENYIMPAN:
- raw_value        : nilai mentah dari input
- normalized_value : nilai setelah normalisasi (contoh: "1 ton" → 1000 kg)
- unit             : satuan
- source           : USER / DOCUMENT / SYSTEM
- confidence_score : 0.0 – 1.0
- status           : UNKNOWN / NOT_APPLICABLE / ZERO / CONFLICTING / VERIFIED

ATURAN PENTING:
→ Fakta yang tidak disebutkan: status = UNKNOWN, nilai = null
→ LLM DILARANG mengisi nilai default untuk fakta yang tidak ada
→ Jika ada 2 nilai berbeda untuk fakta yang sama: status = CONFLICTING

    │
    ▼
Cek fakta yang masih UNKNOWN
    │
    ├─ Ada fakta penting yang kurang?
    │       │
    │       ▼
    │  Dynamic Question Generator
    │       │
    │       │  Hitung information gain per fakta:
    │       │  Gain = (Kandidat tereliminasi / Total kandidat) × Reliabilitas
    │       │
    │       │  Sort pertanyaan dari gain tertinggi
    │       │
    │       ▼
    │  Tanya user pertanyaan dengan gain tertinggi dahulu
    │       │
    │       ▼
    │  Terima jawaban → update FactSet → recalculate gains
    │       │
    │       └─ Loop sampai fakta kritis terpenuhi atau user tidak bisa jawab
    │
    └─ Fakta cukup? → lanjut ke Step 3
```

---

### STEP 3 — Candidate Generation

**Dijalankan oleh:** Database query (MySQL), BUKAN LLM

```
FactSet (fakta yang sudah lengkap)
    │
    ▼
Multi-Strategy Search ke database btki_tariff_lines:

STRATEGI 1: Exact Lexical Match
    SELECT * FROM btki_tariff_lines
    WHERE description_id LIKE '%{generic_name}%'
    OR description_en LIKE '%{generic_name}%'

STRATEGI 2: Synonym Mapping
    "laptop" → "mesin pengolah data otomatis portabel"
    "dumper" → "kendaraan motor untuk pengangkutan barang, off-highway"
    "crane barge" → "crane terapung, struktur terapung"
    ... (kamus terminologi)

STRATEGI 3: Function-Based Search
    primary_function = "lifting/mengangkat"
    → kandidat: 8426 (crane darat), 8427 (forklift), 8905 (crane terapung)

STRATEGI 4: Material-Based Search
    material = "baja/besi"
    → kandidat: Bab 72 (besi/baja), Bab 73 (produk besi/baja)

STRATEGI 5: Legal Note Referral
    Cek apakah Section/Chapter Notes merujuk ke heading tertentu
    Contoh: Catatan 5 Bab 84 → definisi mesin ADP → 8471

STRATEGI 6: EN Cross-Reference
    Parse "see also" dan "excluded from this heading" dari Explanatory Notes
    → Tambahkan heading yang dirujuk sebagai kandidat

    │
    ▼
Untuk setiap kandidat heading (4-digit), ambil context:

ENRICHMENT PER KANDIDAT:
├── Deskripsi resmi (Indonesia + Inggris)
├── Hierarki: seksi → bab → heading
├── Section Notes yang berlaku
├── Chapter Notes yang berlaku
├── Notes yang menarget heading ini secara spesifik
├── Subheading Notes
├── Semua paragraf EN untuk heading ini
├── Child subheadings (untuk KUM HS 6 nanti)
├── Required discriminator facts (dari deskripsi subheading)
└── source_id → link ke lokasi di dokumen asli

    │
    ▼
Output: List kandidat heading + context lengkap
```

---

### STEP 4 — Rules Engine (KUM HS 1–6)

**Dijalankan oleh:** Deterministic rules engine (C#), dengan LLM sebagai helper untuk reasoning text

```
List kandidat heading + FactSet
    │
    ▼
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 1 — Heading Terms + Legal Notes + EN
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk setiap kandidat, secara berurutan:

[a] Cocokkan fakta vs deskripsi heading
[b] Terapkan Section Notes:
    - DEFINITION → apakah barang termasuk definisi?
    - INCLUSION   → apakah barang masuk lingkup?
    - EXCLUSION   → apakah barang dikecualikan?
    - REFERRAL    → apakah notes mengarahkan ke heading lain?
[c] Terapkan Chapter Notes (sama seperti Section Notes)
[d] Terapkan Notes yang spesifik untuk heading ini
[e] Gunakan EN untuk interpretasi:
    - Paragraf INCLUSION → konfirmasi kandidat
    - Paragraf EXCLUSION → eliminasi kandidat
    - Paragraf DEFINITION → klarifikasi scope

Rekam: fakta yang cocok, fakta yang kurang, keputusan (RETAINED/ELIMINATED/UNRESOLVED), alasan, source_id

    │
    ├─ TEPAT 1 heading RETAINED?
    │       → Loncat langsung ke KUM HS 6 ↓
    │
    ├─ 0 heading RETAINED?
    │       → Status: NEED_EXPERT_REVIEW → stop
    │
    └─ 2+ heading RETAINED? → lanjut KUM HS 2

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 2 — Barang Tidak Lengkap / Campuran
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Trigger: assembled_status = CKD / SKD / tidak lengkap

[KUM HS 2(a)] — Barang Tidak Lengkap:
    Evaluasi karakter pokok:
    - Komponen mana yang kurang?
    - Apakah fungsi utama sudah terbentuk?
    - Apakah identitas barang sudah bisa dikenali?

    Output:
    → TREAT_AS_COMPLETE   : klasifikasi sebagai barang utuh
    → CLASSIFY_AS_PRESENTED: klasifikasi sebagai parts/komponen
    → NEED_CLARIFICATION  : minta konfirmasi user

[KUM HS 2(b)] — Campuran Material:
    Trigger: mixture, combination, goods partly of material
    Jika material extension → 2+ heading:
    → Lanjut ke KUM HS 3

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 3 — Pilih dari Beberapa Heading
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Trigger: 2+ heading masih retained setelah KUM HS 1 & 2

[KUM HS 3(a)] — Spesifisitas:
    Hitung specificity score tiap heading:
    +1.0  heading menyebut barang spesifik
    -1.0  heading residual ("lain-lain", "not elsewhere specified")
    +0.5  heading cocok untuk barang lengkap
    -0.5  heading untuk parts

    → Heading paling spesifik dipilih
    → Jika seri, lanjut ke KUM HS 3(b)

[KUM HS 3(b)] — Essential Character:
    Bobot faktor:
    Fungsi/peran     30%  ← biasanya paling dominan
    Material         15%
    Bulk/jumlah      15%
    Nilai            15%
    Kebutuhan        15%
    Persepsi konsumen 10%

    → Heading yang materinya/komponen-nya
      memberikan karakter pokok = heading terpilih
    → Tidak bisa ditentukan? → KUM HS 3(c)

[KUM HS 3(c)] — Urutan Numerik Terakhir:
    Di antara heading yang equally applicable:
    → Pilih heading dengan nomor paling besar/akhir
    → JANGAN memasukkan heading lemah hanya supaya bisa pakai rule ini

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 4 — Most Akin (Paling Mirip)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Trigger: KUM HS 1-3 tidak bisa menentukan

    Bandingkan dengan barang-barang yang paling mirip:
    material, bentuk, fungsi, prinsip kerja, penggunaan,
    karakteristik teknis, identitas komersial

    → Perlu expert review (flag otomatis)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 5 — Kemasan & Peti
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[KUM HS 5(a)] — Peti/Case Khusus:
    Evaluasi: berbentuk khusus untuk barang ini?
    untuk pemakaian jangka panjang? dijual bersama barang?
    → Jika ya: diklasifikasikan bersama barang utama

[KUM HS 5(b)] — Bahan/Wadah Pengemasan:
    Evaluasi: disajikan bersama barang? normalnya untuk packing?
    → Jika ya: diklasifikasikan bersama barang utama

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
KUM HS 6 — Descent ke Subheading
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Trigger: 4-digit heading sudah terkunci

    Ambil child subheadings dari heading yang dikunci

    Aturan perbandingan:
    → Bandingkan HANYA subheading di level yang SAMA
       (1-dash vs 1-dash, 2-dash vs 2-dash)
    → Terapkan KUM HS 1-5 secara analogis di level subheading
    → Gunakan Subheading Notes sebagai tambahan
    → Discriminator: berat, kapasitas, daya, komposisi,
                    retail/curah, CKD/non-CKD, penggunaan

    Descent: 4-digit → 6-digit → 8-digit (BTKI nasional)

    JIKA fakta discriminator tidak ada:
    → Status: NEED_CLARIFICATION
    → JANGAN default ke .00 (kode generik)
    → Minta konfirmasi user

    Output: kode HS 8-digit + uraian resmi dari database

    │
    ▼
Heading 4-digit + Subheading 6-digit + Kode nasional 8-digit TERKUNCI
```

---

### STEP 5 — Validator

**Dijalankan oleh:** Validation service (C#), query ke database

```
Kode HS 8-digit yang dihasilkan Rules Engine
    │
    ▼
VALIDASI BERLAPIS (semua harus PASS):

[V1] Kode ada di database?
     SELECT COUNT(*) FROM btki_tariff_lines
     WHERE code = '{code}' AND version_id = '{activeVersionId}'
     → FAIL: kode tidak ada → REJECT

[V2] Format kode benar?
     Regex: ^\d{4}\.\d{2}\.\d{2}$
     → FAIL: format salah → REJECT

[V3] Semua parent kode ada?
     Cek: 8471 → 8471.30 → 8471.30.20
     Semua level harus ada di database
     → FAIL: parent putus → REJECT

[V4] Uraian exact match?
     Bandingkan uraian dari Rules Engine
     vs uraian resmi di database
     → FAIL: uraian tidak cocok → REJECT

[V5] Kode aktif pada tanggal klasifikasi?
     effective_from ≤ classification_date ≤ effective_to
     → FAIL: kode tidak aktif → REJECT

[V6] Semua source_id valid?
     Semua referensi ke notes/EN dalam reasoning chain
     harus bisa di-resolve ke record database
     → FAIL: broken reference → REJECT

    │
    ├─ Semua PASS → lanjut ke Tariff Lookup
    │
    └─ Ada yang FAIL → REJECT
                    → Error dikembalikan ke Rules Engine
                    → Jangan tampilkan hasil ke user
                    → Log error untuk monitoring
```

---

### STEP 6 — Tariff Lookup Module

**Dijalankan oleh:** Database query (MySQL)

```
Kode HS 8-digit yang sudah tervalidasi
    │
    ▼
LOOKUP TARIF MFN (Bea Masuk Umum):
    SELECT rate_value, rate_unit, currency, quantity_unit
    FROM mfn_rates
    WHERE code = '{code}'
    AND effective_from <= '{date}' <= effective_to

    rate_unit:
    - PERCENT  : persentase dari nilai barang
    - SPECIFIC : nominal per satuan (IDR/USD per kg/unit)
    - COMPOUND : kombinasi persen + specific

    │
    ▼
LOOKUP TARIF FTA (Perjanjian Dagang):
    SELECT scheme_code, partner_country, rate_value,
           rate_unit, effective_dates, conditions_summary
    FROM fta_rates
    WHERE code = '{code}'
    ORDER BY scheme_code, partner_country

    ! PENTING — Label sebagai "Tarif FTA Tersedia" (BUKAN "Tarif FTA Berlaku")
    ! DISCLAIMER WAJIB:
    "Ketersediaan tarif FTA bukan jaminan eligibilitas. Keberlakuan
    bergantung pada: asal barang, pemenuhan Rules of Origin,
    kepemilikan Certificate of Origin, direct consignment,
    dan persyaratan prosedural lainnya."

    │
    ▼
LOOKUP PPN IMPOR:
    SELECT rate_value, rate_unit, effective_dates
    FROM tax_rates
    WHERE code = '{code}' AND tax_type = 'PPN'

    ! DISCLAIMER WAJIB:
    "Tarif yang ditampilkan adalah tarif standar dari BTKI.
    Fasilitas pembebasan, pengurangan, atau tarif khusus
    berdasarkan PMK tertentu tidak dicakup."

    │
    ▼
LOOKUP PPh PASAL 22 IMPOR:
    SELECT rate_value, rate_unit, effective_dates
    FROM tax_rates
    WHERE code = '{code}' AND tax_type = 'PPH22'

    (Disclaimer sama dengan PPN)

    │
    ▼
Output tarif lengkap dengan source_id dan disclaimer
```

---

### STEP 7 — Output & Audit Trail

```
Semua hasil dikompilasi → Classification Result

HASIL YANG DITAMPILKAN KE USER:
┌─────────────────────────────────────────────────────┐
│  KODE HS: 8471.30.20                                │
│  URAIAN: Laptop termasuk notebook dan subnotebook   │
│  CONFIDENCE: TINGGI                                 │
├─────────────────────────────────────────────────────┤
│  TARIF & PAJAK                                      │
│  Bea Masuk MFN : 0%                                 │
│  FTA ASEAN     : 0% [referensi, bukan kepastian]    │
│  PPN           : 11% [tarif standar BTKI]           │
│  PPh 22        : 10% [tarif standar BTKI]           │
├─────────────────────────────────────────────────────┤
│  REASONING CHAIN                                    │
│  [KUM HS 1] Fakta cocok dengan deskripsi heading... │
│  [KUM HS 6] Subpos 8471.30 dipilih karena...        │
├─────────────────────────────────────────────────────┤
│  KANDIDAT DIELIMINASI                               │
│  8525 → dikecualikan oleh Catatan 3 Bagian XVI...   │
│  8528 → display bukan fungsi utama...               │
├─────────────────────────────────────────────────────┤
│  FAKTA PERLU KONFIRMASI (jika ada)                  │
│  • Konfirmasi berat unit ≤ 10 kg                    │
└─────────────────────────────────────────────────────┘

AUDIT TRAIL (tersimpan di database):
- classification_cases:
    user_id, version_id, facts_json, final_code,
    final_description, confidence, status, created_at

- reasoning_events (satu record per step KUM HS):
    case_id, event_order, rule_applied, timestamp,
    facts_used, candidates_before, candidates_after,
    decision, reasoning, source_ids

SETIAP source_id dapat ditelusuri ke:
    → btki_tariff_lines.source_id
    → explanatory_notes.source_id
    → chapter_notes.source_id
    → ... yang merujuk ke:
       file checksum : SHA256 dokumen Word asli
       halaman       : nomor halaman di Word
       paragraf      : index paragraf
       sel tabel     : table/row/cell index
```

---

## Alur Iteratif — Ketika Fakta Tidak Cukup

```
User submit input
    │
    ▼
Fact Engine → ada fakta kritis yang UNKNOWN
    │
    ▼
System tanya satu pertanyaan (gain tertinggi)
    │
    ▼
User jawab
    │
    ├─ FactSet diupdate
    ├─ Information gain dihitung ulang
    └─ Lanjut ke pertanyaan berikutnya atau ke Candidate Generation

Contoh dialog:
  System: "Apakah unit ini memiliki keyboard terintegrasi?"
  User:   "Ya"
  System: "Berapa berat unitnya?"
  User:   "1.4 kg"
  System: → [Klasifikasi: 8471.30.20, TINGGI]
```

---

## Ringkasan Peran LLM vs Database

| Komponen | Dijalankan oleh | Keterangan |
|----------|----------------|------------|
| Ekstrak fakta dari teks bebas | LLM | Identifikasi, normalisasi, deteksi konflik |
| Generate pertanyaan | LLM | Teks pertanyaan yang natural |
| Apply reasoning/interpretasi teks | LLM | Helper untuk matching EN dan notes |
| Menentukan kode HS | **DATABASE** | Wajib dari btki_tariff_lines |
| Mengambil uraian resmi | **DATABASE** | Exact match, tidak diparafrase |
| Mengambil tarif | **DATABASE** | Dari mfn_rates, fta_rates, tax_rates |
| Validasi kode | **DATABASE** | Reject jika tidak ada di DB |
| Audit trail | **DATABASE** | classification_cases + reasoning_events |

> **Aturan utama:** LLM boleh membaca dan menginterpretasi data dari database, tapi **tidak boleh menghasilkan** kode HS, uraian, atau angka tarif dari memorinya sendiri.

---

## Status & Kode Kembalian

```
DETERMINED          → Kode HS berhasil ditentukan
NEED_CLARIFICATION  → Ada fakta kritis yang kurang, pertanyaan dikirim ke user
NEED_EXPERT_REVIEW  → Kasus terlalu kompleks, butuh review manual
VALIDATION_FAILED   → Kode tidak lolos validasi database
ERROR               → Error teknis (koneksi DB, parsing, dll)
```

---

*Dokumen ini adalah panduan teknis untuk implementasi AI workflow di HS Classification V2.*  
*Setiap perubahan pada alur KUM HS harus direview oleh ahli kepabeanan.*
