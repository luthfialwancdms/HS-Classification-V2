# MASTER PROMPT — MEMBANGUN AI HS CLASSIFICATION INTELLIGENCE V2

Salin seluruh prompt ini ke Codex atau mesin AI coding lain, kemudian berikan akses ke repository aplikasi V1 serta file/database BTKI, KUM HS, Catatan Hukum, dan Explanatory Notes yang tersedia.

Repository yang sedang terbuka adalah aplikasi AI HS Classification 
Intelligence V1.

Sumber database tersedia dalam workspace:
1. BTKI lengkap dalam Microsoft Word;
2. Explanatory Notes lengkap dalam Microsoft Word.

Baca MASTER PROMPT berikut sampai selesai. Mulai dengan audit repository,
audit struktur kedua database Word, gap report V1 ke V2, dan implementation
plan. Setelah audit selesai, lanjutkan Milestone 1 secara bertahap.

Pertahankan fitur V1 yang masih berfungsi. Jangan menghapus perubahan existing,
jangan hard-code contoh barang, dan jangan menghasilkan kode atau uraian HS
dari pengetahuan model.

[PASTE MASTER PROMPT DI SINI]
---

## PERAN ANDA


Anda bertugas meningkatkan aplikasi **AI HS Classification Intelligence V1** menjadi **V2** yang dapat menghasilkan rekomendasi kode HS/BTKI delapan digit berdasarkan fakta objektif barang, struktur BTKI, KUM HS 1–6, Catatan Bagian, Catatan Bab, ketentuan yang berkaitan dengan Pos, Catatan Subpos, serta Explanatory Notes.

Sistem harus dapat menjelaskan, membuktikan, dan merekonstruksi seluruh proses klasifikasi. Sistem bukan sekadar semantic search atau chatbot yang menebak kode.

---

## TUJUAN PRODUK V2

Bangun aplikasi yang menjalankan alur:

```text
Input pengguna dan dokumen barang
        ↓
Ekstraksi serta normalisasi fakta objektif
        ↓
Validasi kelengkapan dan konflik fakta
        ↓
Pembentukan kandidat Pos dari database BTKI
        ↓
Pengujian Catatan Bagian, Catatan Bab, dan ketentuan terkait Pos
        ↓
Pengujian EN untuk setiap kandidat Pos
        ↓
Penerapan KUM HS 1–5 secara berurutan dan kondisional
        ↓
Penetapan Pos empat digit
        ↓
Pengujian Catatan Subpos dan KUM HS 6
        ↓
Penurunan kode 4 → 6 → 8 digit
        ↓
Validasi kode dan uraian exact terhadap database BTKI aktif
        ↓
Confidence, classification risk, KADL, dan audit trail
```

Fase V2 ini hanya mencakup:

- BTKI;
- KUM HS 1–6;
- Catatan Bagian;
- Catatan Bab;
- ketentuan hukum yang berkaitan dengan Pos;
- Catatan Subpos;
- Explanatory Notes;
- rekomendasi kode HS/BTKI;
- confidence;
- classification risk;
- legal reasoning; dan
- audit trail.

Setelah kode BTKI delapan digit tervalidasi, V2 juga harus mengambil dari database BTKI yang sama:

- tarif MFN Bea Masuk;
- tarif FTA/preferensi berdasarkan skema atau negara mitra yang tersedia dalam BTKI;
- tarif PPN impor standar yang tercantum;
- tarif PPh Pasal 22 impor standar yang tercantum; dan
- periode berlaku serta sumber tarif tersebut.

Tarif merupakan hasil lookup berdasarkan kode final, tanggal berlaku, dan skema yang dipilih. LLM dilarang mengarang atau menghitung tarif dari ingatan. Untuk FTA, bedakan dengan tegas antara **tarif preferensi yang tersedia** dan **kelayakan pemanfaatan FTA**. Penentuan entitlement dapat memerlukan negara asal, skema, Rules of Origin, Certificate/Proof of Origin, direct consignment, dan persyaratan prosedural lainnya. Jangan otomatis menyatakan tarif FTA dapat digunakan hanya karena negara dipilih.

Fase ini belum mencakup:

- larangan dan pembatasan Kementerian/Lembaga;
- pemetaan dan keputusan lartas per Kementerian/Lembaga;
- perlakuan khusus, fasilitas, pengecualian, pembebasan, atau tarif khusus PPN berdasarkan PMK/peraturan di luar tarif standar BTKI;
- perlakuan khusus, fasilitas, pengecualian, pembebasan, atau tarif khusus PPh Pasal 22 berdasarkan PMK/peraturan di luar tarif standar BTKI;
- validasi substansi dan prosedur pemenuhan Rules of Origin/FTA secara penuh, kecuali data dan rules module khusus telah disediakan;
- PPnBM apabila tidak tersedia atau belum dipetakan di dalam sumber BTKI yang diimpor;
- trade remedies; atau
- perlakuan fiskal lainnya.

Desain arsitektur harus memisahkan `classification result`, `standard tariff lookup`, `FTA rate reference`, dan `special regulatory/fiscal treatment`, sehingga lartas serta perlakuan khusus berdasarkan PMK dapat ditambahkan pada fase berikutnya tanpa mengubah core classification engine.

---

## PRINSIP NON-NEGOTIABLE

1. **LLM tidak boleh menciptakan kode HS.**
2. **LLM tidak boleh menciptakan uraian BTKI.**
3. **LLM tidak boleh menciptakan Catatan Bagian, Catatan Bab, Catatan Subpos, atau ketentuan hukum lainnya.**
4. **LLM tidak boleh mengutip EN dari ingatan model.**
5. Semua kode, uraian, Catatan, dan EN harus berasal dari database yang tersedia dan mempunyai `source_id`.
6. Kode final hanya dapat dipilih dari `tariff_node` yang aktif pada tanggal klasifikasi.
7. Uraian resmi harus diambil server-side dari database setelah kode dipilih; jangan menerima uraian free-text dari LLM sebagai uraian resmi.
8. Jika kode tidak ditemukan, tidak aktif, bukan delapan digit, parent hierarchy tidak lengkap, atau uraian tidak sama dengan master, hasil harus ditolak.
9. Jika fakta penentu belum tersedia, sistem harus berhenti dan meminta klarifikasi; jangan menebak.
10. Confidence dihitung oleh backend berdasarkan formula dan caps; LLM dilarang menentukan confidence sendiri.
11. KUM HS diterapkan secara berurutan dan kondisional. Jangan memaksakan seluruh KUM HS pada setiap barang.
12. EN merupakan alat bantu interpretasi. EN tidak boleh mengalahkan uraian Pos, Catatan Bagian, Catatan Bab, atau Catatan Subpos.
13. Nama dagang, invoice description, HS dari supplier, keputusan sebelumnya, dan semantic similarity merupakan evidence awal, bukan dasar hukum final.
14. Seluruh keputusan dan perubahan harus tercatat dalam audit trail.
15. Jangan hard-code laptop, dumper, crane barge, atau contoh tertentu. Engine harus berlaku umum untuk seluruh kondisi barang.

---

## KLARIFIKASI STRUKTUR CATATAN HUKUM

Dalam struktur hukum HS/BTKI, kategori sumber legal yang harus dibedakan adalah:

1. Catatan Bagian;
2. Catatan Bab; dan
3. Catatan Subpos.

Tidak perlu membuat kategori legal baru bernama **Catatan Pos** apabila sumber aslinya tidak mempunyai kategori tersebut.

Jika suatu Catatan Bagian atau Catatan Bab berbunyi, misalnya, “untuk keperluan Pos 04.03”, simpan sebagai:

```json
{
  "source_type": "CHAPTER_NOTE",
  "scope_chapter": "04",
  "target_heading": "0403",
  "ui_label": "Ketentuan terkait Pos 04.03"
}
```

Dengan demikian:

- `source_type` tetap mengikuti sumber hukum aslinya;
- `target_heading` menunjukkan Pos yang dituju; dan
- UI boleh menampilkan kelompok **Ketentuan Terkait Pos** untuk memudahkan pengguna.

Penjelasan teknis per Pos dari Explanatory Notes harus disimpan sebagai `EN_HEADING_NOTE`, bukan sebagai Catatan Hukum BTKI.

---

## LANGKAH PERTAMA: AUDIT REPOSITORY V1

Sebelum mengubah kode:

1. Baca seluruh repository V1.
2. Identifikasi framework frontend, backend, database, API, deployment, authentication, prompt, dan model yang digunakan.
3. Temukan lokasi rekomendasi HS dibuat.
4. Temukan apakah kode dan uraian berasal dari database atau respons generatif LLM.
5. Petakan seluruh form, button, route, state, dan output V1.
6. Identifikasi data yang sudah tersedia dan data yang masih berupa hard-coded example.
7. Periksa test suite, lint, type checking, logging, dan error handling.
8. Periksa status git serta pertahankan perubahan milik user yang tidak terkait.
9. Jangan mengganti stack tanpa alasan teknis yang kuat.
10. Jangan langsung menulis ulang aplikasi.

Hasil tahap audit harus berupa:

- arsitektur V1;
- data flow V1;
- gap V1 → V2;
- risiko hallucination;
- daftar komponen yang dapat dipertahankan;
- daftar komponen yang harus diubah;
- proposed architecture V2;
- daftar migration;
- implementation milestone;
- daftar file yang akan disentuh;
- testing plan; dan
- rollback plan.

Setelah gap report selesai, lanjutkan implementasi secara bertahap tanpa menunggu instruksi tambahan selama perubahan masih berada dalam scope ini dan aman dilakukan.

---

## DATABASE INGESTION: BTKI, KUM HS, CATATAN, DAN EN

### A. Sumber database

**Sumber utama database BTKI dan Explanatory Notes yang diberikan pemilik produk adalah file Microsoft Word.** Perlakukan DOCX sebagai primary ingestion format. Sistem harus dapat mengambil dan mengimpor database tambahan melalui:

1. Upload file dari administrator;
2. Folder atau storage internal yang ditentukan melalui configuration;
3. Database existing;
4. API internal;
5. CSV;
6. XLSX;
7. JSON;
8. XML;
9. DOCX/Microsoft Word sebagai format sumber utama;
10. searchable PDF; dan
11. scanned PDF melalui OCR jika benar-benar diperlukan.

Prioritaskan structured data. PDF/DOCX parsing digunakan apabila structured data tidak tersedia.

Untuk fase pertama, prioritaskan parser DOCX yang deterministic. Jangan mengekstrak BTKI atau EN hanya dengan mengirim seluruh Word kepada LLM. Gunakan OOXML/document parser untuk memperoleh struktur; LLM hanya membantu normalisasi bagian yang ambigu setelah parser menghasilkan evidence dan lokasi sumber.

Jika file masih berformat `.doc` lama, lakukan conversion terkontrol menjadi `.docx`, simpan checksum kedua file, catat conversion tool/version, dan lakukan visual comparison sebelum import. Jangan menggunakan `.doc` hasil konversi tanpa validation report.

### A1. DOCX ingestion pipeline khusus BTKI

Parser BTKI Word harus mampu membaca dan mempertahankan:

- urutan dokumen;
- Bagian/Section;
- Bab/Chapter;
- Pos empat digit;
- Subpos enam digit;
- Pos Tarif delapan digit;
- simbol dash dan tingkat indentasi;
- uraian Bahasa Indonesia;
- uraian Bahasa Inggris apabila tersedia;
- satuan;
- tarif MFN Bea Masuk;
- tarif FTA/preferensi per skema/negara apabila terdapat dalam sumber;
- PPN;
- PPh Pasal 22;
- footnote;
- effective date;
- Catatan Bagian;
- Catatan Bab;
- Catatan Subpos;
- Catatan Bagian/Bab yang menarget Pos tertentu;
- page number dan lokasi sumber untuk audit.

Parser tidak boleh mengandalkan tampilan visual saja. Baca OOXML berikut bila relevan:

- paragraph style;
- outline level;
- numbering definition;
- indentation/hanging indentation;
- tabs;
- table rows/cells;
- merged cells;
- bookmarks;
- footnotes/endnotes;
- headers/footers;
- section breaks;
- tracked changes;
- hidden text; dan
- hyperlink/cross-reference.

Bangun rule-based mapping dari struktur Word menjadi hierarchy tree. Jika code, dash, indentasi, dan parent hierarchy memberikan sinyal berbeda, tandai `STRUCTURE_AMBIGUITY` dan masukkan ke review queue; jangan menebak parent.

### A2. DOCX ingestion pipeline khusus EXPLANATORY NOTES (EN)

Parser EXPLANATORY NOTES (EN) dalam format MS Word harus mampu membaca dan mempertahankan:

- edition;
- Bagian;
- Bab;
- heading/Pos EN;
- subheading bila tersedia;
- heading title;
- nomor paragraf;
- subjudul;
- scope statement;
- definition;
- inclusion;
- exclusion;
- technical characteristics;
- example;
- borderline guidance;
- cross-reference ke Pos/Bab lain;
- tabel;
- daftar/bullet;
- gambar/caption jika mempunyai nilai interpretatif;
- Bahasa Inggris dan terjemahan bila tersedia;
- page/paragraph locator; dan
- source checksum.

Pecah EXPLANATORY NOTES (EN) berdasarkan heading dan paragraph semantic unit, bukan fixed character count semata. Jangan memotong satu exclusion atau cross-reference menjadi chunk yang kehilangan konteks. Simpan `previous_paragraph_id`, `next_paragraph_id`, `parent_section_id`, dan `referred_codes`.

### A3. DOCX visual-verification workflow

Untuk setiap file Word yang diimpor:

1. Simpan file original read-only dan checksum;
2. Ekstrak OOXML secara deterministic;
3. Render Word menjadi PDF/PNG untuk quality verification;
4. Bandingkan sampled pages dengan parsed records;
5. Tampilkan side-by-side source paragraph/table dan hasil mapping pada Admin Data Center;
6. Jalankan row/code count reconciliation;
7. Jalankan heading/subheading hierarchy reconciliation;
8. Jalankan Catatan-scope reconciliation;
9. Jalankan EN heading coverage report;
10. Wajibkan approval Data Admin atas ambiguity/error sebelum activation.

Setiap record hasil parsing harus dapat menavigasi kembali ke:

```text
source_file_id
source_checksum
document_part
page_number_if_available
paragraph_index
table_index
row_index
cell_index
OOXML element reference/bookmark jika tersedia
```

### A4. Tracked changes dan teks non-final

- Deteksi tracked insertions/deletions;
- Jangan diam-diam menggabungkan deleted text ke database aktif;
- Default gunakan accepted/final view hanya setelah dikonfirmasi;
- Laporkan comments, unresolved revisions, hidden text, dan conflicting versions;
- Sediakan preview sebelum commit.

Jangan melakukan scraping sumber EN atau sumber berlisensi tanpa otorisasi. Jika database EN belum tersedia pada repository atau storage yang diberikan:

- bangun import adapter dan admin ingestion workflow;
- tampilkan data contract yang dibutuhkan;
- tandai status `EXPLANATORY NOTES (EN)_DATA_NOT_LOADED`;
- jangan mengarang isi EXPLANATORY NOTES (EN); dan
- minta operator memasukkan sumber yang sah.

### B. Admin Data Center

Buat menu **Admin Data Center** dengan button:

- `Import BTKI`;
- `Import KUM HS`;
- `Import Catatan Hukum`;
- `Import Explanatory Notes`;
- `Tambah Versi`;
- `Validasi Dataset`;
- `Preview Mapping`;
- `Jalankan Dry Run`;
- `Commit Import`;
- `Batalkan Import`;
- `Lihat Error`;
- `Lihat Duplicate`;
- `Lihat Orphan Node`;
- `Bandingkan Versi`;
- `Aktifkan Versi`;
- `Nonaktifkan Versi`;
- `Rollback Versi`; dan
- `Rebuild Search Index`.

Tambahkan khusus Word:

- `Preview Dokumen Word`;
- `Lihat Struktur OOXML`;
- `Bandingkan Word vs Hasil Parsing`;
- `Review Ambiguous Hierarchy`;
- `Review Tracked Changes`;
- `Review Merged Cells`;
- `Reconcile Jumlah Kode`;
- `Reconcile Catatan`;
- `Reconcile Coverage EXPLANATORY NOTES (EN)`; dan
- `Approve DOCX Mapping`.

### C. Import pipeline

Import harus melalui state:

```text
UPLOADED
→ PARSING
→ MAPPED
→ VALIDATING
→ DRY_RUN_PASSED / DRY_RUN_FAILED
→ READY_TO_COMMIT
→ COMMITTED
→ INDEXED
→ ACTIVE
```

Setiap import harus:

1. Menghitung checksum file;
2. Menyimpan nama, versi, edition, tanggal berlaku, dan sumber;
3. Menyimpan parser version;
4. Menyimpan mapping report;
5. Menyimpan row-level error;
6. Mendeteksi duplicate;
7. Mendeteksi kode tidak valid;
8. Mendeteksi parent yang hilang;
9. Mendeteksi indentasi yang tidak konsisten;
10. Mendeteksi tanggal berlaku yang overlap;
11. Mendeteksi uraian kosong;
12. Mendeteksi Catatan tanpa scope;
13. Mendeteksi EN paragraph tanpa heading;
14. Menjalankan dry run;
15. Memerlukan approval administrator sebelum aktif; dan
16. Dapat di-rollback tanpa menghapus audit record.

### D. Normalisasi kode

Simpan kode tanpa titik sebagai canonical code dan sediakan formatted code untuk UI:

```text
Canonical: 84713020
Formatted: 8471.30.20
```

Jangan mengubah leading zero.

Validasi tingkat kode:

```text
2 digit  = Chapter
4 digit  = Heading/Pos
6 digit  = HS Subheading
8 digit  = AHTN/BTKI Tariff Line
```

Bab 98 dan 99 dapat mempunyai perlakuan nasional khusus dan harus tetap didukung melalui metadata, bukan hard-coded branching yang tersebar.

---

## DATA MODEL MINIMUM

Implementasikan atau adaptasikan tabel/collection berikut.

### 1. `nomenclature_version`

```text
id
name
jurisdiction
hs_edition
ahtn_edition
btki_edition
effective_from
effective_to
regulation_number
source_document_id
status
created_at
activated_at
```

### 2. `tariff_node`

```text
id
nomenclature_version_id
canonical_code
formatted_code
digits
node_type
parent_code
section_number
chapter_number
heading_code
subheading_code
indent_level
official_description_id
official_description_en
effective_from
effective_to
active
source_id
```

### 2A. `tariff_rate`

```text
id
nomenclature_version_id
tariff_node_id
rate_type: MFN_BM | FTA_BM | PPN_IMPORT | PPH22_IMPORT
scheme_code
partner_country_code
rate_value
rate_unit: PERCENT | SPECIFIC | COMPOUND
currency_if_specific
quantity_unit_if_specific
formula_if_compound
effective_from
effective_to
conditions_summary
source_id
```

Simpan tarif FTA per skema/negara sesuai struktur sumber. Jangan menimpa MFN dengan FTA. Satu kode dapat memiliki beberapa rate record yang berlaku untuk skema berbeda.

### 3. `legal_note`

```text
id
nomenclature_version_id
source_type: SECTION_NOTE | CHAPTER_NOTE | SUBHEADING_NOTE
section_number
chapter_number
target_codes[]
note_number
paragraph_number
text_id
text_en
effect_type: DEFINITION | INCLUSION | EXCLUSION | CONDITION | REFERRAL | PRIORITY
referred_codes[]
effective_from
effective_to
source_id
```

### 4. `explanatorynotes(en)_document`

```text
id
edition
language
title
source_file_id
license_metadata
checksum
status
```

### 5. `explanatorynotes(en)_paragraph`

```text
id
explanatorynotes(en)_document_id
heading_code
subheading_code_if_available
section_path
paragraph_number
paragraph_text
paragraph_type: SCOPE | DEFINITION | INCLUSION | EXCLUSION | TECHNICAL_CHARACTERISTIC | EXAMPLE | BORDERLINE | CROSS_REFERENCE
referred_codes[]
embedding
source_id
```

### 6. `kum_hs_rule`

```text
id
rule_code: GRI_1 | GRI_2A | GRI_2B | GRI_3A | GRI_3B | GRI_3C | GRI_4 | GRI_5A | GRI_5B | GRI_6
official_text_id
official_text_en
trigger_schema
evaluation_schema
edition
source_id
```

### 7. `fact_definition`

```text
fact_key
label
description
datatype
unit_dimension
allowed_values
validation_schema
applicable_product_domains[]
legal_relevance
```

### 8. `decision_requirement`

```text
id
candidate_code
fact_key
condition
mandatory
threshold
unit
legal_source_ids[]
question_template
priority
```

### 9. `classification_case`

```text
id
tenant_id
user_id
classification_date
nomenclature_version_id
status
commercial_identity
legal_identity
input_language
created_at
updated_at
```

### 10. `fact_observation`

```text
id
case_id
fact_key
raw_value
normalized_value
unit
source_type: USER | DOCUMENT | EXPERT | SYSTEM
evidence_id
confidence
verification_status
```

### 11. `candidate_evaluation`

```text
id
case_id
candidate_code
candidate_level
generation_source
matched_facts[]
missing_facts[]
conflicting_facts[]
legal_note_results[]
en_results[]
rule_stage
decision: RETAINED | ELIMINATED | UNRESOLVED
reason
source_ids[]
```

### 12. `reasoning_event`

```text
id
case_id
sequence_number
event_type
rule_code
candidate_set_before
candidate_set_after
input_fact_ids[]
source_ids[]
structured_result
actor_type
actor_id
engine_version
model_version_if_any
prompt_version_if_any
created_at
previous_event_hash
event_hash
```

### 13. `expert_correction`

```text
id
case_id
target_type
target_id
before_value
after_value
reason
evidence_ids[]
reviewer_id
approval_status
created_at
```

Sesuaikan nama tabel dengan konvensi repository, tetapi pertahankan kemampuan datanya.

---

## PEMBAGIAN TUGAS LLM, RETRIEVAL, RULES ENGINE, DAN VALIDATOR

### LLM boleh melakukan

- Ekstraksi fakta dari deskripsi dan dokumen;
- Normalisasi terminologi;
- Deteksi kemungkinan kontradiksi untuk diverifikasi;
- Pembentukan legal identity sementara;
- Pembentukan query pencarian kandidat;
- Ranking awal kandidat;
- Penyusunan pertanyaan klarifikasi;
- Penyusunan narasi KADL dari structured reasoning events; dan
- Penerjemahan penjelasan.

### LLM dilarang melakukan

- Membuat kode yang tidak diberikan retrieval;
- Membuat uraian resmi;
- Membuat Catatan Hukum;
- Mengutip EXPLANATORY NOTES (EN) dari memori;
- Menentukan bahwa fakta yang hilang “kemungkinan” mempunyai nilai tertentu;
- Memilih kode final tanpa deterministic validator;
- Menetapkan confidence sendiri;
- Menyembunyikan kandidat yang bertentangan; atau
- Mengubah source text.

### Retrieval engine bertugas

- Mengambil node BTKI yang relevan;
- Mengambil ancestor dan child node;
- Mengambil Catatan Bagian/Bab/Subpos yang scope-nya relevan;
- Mengambil ketentuan yang secara eksplisit menarget Pos;
- Mengambil EXPLANATORY NOTES (EN) per kandidat Pos;
- Mengambil cross-reference; dan
- Mengembalikan source IDs yang dapat divalidasi.

### Rules engine bertugas

- Menjalankan trigger KUM HS;
- Menentukan urutan rule;
- Menentukan candidate state;
- Menerapkan exclusion dan referral;
- Mengendalikan stop condition;
- Mengunci Pos empat digit;
- Mengendalikan same-level subheading comparison;
- Menghitung confidence caps; dan
- Menghasilkan structured reasoning events.

### Validator bertugas

- Exact code validation;
- Exact official description lookup;
- Active-date validation;
- Parent hierarchy validation;
- Indentation validation;
- Same-level comparison validation;
- Source ID validation;
- Output schema validation; dan
- Status eligibility validation.

---

## FACT EXTRACTION DAN FACT GATE

Sistem harus mengidentifikasi sekurang-kurangnya:

- nama dagang;
- nama generik;
- brand/model;
- material;
- komposisi/ingredients;
- persentase/konsentrasi;
- bentuk;
- dimensi;
- berat;
- kapasitas;
- daya/tegangan;
- proses pembuatan;
- fungsi utama;
- fungsi sekunder;
- prinsip kerja;
- input-process-output;
- sumber tenaga;
- penggunaan utama;
- area operasi;
- mobility;
- mounting;
- complete/incomplete;
- finished/unfinished;
- assembled/unassembled;
- CKD/SKD;
- satuan/bulk;
- mixture/composite;
- set/kit;
- part/accessory;
- used;
- waste/scrap;
- packaging/container; dan
- dokumen pendukung.

Setiap fakta harus mempunyai:

- nilai;
- unit jika relevan;
- sumber;
- evidence;
- verification status; dan
- confidence ekstraksi.

Bedakan:

```text
UNKNOWN       = belum diberikan
NOT_APPLICABLE = tidak relevan
ZERO          = nilainya memang nol
CONFLICTING   = terdapat dua nilai berbeda
VERIFIED      = telah dikonfirmasi
```

Jangan menyamakan field kosong dengan tidak relevan.

Jika fakta wajib atau fakta pembeda kandidat hilang, return:

```json
{
  "status": "NEED_CLARIFICATION",
  "missing_facts": [],
  "questions": [],
  "candidate_impact": []
}
```

Urutkan pertanyaan berdasarkan information gain: tanyakan fakta yang paling cepat mengeliminasi kandidat terlebih dahulu.

---

## PEMBENTUKAN KANDIDAT

Candidate generation harus menggunakan kombinasi:

1. Exact lexical search;
2. Synonym/terminology mapping;
3. Multilingual search Indonesia–Inggris;
4. Semantic search;
5. Function search;
6. Material search;
7. Composition search;
8. Process search;
9. Form search;
10. Use/principal-use search;
11. Parent-machine search;
12. Legal-note referral;
13. EN cross-reference; dan
14. Similar/akin goods hanya untuk kandidat KUM HS 4.

Semantic similarity hanya menghasilkan kandidat. Semantic similarity tidak boleh menjadi dasar final.

Untuk setiap kandidat, ambil:

- official heading description;
- hierarchy;
- applicable Section Notes;
- applicable Chapter Notes;
- notes targeting that heading;
- applicable Subheading Notes;
- EN heading paragraphs;
- child subheadings;
- required discriminator facts; dan
- cross-referenced headings.

---

## URUTAN PENAFSIRAN KUM HS 1–6

### TAHAP 0 — VALIDASI REFERENSI

1. Tentukan tanggal klasifikasi atau tanggal impor.
2. Pilih nomenclature version yang aktif pada tanggal tersebut.
3. Pastikan database BTKI, KUM HS, Catatan, dan EN tersedia.
4. Jika versi tidak dapat ditentukan, status `REFERENCE_VERSION_REQUIRED`.

### KUM HS 1 — WAJIB SELALU

Untuk setiap kandidat Pos empat digit:

1. Bandingkan fakta objektif dengan uraian Pos;
2. Terapkan Catatan Bagian;
3. Terapkan Catatan Bab;
4. Terapkan definition;
5. Terapkan inclusion;
6. Terapkan exclusion;
7. Terapkan condition;
8. Terapkan referral;
9. Gunakan EN kandidat untuk menjelaskan scope, karakteristik, inclusion, exclusion, dan borderline;
10. Catat seluruh source IDs.

Urutan operasional:

```text
Heading terms
→ Section Notes
→ Chapter Notes
→ notes targeting the heading
→ EN assistance
→ candidate result
```

Jika satu Pos sudah ditentukan oleh KUM HS 1, jangan memaksakan KUM HS 2–4.

### KUM HS 2(a)

Trigger hanya jika barang:

- incomplete;
- unfinished;
- unassembled;
- disassembled;
- CKD;
- SKD; atau
- blank.

Uji apakah barang saat disajikan telah mempunyai essential character barang lengkap atau selesai.

Pertanyaan minimum:

- Komponen/proses apa yang belum tersedia?
- Apakah identitas barang lengkap sudah dapat dikenali?
- Apakah fungsi utama sudah terbentuk?
- Seberapa besar pekerjaan lanjutan?
- Apakah seluruh komponen diajukan bersama?
- Apakah terurai hanya untuk pengangkutan/perakitan?

Output rule:

```text
TREAT_AS_COMPLETE
CLASSIFY_AS_PRESENTED
NEED_CLARIFICATION
```

### KUM HS 2(b)

Trigger hanya jika terdapat:

- campuran bahan/zat;
- kombinasi material;
- barang seluruh atau sebagian dari material tertentu; atau
- perluasan referensi suatu material dalam Pos.

Jika perluasan tersebut menimbulkan dua atau lebih kandidat Pos, lanjutkan ke KUM HS 3.

### KUM HS 3(a)

Trigger jika barang prima facie dapat diklasifikasikan dalam dua atau lebih Pos.

Uji:

- Pos spesifik versus Pos umum;
- uraian barang lengkap versus residual heading;
- apakah masing-masing Pos hanya merujuk sebagian material/komponen/item dalam mixture, composite goods, atau retail set.

Jika dua atau lebih Pos masing-masing hanya merujuk sebagian material/komponen/item, perlakukan sebagai equally specific dan lanjutkan ke KUM HS 3(b).

### KUM HS 3(b)

Trigger jika KUM HS 3(a) belum menyelesaikan dan barang merupakan:

- mixture;
- composite goods;
- barang yang terdiri dari komponen berbeda; atau
- set untuk penjualan eceran.

Retail set test harus memeriksa:

1. Minimal dua barang berbeda yang prima facie masuk Pos berbeda;
2. Disatukan untuk memenuhi kebutuhan tertentu atau melakukan aktivitas tertentu; dan
3. Disiapkan untuk penjualan langsung tanpa repacking.

Essential character test dapat mempertimbangkan:

- fungsi;
- peran komponen;
- kebutuhan terhadap penggunaan barang;
- material;
- bulk;
- quantity;
- weight;
- value;
- durability;
- consumer perception; dan
- contribution terhadap identity.

Jangan memilih satu faktor secara otomatis. Simpan faktor, bobot operasional, evidence, dan alasan.

Jika essential character tidak dapat ditentukan, lanjut KUM HS 3(c).

### KUM HS 3(c)

Trigger hanya jika 3(a) dan 3(b) gagal.

Pilih Pos yang terakhir secara numerik hanya di antara kandidat yang:

- sama-sama layak;
- telah lulus KUM HS 1; dan
- benar-benar mempunyai pertimbangan setara.

Jangan memasukkan kandidat lemah hanya agar kode numeriknya menjadi paling akhir.

### KUM HS 4

Trigger hanya jika barang tidak dapat diklasifikasikan berdasarkan KUM HS 1–3.

Most akin test harus membandingkan:

- material;
- form;
- function;
- operating principle;
- use;
- technical characteristics;
- commercial identity; dan
- relationship terhadap barang pembanding.

Tampilkan barang pembanding dan alasan keserupaannya. Untuk risiko tinggi, wajib expert review.

### KUM HS 5(a)

Trigger untuk case/container yang:

- dibentuk khusus atau pas untuk barang tertentu;
- cocok untuk penggunaan jangka panjang;
- diajukan bersama barang;
- lazim dijual bersama barang; dan
- tidak memberikan keseluruhan essential character.

Output: classified with article atau separately.

### KUM HS 5(b)

Trigger untuk packing material dan packing container.

Uji:

- diajukan bersama barang;
- lazim digunakan untuk membungkus barang tersebut; dan
- tidak secara nyata cocok untuk penggunaan berulang.

Output: classified with goods atau separately.

### PENETAPAN POS EMPAT DIGIT

Setelah KUM HS 1–5 selesai:

1. Pastikan hanya satu Pos empat digit retained;
2. Simpan seluruh eliminated candidates;
3. Simpan alasan dan source IDs;
4. Lock Pos;
5. Jangan lagi membandingkan dengan Pos lain pada tahap KUM HS 6, kecuali analisis di-reset karena fakta berubah.

### KUM HS 6

KUM HS 6 diterapkan setelah Pos empat digit dikunci.

1. Ambil child subheadings langsung dari Pos;
2. Bandingkan hanya subpos pada tingkat indentasi yang sama;
3. Gunakan uraian subpos;
4. Gunakan Catatan Subpos;
5. Gunakan Catatan Bagian dan Catatan Bab yang masih relevan;
6. Terapkan KUM HS 1–5 secara mutatis mutandis;
7. Pilih subpos enam digit;
8. Turunkan ke tariff line delapan digit dengan struktur BTKI/AHTN;
9. Uji threshold pembeda, seperti berat, kapasitas, daya, ukuran, komposisi, konsentrasi, retail/bulk, CKD/non-CKD, dan penggunaan;
10. Validasi same-level comparison pada setiap branch;
11. Validasi final code terhadap master aktif.

Jika fakta pembeda subpos tidak tersedia, jangan berhenti pada kode empat atau enam digit sebagai final. Return `NEED_CLARIFICATION` dan tampilkan intermediate classification secara jelas sebagai **bukan rekomendasi final**.

---

## LOGIC KONDISI BARANG

Implementasikan conditional module berikut:

### Incomplete/unfinished

- missing components;
- remaining manufacturing process;
- function already available;
- essential character evidence.

### CKD/SKD/unassembled

- component manifest;
- same shipment versus split shipment;
- number of units;
- assembly operation;
- essential identity.

### Mixture

- all substances;
- percentage by weight/volume;
- concentration;
- active ingredients;
- purpose of addition;
- essential character.

### Composite goods

- components;
- separability;
- role;
- function;
- weight;
- value;
- essential character.

### Retail set

- item list;
- individual headings;
- single need/activity;
- retail packaging;
- repacking requirement;
- essential character.

### Part

- parent machine;
- sole/principal use;
- dedicated versus general use;
- independent heading;
- parts of general use;
- legal-note exclusion;
- ability to work independently.

### Accessory

- enhancement function;
- necessity;
- standalone use;
- dedicated use;
- independent heading;
- legal-note exclusion.

### Multifunction/composite machine

- all functions;
- simultaneous/alternative operation;
- principal function;
- Section XVI Notes;
- functional unit relationship;
- independent components.

### Bulk

- net weight/volume;
- physical state;
- concentration;
- packaging;
- industrial versus retail presentation.

### Used goods/waste/scrap

- remaining function;
- repairability;
- intended recovery;
- damage;
- contamination;
- scrap criteria;
- relevant Legal Notes.

### Packaging/container

- specially shaped;
- durable;
- normally sold with goods;
- repetitive use;
- contribution to essential character.

---

## UI V2

Bangun wizard berikut.

### STEP 1 — REFERENSI KLASIFIKASI

Field:

- Tanggal impor/ekspor;
- Tujuan klasifikasi;
- Nomenclature version;
- BTKI version;
- EN edition;
- Bahasa.

Button:

- `Konfirmasi Referensi`;
- `Lihat Peraturan`;
- `Bandingkan Versi`;
- `Kembali`;
- `Lanjut`.

### STEP 2 — IDENTITAS BARANG DAN DOKUMEN

Field:

- Nama dagang;
- Nama generik;
- Brand/model;
- Manufacturer;
- SKU/part number;
- Invoice description;
- HS supplier sebagai referensi;
- Keterangan tambahan.

Button:

- `Upload Invoice`;
- `Upload Packing List`;
- `Upload Catalogue`;
- `Upload Datasheet`;
- `Upload Drawing`;
- `Upload Foto`;
- `Upload MSDS/COA`;
- `Ekstrak Fakta`;
- `Lihat Hasil Ekstraksi`;
- `Koreksi Fakta`;
- `Lanjut`.

### STEP 3 — KONDISI SAAT DIIMPOR

Button/select:

- `Lengkap dan Selesai`;
- `Tidak Lengkap`;
- `Belum Selesai`;
- `Terurai/Unassembled`;
- `CKD`;
- `SKD`;
- `Blank`;
- `Satuan`;
- `Curah`;
- `Campuran`;
- `Barang Komposit`;
- `Retail Set/Kit`;
- `Part`;
- `Accessory`;
- `Barang Bekas`;
- `Waste/Scrap`;
- `Kemasan/Wadah Khusus`.

Jawaban dapat multi-select jika faktanya memang bersamaan.

### STEP 4 — FISIK, MATERIAL, DAN KOMPOSISI

Field/button:

- `Tambah Material`;
- `Tambah Ingredient`;
- `Tambah Komponen`;
- `Tambah Coating`;
- Persentase berat/volume;
- Konsentrasi;
- Bentuk fisik;
- Dimensi;
- Berat;
- Unit converter;
- `Tandai Tidak Diketahui`;
- `Lanjut`.

### STEP 5 — FUNGSI DAN CARA KERJA

Field/button:

- Fungsi utama;
- `Tambah Fungsi Sekunder`;
- Input;
- Process;
- Output;
- Prinsip operasi;
- Sumber tenaga;
- Standalone/depends on other machine;
- Multifunction;
- `Tambah Functional Unit`;
- `Lanjut`.

### STEP 6 — PENGGUNAAN DAN KONTEKS

Field:

- Principal use;
- Actual use;
- Industri;
- Area operasi;
- Mobilitas;
- Mounting;
- Parent machine;
- Target user.

### STEP 7 — KONDISI KHUSUS

Tampilkan panel dinamis berdasarkan STEP 3:

- Incomplete panel;
- CKD/SKD panel;
- Mixture panel;
- Composite panel;
- Retail set panel;
- Part panel;
- Accessory panel;
- Multifunction panel;
- Bulk panel;
- Used/waste panel;
- Packaging panel.

### STEP 8 — VALIDASI FAKTA

Button:

- `Validasi Fakta`;
- `Lihat Fakta Kurang`;
- `Lihat Fakta Konflik`;
- `Jawab Klarifikasi`;
- `Unggah Bukti`;
- `Konfirmasi Fakta`;
- `Bentuk Kandidat`.

### STEP 9 — KANDIDAT HS

Setiap candidate card harus menampilkan:

- kode Pos;
- official description;
- hierarchy;
- matched facts;
- missing facts;
- conflicting facts;
- applicable legal notes;
- EN match;
- generation source;
- status;
- alasan.

Button:

- `Buka Uraian BTKI`;
- `Buka Catatan Bagian`;
- `Buka Catatan Bab`;
- `Buka Ketentuan Terkait Pos`;
- `Buka EN`;
- `Pertahankan Kandidat`;
- `Tantang Kandidat`;
- `Tambahkan Kandidat`;
- `Jalankan Legal Test`.

Button `Pertahankan` atau `Tantang` dari user tidak boleh langsung mengubah hasil hukum. Input tersebut menjadi user feedback/evidence yang harus diuji engine.

### STEP 10 — LEGAL REASONING

Tabs:

- `KUM HS`;
- `Catatan Bagian`;
- `Catatan Bab`;
- `Ketentuan Terkait Pos`;
- `EN per Kandidat`;
- `Catatan Subpos`;
- `Kandidat Dieliminasi`;
- `Decision Tree`;
- `Audit Trail`.

Button:

- `Jalankan Ulang`;
- `Buka Sumber`;
- `Lihat Rule Trigger`;
- `Lihat Alasan Eliminasi`;
- `Jawab Klarifikasi`;
- `Koreksi Fakta`;
- `Minta Expert Review`.

### STEP 11 — REKOMENDASI

Tampilkan:

- Status;
- Recommended HS Code delapan digit;
- Official BTKI Description;
- Legal Identity;
- Classification path;
- Legal basis;
- Retained candidate;
- Eliminated candidates;
- Material facts;
- Missing confirmation;
- Confidence;
- Classification risk;
- Audit trail summary.

Panel `Tarif dari BTKI` harus menampilkan:

- MFN Bea Masuk;
- daftar tarif FTA/preferensi yang tersedia per skema/negara;
- PPN impor standar;
- PPh Pasal 22 impor standar;
- tanggal berlaku;
- source reference;
- status `RATE_REFERENCE_ONLY` atau `FTA_ELIGIBILITY_CONFIRMED` jika kelak rules-of-origin module tersedia; dan
- peringatan bahwa perlakuan khusus PPN/PPh 22 berdasarkan PMK belum dianalisis pada V2.

Button:

- `Lihat KADL`;
- `Ekspor DOCX`;
- `Ekspor PDF`;
- `Ekspor JSON`;
- `Minta Expert Review`;
- `Setujui Rekomendasi`;
- `Tolak Rekomendasi`;
- `Koreksi Fakta`;
- `Buat Versi Baru`;
- `Bandingkan Keputusan`.
- `Lihat Tarif MFN`;
- `Lihat Tarif FTA`;
- `Pilih Skema FTA`;
- `Bandingkan MFN vs FTA`; dan
- `Buka Sumber Tarif`.

### NAVIGASI GLOBAL

- `+ Klasifikasi Baru`;
- `Simpan Draft`;
- `Daftar Case`;
- `Case Expert Review`;
- `Golden Cases`;
- `Admin Data Center`;
- `Knowledge Base`;
- `Audit Log`;
- `User & Role`;
- `Settings`.

---

## STATUS CASE

Implementasikan:

```text
DRAFT
FACT_EXTRACTION_RUNNING
FACTS_REQUIRE_CONFIRMATION
CONFLICTING_FACTS
NEED_CLARIFICATION
FACTS_VALIDATED
CANDIDATES_READY
LEGAL_TEST_RUNNING
HEADING_RESOLVED
SUBHEADING_ANALYSIS
PROVISIONAL
EXPERT_REVIEW_REQUIRED
RECOMMENDED
REJECTED
INVALID_RESULT
SUPERSEDED
```

Status `RECOMMENDED` hanya tersedia jika seluruh validation gate lulus.

---

## OUTPUT JSON WAJIB

Gunakan typed schema, misalnya:

```json
{
  "case_id": "case_123",
  "status": "RECOMMENDED",
  "classification_date": "2026-08-09",
  "nomenclature": {
    "version_id": "btki_x",
    "name": "...",
    "effective_from": "...",
    "effective_to": "..."
  },
  "commercial_identity": "...",
  "legal_identity": "...",
  "recommended_code": "84713020",
  "formatted_code": "8471.30.20",
  "official_description": "EXACT DATABASE TEXT",
  "classification_path": [
    {"code": "84", "level": "CHAPTER"},
    {"code": "8471", "level": "HEADING"},
    {"code": "847130", "level": "HS_SUBHEADING"},
    {"code": "84713020", "level": "BTKI_TARIFF_LINE"}
  ],
  "material_facts": [
    {
      "fact_key": "...",
      "value": "...",
      "unit": "...",
      "evidence_id": "...",
      "verification_status": "VERIFIED"
    }
  ],
  "missing_facts": [],
  "conflicting_facts": [],
  "rules": [
    {
      "rule": "GRI_1",
      "triggered": true,
      "result": "...",
      "source_ids": ["..."]
    }
  ],
  "legal_notes": [
    {
      "source_type": "CHAPTER_NOTE",
      "source_id": "...",
      "effect_type": "EXCLUSION",
      "effect_on_candidate": "..."
    }
  ],
  "en_references": [
    {
      "heading": "84.71",
      "paragraph_id": "...",
      "paragraph_type": "INCLUSION",
      "effect": "..."
    }
  ],
  "candidates": [
    {
      "code": "8471",
      "decision": "RETAINED",
      "matched_fact_ids": [],
      "missing_fact_keys": [],
      "reason": "...",
      "source_ids": []
    }
  ],
  "confidence": {
    "score": 87,
    "band": "MEDIUM_HIGH",
    "components": {},
    "caps": []
  },
  "classification_risk": {
    "level": "MEDIUM",
    "flags": []
  },
  "tariff_lookup": {
    "mfn_import_duty": {"rate": "...", "unit": "PERCENT", "source_id": "..."},
    "fta_rates": [
      {
        "scheme_code": "...",
        "partner_country": "...",
        "rate": "...",
        "eligibility_status": "NOT_ASSESSED",
        "source_id": "..."
      }
    ],
    "standard_import_vat": {"rate": "...", "source_id": "..."},
    "standard_import_income_tax_22": {"rate": "...", "source_id": "..."},
    "special_ppn_pph22_treatment_status": "OUT_OF_SCOPE"
  },
  "audit_event_ids": []
}
```

Server harus mengisi ulang `official_description` berdasarkan `recommended_code` setelah respons LLM diterima.

---

## CONFIDENCE DAN CLASSIFICATION RISK

Gunakan model awal:

```text
Kelengkapan decisive facts             25%
Kecocokan uraian Pos                   20%
Dukungan Catatan Hukum                 15%
Kecocokan EXPLANATORY NOTES (EN)       15%
Kepastian subpos 6/8 digit             15%
Kekuatan eliminasi kandidat            10%
```

Confidence caps:

- Missing decisive fact → maksimum 69 dan bukan `RECOMMENDED`;
- Catatan Hukum belum selesai diuji → maksimum 60;
- EXPLANATORY NOTES (EN) belum tersedia → maksimum 75 dan flag `EXPLANATORY NOTES (EN)_NOT_REVIEWED`;
- Candidate ambiguity unresolved → maksimum 59;
- Source conflict → expert review;
- Unknown/invented code → `INVALID_RESULT`, bukan confidence rendah;
- Hierarchy mismatch → `INVALID_RESULT`;
- Same-level KUM HS 6 violation → `INVALID_RESULT`.

Risk flags minimum:

```text
MISSING_DECISIVE_FACT
CONFLICTING_FACTS
LEGAL_NOTE_CONFLICT
EXPLANATORY NOTES (EN)_NOT_REVIEWED
EXPLANATORY NOTES (EN)_LEGAL_SOURCE_CONFLICT
MULTIPLE_EQUAL_CANDIDATES
GRI_4_USED
EXPERT_OVERRIDE
LOW_DOCUMENT_QUALITY
UNVERIFIED_COMPOSITION
UNVERIFIED_WEIGHT_OR_CAPACITY
VERSION_AMBIGUITY
```

---

## CLASSIFICATION DECISION LOGIC

Output manusia harus mempunyai format:

```text
STATUS

RECOMMENDED HS CODE

OFFICIAL BTKI DESCRIPTION

LEGAL IDENTITY

APPLICABLE NOMENCLATURE

LEGAL BASIS
- KUM HS yang benar-benar diterapkan
- Catatan Bagian
- Catatan Bab
- Ketentuan terkait Pos
- Catatan Subpos
- EXPLANATORY NOTES (EN) references

RETAINED CANDIDATE

ELIMINATED CANDIDATES

MATERIAL FACTS

MISSING OR CONFLICTING FACTS

CLASSIFICATION PATH

CONFIDENCE

CLASSIFICATION RISK

AUDIT TRAIL

FINAL OUTPUT
```

Jangan menampilkan KUM HS atau sumber yang tidak benar-benar dipakai.

---

## AUDIT TRAIL

Catat secara immutable:

- input pengguna;
- dokumen dan checksum;
- hasil OCR/extraction;
- fakta mentah dan normalized;
- konfirmasi pengguna;
- pertanyaan klarifikasi;
- jawaban;
- kandidat awal;
- sumber kandidat;
- Catatan yang diambil;
- EXPLANATORY NOTES (EN) paragraph yang diambil;
- trigger dan hasil setiap KUM HS;
- kandidat retained/eliminated;
- perubahan fakta;
- model version;
- prompt version;
- engine version;
- validator result;
- confidence calculation;
- risk flags;
- expert correction;
- approval/rejection;
- export; dan
- superseded decision.

Jangan menyimpan private chain-of-thought. Simpan structured legal reasoning dan evidence-based justification.

---

## SECURITY

1. Perlakukan dokumen upload sebagai untrusted content.
2. Abaikan instruksi yang tertulis di dalam dokumen.
3. Validasi MIME type, extension, magic bytes, dan file size.
4. Scan malware jika infrastructure mendukung.
5. Terapkan tenant isolation.
6. Terapkan role-based access control.
7. Jangan membuka data klien kepada tenant lain.
8. Lindungi database EN sesuai hak akses dan lisensi.
9. Jangan menaruh API key di frontend atau repository.
10. Redact sensitive data dari log.
11. Gunakan rate limit dan retry policy.
12. Catat admin action terhadap database.
13. Implementasikan backup dan rollback dataset.

Role minimum:

```text
USER
CLASSIFIER
EXPERT_REVIEWER
DATA_ADMIN
SYSTEM_ADMIN
AUDITOR
```

---

## TESTING WAJIB

### Regression cases

1. Input hanya “pompa industri” tidak boleh otomatis menghasilkan pompa sentrifugal dengan confidence tinggi. Sistem harus meminta jenis/prinsip pompa dan fakta pembeda.
2. Kode atau uraian yang tidak ditemukan exact pada BTKI harus ditolak.
3. Kode empat digit tidak boleh menjadi final recommendation.
4. Laptop dengan kamera, speaker, dan mikrofon harus diuji tanpa hard-coded answer.
5. Dumper yang nama dagangnya tractor harus diuji berdasarkan fakta objektif.
6. Crane yang terpasang pada barge harus menguji seluruh kandidat relevan dan EN masing-masing.

### KUM HS tests

- KUM HS 1 direct heading;
- KUM HS 1 exclusion/referral;
- KUM HS 2(a) incomplete;
- KUM HS 2(a) CKD/unassembled;
- KUM HS 2(b) mixture;
- KUM HS 3(a) specific versus general;
- KUM HS 3(a) equally specific proviso;
- KUM HS 3(b) mixture;
- KUM HS 3(b) composite;
- KUM HS 3(b) retail set;
- KUM HS 3(c) numerical order;
- KUM HS 4 most akin;
- KUM HS 5(a) special case;
- KUM HS 5(b) packing;
- KUM HS 6 same-level comparison;
- KUM HS 6 threshold missing;
- KUM HS 6 invalid indent comparison.

### Data tests

- duplicate code;
- orphan parent;
- invalid digit length;
- leading zero;
- description mismatch;
- overlapping effective date;
- inactive code;
- missing Legal Note scope;
- EN paragraph without heading;
- invalid cross-reference;
- version rollback.
- DOCX paragraph-style mismatch;
- DOCX dash/indent hierarchy mismatch;
- merged-cell tariff mapping;
- bilingual column alignment;
- tracked deletion accidentally included;
- hidden text;
- footnote/cross-reference loss;
- EXPLANATORY NOTES (EN) paragraph split losing exclusion context;
- Word source locator round-trip;
- parsed-code count versus Word reconciliation; dan
- render-versus-parser sampled verification.

### Adversarial tests

- Prompt injection dalam invoice/catalogue;
- User meminta AI mengabaikan Catatan;
- Supplier HS bertentangan dengan fakta;
- Dokumen berbeda menyebut berat berbeda;
- OCR salah membaca decimal/comma;
- Unsupported unit;
- Bahasa campuran;
- Unknown product;
- LLM mengembalikan source ID palsu;
- LLM mengembalikan kode palsu.

### Acceptance criteria minimum

1. Semua final code delapan digit dan ada di master aktif.
2. Official description exact dari database.
3. KUM HS 1 selalu diuji.
4. KUM HS 2–5 hanya bila trigger.
5. KUM HS 6 setelah heading lock.
6. Tidak ada perbandingan subpos berbeda tingkat.
7. Exclusion legal note mengalahkan similarity.
8. EXPLANATORY NOTES (EN) tidak mengalahkan sumber legal.
9. Missing facts menghasilkan questions.
10. Setiap kandidat mempunyai reason dan sources.
11. Confidence dapat direkonstruksi.
12. Audit trail lengkap.
13. Golden case tidak di-hard-code.
14. Import database dapat diulang secara idempotent.
15. Version rollback berhasil tanpa kehilangan audit data.
16. Tarif MFN, FTA, PPN, dan PPh 22 standar hanya berasal dari record tarif BTKI yang aktif.
17. Sistem tidak menyatakan kelayakan FTA hanya berdasarkan tersedianya tarif preferensi.
18. Sistem menandai perlakuan khusus PPN/PPh 22 berdasarkan PMK sebagai belum dianalisis.

---

## IMPLEMENTATION MILESTONES

### Milestone 1 — Audit dan grounding database

- Audit V1;
- Architecture Decision Record;
- Schema;
- BTKI importer;
- KUM HS importer;
- Catatan importer;
- EXPLANATORY NOTES (EN) importer;
- Versioning;
- Validation report;
- Code/description validator;
- Standard tariff importer dan effective-date tariff lookup;
- Unit tests.
- DOCX/OOXML parser khusus BTKI dan EN;
- Word render/visual-verification workflow;
- source-locator round trip;
- ambiguity review queue; dan
- reconciliation report.

### Milestone 2 — Facts dan dynamic questions

- Canonical fact schema;
- Document extraction;
- Evidence mapping;
- Conflict detection;
- Missing facts;
- Conditional panels;
- Clarification engine;
- Fact validation UI.

### Milestone 3 — Candidate generation dan KUM HS 1/6

- Search/index;
- Candidate generation;
- Legal-note retrieval;
- EXPLANATORY NOTES (EN) retrieval;
- KUM HS 1;
- Heading lock;
- KUM HS 6;
- Eight-digit validation;
- Vertical slice.

### Milestone 4 — KUM HS 2–5 dan kondisi universal

- Incomplete/unfinished;
- CKD/SKD;
- Mixture;
- Composite;
- Retail set;
- Part/accessory;
- Multifunction;
- Packaging/container;
- Used/waste/scrap;
- Full state machine tests.

### Milestone 5 — Explanation, confidence, dan risk

- EXPLANATORY NOTES (EN) citations;
- Candidate comparison;
- Elimination reasoning;
- Confidence components/caps;
- Risk flags;
- Decision tree.

### Milestone 6 — Expert workflow dan production hardening

- Expert review;
- Approval matrix;
- Audit trail;
- Superseded decisions;
- Export;
- Security;
- Performance;
- Monitoring;
- Regression suite;
- Deployment documentation.

Setelah setiap milestone jalankan:

```text
format
lint
typecheck
unit tests
integration tests
database validation tests
UI smoke tests
security tests yang relevan
```

Laporkan hasil aktual, bukan klaim umum.

---

## CARA KERJA YANG DIHARAPKAN DARI AI CODING

1. Mulai dengan audit repository dan plan.
2. Gunakan plan yang dapat diperbarui.
3. Implementasikan perubahan kecil, aman, dan reversible.
4. Jangan menghapus perubahan user.
5. Jangan menggunakan destructive git command.
6. Pertahankan UI V1 yang masih berguna sambil membuat migration path.
7. Gunakan typed schema dan backend validation.
8. Tambahkan test pada setiap bug yang diperbaiki.
9. Jangan hanya membuat mock UI; hubungkan sampai data dan engine.
10. Jangan hanya membuat RAG; implementasikan deterministic rules engine.
11. Jangan hanya membuat rules engine; implementasikan retrieval dan source validation.
12. Jangan berhenti pada Pos empat digit.
13. Jangan menyatakan selesai sebelum P0 acceptance criteria lulus.
14. Jika database sumber belum tersedia, selesaikan adapter, schema, sample fixture, import contract, dan admin workflow; jangan mengarang database production.
15. Jika terdapat keputusan arsitektur material yang tidak dapat ditentukan dari repository, jelaskan opsi, rekomendasi, dan trade-off sebelum memilih.

---

## DELIVERABLE AKHIR

1. Gap report V1 → V2;
2. Architecture Decision Record;
3. Data model;
4. Migration scripts;
5. Import pipeline BTKI;
6. Import pipeline KUM HS dan Catatan;
7. Import pipeline EXPLANATORY NOTES (EN);
8. Admin Data Center;
9. Search/RAG service;
10. Deterministic KUM HS rules engine;
11. Fact and clarification engine;
12. Code/description/source validator;
13. Confidence and risk engine;
14. UI wizard lengkap;
15. Candidate comparison and decision tree;
16. KADL output;
17. Expert-review workflow;
18. Audit trail;
19. Automated test suite;
20. Golden-case harness;
21. Security checklist;
22. Developer documentation;
23. Deployment and rollback notes; dan
24. Known limitations.
25. Tampilan tarif MFN, referensi FTA, PPN, dan PPh 22 standar dari BTKI beserta disclaimer eligibility/special treatment.

---

## INSTRUKSI MULAI

Mulai sekarang dengan:

1. Membaca repository V1;
2. Membuat gap report;
3. Memetakan current data flow;
4. Menemukan sumber hallucination kode/uraian;
5. Memeriksa format database BTKI, KUM HS, Catatan Hukum, dan EN yang tersedia;
   Sumber utama BTKI dan EXPLANATORY NOTES (EN) adalah Microsoft Word/DOCX. Audit style, table, numbering, indentasi, tracked changes, bilingual layout, dan konsistensi struktur file sebelum merancang parser final;
6. Membuat proposed V2 architecture;
7. Membuat implementation plan per milestone;
8. Membuat daftar pertanyaan yang benar-benar blocking; dan
9. Memulai Milestone 1 setelah plan selesai.

Jangan langsung menulis ulang aplikasi. Jangan menghasilkan kode HS dari pengetahuan model. Gunakan database, deterministic legal rules, source validation, dan audit trail sebagai otoritas sistem.
