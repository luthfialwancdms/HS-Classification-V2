# Task List — HS Classification V2
## Untuk Breakdown di Excel

> Format kolom: No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave (Parallel)

---

## Kolom Penjelasan

| Kolom | Keterangan |
|-------|-----------|
| **Task ID** | Nomor referensi task (misal: 1.1, 2.3) |
| **Grup** | Kelompok besar task |
| **Nama Task** | Judul singkat task |
| **Deskripsi Singkat** | Apa yang dikerjakan |
| **Tipe** | `CORE` = wajib / `TEST` = property-based test (opsional) / `CHECKPOINT` = review milestone |
| **Status** | `TODO` / `IN PROGRESS` / `DONE` |
| **Prioritas** | `🔴 Kritis` / `🟡 Penting` / `🟢 Nice to have` |
| **Bulan** | Target bulan pengerjaan (1–4) |
| **Wave** | Nomor wave untuk task paralel (task dalam wave sama bisa dikerjakan bersamaan) |

---

## FASE 1 — FONDASI DATABASE & ADMIN DATA CENTER
*(Target: Bulan 1)*

| No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave |
|----|---------|------|-----------|-------------------|------|--------|-----------|-------|------|
| 1 | 1.1 | Setup Proyek | Buat struktur proyek C# | Init ASP.NET Core 8 Web API, Clean Architecture (API/Application/Domain/Infrastructure), NuGet packages (EF Core, MySQL, OpenXML, Serilog, AutoMapper) | CORE | TODO | 🔴 Kritis | 1 | 0 |
| 2 | 1.2 | Setup Proyek | Buat schema database MySQL | Buat entity & migrasi untuk semua tabel: nomenclature_versions, btki_tariff_lines, explanatory_notes, section_notes, chapter_notes, subheading_notes, mfn_rates, fta_rates, tax_rates, classification_cases, reasoning_events | CORE | TODO | 🔴 Kritis | 1 | 0 |
| 3 | 1.3 | Setup Proyek | Implementasi Version Manager | State machine versi BTKI: UPLOADED→PARSING→MAPPED→VALIDATING→DRY_RUN→COMMITTED→ACTIVE. Satu versi aktif per tanggal | CORE | TODO | 🔴 Kritis | 1 | 1 |
| 4 | 1.4* | Setup Proyek | Property test: state machine | Test transisi state yang valid & invalid pada Version Manager | TEST | TODO | 🟢 Nice to have | 1 | 2 |
| 5 | 1.5* | Setup Proyek | Property test: satu versi aktif | Test constraint hanya satu versi aktif per rentang tanggal | TEST | TODO | 🟢 Nice to have | 1 | 2 |
| 6 | 2.1 | Parser BTKI | Buat DOCX parser OpenXML | Parse file Word BTKI: ekstrak kode, uraian ID/EN, level indent, tarif (MFN/FTA/PPN/PPh22), simpan source location (halaman, paragraf, sel) | CORE | TODO | 🔴 Kritis | 1 | 1 |
| 7 | 2.2 | Parser BTKI | Deteksi hierarki & relasi parent | Bangun relasi parent-child dari indent, flag STRUCTURE_AMBIGUITY jika parent tidak bisa ditentukan | CORE | TODO | 🔴 Kritis | 1 | 2 |
| 8 | 2.3 | Parser BTKI | Buat Structure Validator | Validasi format kode, duplikat, missing parent, orphan, inkonsistensi indent. Output laporan ERROR/WARNING/INFO | CORE | TODO | 🔴 Kritis | 1 | 3 |
| 9 | 2.4 | Parser BTKI | Dry run & commit workflow | Simulasi aktivasi: cek parent, circular ref, total count 14.273. Antrian review ambiguitas untuk admin | CORE | TODO | 🔴 Kritis | 1 | 4 |
| 10 | 2.5 | Parser BTKI | API endpoint & UI admin import BTKI | Endpoint upload/validasi/commit BTKI. UI admin untuk upload, lihat laporan, approve ambiguitas | CORE | TODO | 🔴 Kritis | 1 | 5 |
| 11 | 2.6* | Parser BTKI | Property test: source traceability | Test setiap node punya source_id yang bisa di-resolve ke dokumen asli | TEST | TODO | 🟡 Penting | 1 | 5 |
| 12 | 3.1 | Parser EN | Buat EN DOCX parser | Parse Explanatory Notes: ekstrak paragraf, klasifikasi tipe (INCLUSION/EXCLUSION/DEFINITION/dll), simpan source location | CORE | TODO | 🔴 Kritis | 1 | 1 |
| 13 | 3.2 | Parser EN | Linking paragraf EN | Buat link prev/next paragraf, assign parent_section_id untuk pernyataan multi-paragraf | CORE | TODO | 🔴 Kritis | 1 | 2 |
| 14 | 3.3 | Parser EN | Validasi & import workflow EN | Deteksi paragraf tanpa referensi heading valid, rekonsiliasi total 65.833, API commit | CORE | TODO | 🔴 Kritis | 1 | 3 |
| 15 | 3.4* | Parser EN | Property test: segmentasi EN | Test chain prev/next paragraf tidak putus, parent_section_id konsisten | TEST | TODO | 🟢 Nice to have | 1 | 5 |
| 16 | 4.1 | Parser Legal Notes | Buat Legal Notes parser | Parse Section Notes, Chapter Notes, Subheading Notes. Klasifikasi effect_type (DEFINITION/INCLUSION/EXCLUSION/REFERRAL/dll) | CORE | TODO | 🔴 Kritis | 1 | 1 |
| 17 | 4.2 | Parser Legal Notes | Validasi & import workflow Legal Notes | Rekonsiliasi jumlah catatan, deteksi catatan tanpa referensi valid, API commit | CORE | TODO | 🔴 Kritis | 1 | 2 |
| 18 | 5 | Checkpoint | ✅ Checkpoint 1 — Database & Admin selesai | Review: semua test pass, schema stabil, import workflow functional | CHECKPOINT | TODO | 🔴 Kritis | 1 | — |

---

## FASE 1B — TEMPLATE INGESTION (CSV/Excel)
*(Target: Bulan 1, paralel dengan Fase 1)*

| No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave |
|----|---------|------|-----------|-------------------|------|--------|-----------|-------|------|
| 18B | 5B.1 | Template Ingestion | Generate template CSV & Excel | Buat template CSV & Excel dengan semua kolom standar + tooltip. Endpoint download GET /api/admin/template/download/csv dan /xlsx | CORE | TODO | 🟡 Penting | 1 | 5 |
| 19B | 5B.2 | Template Ingestion | Validasi file template | Validasi per baris: format kode, parent, tanggal, status, rate_unit. Laporan error per baris. ERROR = reject, WARNING = perlu konfirmasi. Support CSV & xlsx | CORE | TODO | 🟡 Penting | 1 | 5 |
| 20B | 5B.3 | Template Ingestion | Preview diff & commit workflow | Kategorisasi baris: INSERT/UPDATE/UNCHANGED. Preview side-by-side dengan diff. Konfirmasi admin sebelum commit. source_id format template:upload. State machine sama dengan DOCX | CORE | TODO | 🟡 Penting | 1 | 5 |
| 21B | 5B.4 | Template Ingestion | Tambah kolom ingestion_method ke DB | ENUM(DOCX_PARSER, CSV_TEMPLATE, XLSX_TEMPLATE) di tabel btki_tariff_lines. Migrasi data existing = DOCX_PARSER | CORE | TODO | 🟡 Penting | 1 | 2 |
| 22B | 5B.5 | Template Ingestion | UI admin upload template | Tab kedua di Admin Data Center: tombol download template, validasi error per baris, preview INSERT/UPDATE/SKIP dengan diff, progress & summary | CORE | TODO | 🟡 Penting | 1 | 5 |

---

## FASE 2 — MESIN KLASIFIKASI (FACT ENGINE + RULES ENGINE)
*(Target: Bulan 2)*

| No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave |
|----|---------|------|-----------|-------------------|------|--------|-----------|-------|------|
| 19 | 6.1 | Fact Engine | Buat Fact Extractor 35 tipe fakta | Ekstrak 35 tipe fakta dari input user via LLM. Status per fakta: UNKNOWN/NOT_APPLICABLE/CONFLICTING/VERIFIED. Tidak boleh default | CORE | TODO | 🔴 Kritis | 2 | 6 |
| 20 | 6.2 | Fact Engine | Dynamic question generator | Generate pertanyaan berdasarkan information gain. Pertanyaan diurutkan dari gain tertinggi. Recalculate setelah tiap jawaban | CORE | TODO | 🔴 Kritis | 2 | 7 |
| 21 | 6.3 | Fact Engine | Conflict detection & konfirmasi user | Deteksi fakta yang bertentangan dari sumber berbeda, minta konfirmasi user | CORE | TODO | 🔴 Kritis | 2 | 7 |
| 22 | 6.4* | Fact Engine | Property test: tidak ada default fakta | Test bahwa fakta yang tidak ada → status=UNKNOWN, nilai=null (bukan default) | TEST | TODO | 🟡 Penting | 2 | 8 |
| 23 | 6.5* | Fact Engine | Property test: deteksi fakta konflik | Test fakta bertentangan → status=CONFLICTING, sistem minta konfirmasi | TEST | TODO | 🟡 Penting | 2 | 8 |
| 24 | 6.6* | Fact Engine | Property test: urutan pertanyaan | Test pertanyaan diurutkan dari information gain tertinggi | TEST | TODO | 🟢 Nice to have | 2 | 8 |
| 25 | 7.1 | Candidate Generator | Multi-strategy database search | Cari kandidat heading via: exact match, sinonim, multilingual, fungsi, material, legal note referral, EN cross-reference. TIDAK boleh generate kode dari LLM | CORE | TODO | 🔴 Kritis | 2 | 8 |
| 26 | 7.2 | Candidate Generator | Enrichment kandidat dengan context legal | Untuk tiap kandidat: ambil deskripsi, hierarki, Section Notes, Chapter Notes, Subheading Notes, paragraf EN, child subheadings, source_id | CORE | TODO | 🔴 Kritis | 2 | 9 |
| 27 | 7.3* | Candidate Generator | Property test: tidak ada kode hallusinasi | Test semua kandidat yang dikembalikan ada di tabel btki_tariff_lines | TEST | TODO | 🔴 Kritis | 2 | 10 |
| 28 | 7.4* | Candidate Generator | Property test: enrichment lengkap | Test semua field enrichment terisi: deskripsi, hierarki, notes, EN, source_id | TEST | TODO | 🟡 Penting | 2 | 10 |
| 29 | 8.1 | Rules Engine | KUM HS 1: Heading + Notes + EN | Cocokkan fakta vs deskripsi heading, terapkan Section/Chapter Notes, gunakan EN untuk inklusi/eksklusi. Rekam reasoning + source_ids | CORE | TODO | 🔴 Kritis | 2 | 10 |
| 30 | 8.2 | Rules Engine | KUM HS 1: Early stopping | Jika tepat 1 heading retained → loncat ke KUM HS 6. Tidak perlu KUM HS 2-4 | CORE | TODO | 🔴 Kritis | 2 | 11 |
| 31 | 8.3* | Rules Engine | Property test: KUM HS 1 universal | Test KUM HS 1 diterapkan ke semua kandidat, reasoning event tercatat | TEST | TODO | 🟡 Penting | 2 | 11 |
| 32 | 8.4* | Rules Engine | Property test: early stopping | Test jika 1 heading retained → langsung KUM HS 6, tidak ada event KUM HS 2-4 | TEST | TODO | 🟡 Penting | 2 | 11 |
| 33 | 9.1 | Rules Engine | KUM HS 2(a): Barang tidak lengkap | Evaluasi karakter pokok barang CKD/SKD/tidak lengkap. Output: TREAT_AS_COMPLETE / CLASSIFY_AS_PRESENTED / NEED_CLARIFICATION | CORE | TODO | 🔴 Kritis | 2 | 11 |
| 34 | 9.2 | Rules Engine | KUM HS 2(b): Campuran material | Trigger jika mixture/combination. Jika menghasilkan 2+ heading → lanjut KUM HS 3 | CORE | TODO | 🔴 Kritis | 2 | 11 |
| 35 | 10.1 | Rules Engine | KUM HS 3(a): Spesifisitas | Hitung specificity score tiap heading. Pilih paling spesifik. Jika seri → KUM HS 3(b) | CORE | TODO | 🔴 Kritis | 2 | 12 |
| 36 | 10.2 | Rules Engine | KUM HS 3(b): Essential character | Evaluasi 6 faktor dengan bobot (fungsi 30%, material 15%, bulk 15%, nilai 15%, kebutuhan 15%, persepsi 10%). Tidak ada faktor yang otomatis dominan | CORE | TODO | 🔴 Kritis | 2 | 13 |
| 37 | 10.3 | Rules Engine | KUM HS 3(c): Urutan numerik terakhir | Dari heading equally applicable → pilih yang nomor terbesar | CORE | TODO | 🔴 Kritis | 2 | 13 |
| 38 | 10.4* | Rules Engine | Property test: KUM HS 3(a) spesifisitas | Test heading paling spesifik dipilih, KUM HS 3(b) tidak dijalankan | TEST | TODO | 🟡 Penting | 2 | 14 |
| 39 | 11.1 | Rules Engine | KUM HS 4: Most akin | Bandingkan dengan barang paling mirip (material, bentuk, fungsi, prinsip kerja). Flag expert review | CORE | TODO | 🟡 Penting | 2 | 14 |
| 40 | 11.2 | Rules Engine | KUM HS 5: Kemasan & peti | 5(a): peti khusus diklasifikasikan dengan barang. 5(b): bahan kemas diklasifikasikan dengan barang | CORE | TODO | 🟡 Penting | 2 | 14 |
| 41 | 12.1 | Rules Engine | KUM HS 6: Descent ke subheading | Setelah heading 4-digit terkunci → turun ke 6-digit → 8-digit. Bandingkan hanya level yang sama. TIDAK default ke .00 | CORE | TODO | 🔴 Kritis | 2 | 15 |
| 42 | 12.2* | Rules Engine | Property test: KUM HS 6 same-level | Test hanya subheading level sama yang dibandingkan, tidak cross-heading | TEST | TODO | 🟡 Penting | 2 | 16 |
| 43 | 13 | Checkpoint | ✅ Checkpoint 2 — Rules Engine selesai | Review: semua KUM HS 1-6 implementasi, reasoning chain tercatat | CHECKPOINT | TODO | 🔴 Kritis | 2 | — |

---

## FASE 3 — VALIDASI, TARIF & API
*(Target: Bulan 3)*

| No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave |
|----|---------|------|-----------|-------------------|------|--------|-----------|-------|------|
| 44 | 14.1 | Validator | Buat comprehensive validator | Validasi: kode ada di DB, format benar, semua parent ada, uraian exact match, tanggal aktif, source_id valid. Return IsValid=false jika ada yang gagal | CORE | TODO | 🔴 Kritis | 3 | 16 |
| 45 | 14.2 | Validator | Validasi rejection & error handling | Jika validasi gagal: jangan tampilkan ke user, kembalikan error ke Rules Engine, log untuk monitoring | CORE | TODO | 🔴 Kritis | 3 | 17 |
| 46 | 14.3* | Validator | Property test: validasi komprehensif | Test semua skenario invalid: kode tidak ada, format salah, parent hilang, uraian tidak cocok, kode tidak aktif, source_id rusak | TEST | TODO | 🟡 Penting | 3 | 17 |
| 47 | 14.4* | Validator | Property test: rejection tidak tampil ke user | Test hasil invalid tidak ditampilkan ke user, dikembalikan ke Rules Engine | TEST | TODO | 🟡 Penting | 3 | 17 |
| 48 | 15.1 | Tariff Lookup | Lookup tarif MFN | Ambil tarif Bea Masuk Umum (PERCENT/SPECIFIC/COMPOUND) dari mfn_rates, verifikasi tanggal efektif, sertakan source_id | CORE | TODO | 🔴 Kritis | 3 | 17 |
| 49 | 15.2 | Tariff Lookup | Lookup tarif FTA | Ambil semua tarif FTA per skema & negara mitra. Label "Tarif FTA Tersedia" (bukan berlaku). Tampilkan disclaimer wajib tentang eligibilitas | CORE | TODO | 🔴 Kritis | 3 | 18 |
| 50 | 15.3 | Tariff Lookup | Lookup PPN & PPh 22 impor | Ambil tarif standar PPN dan PPh 22 dari tax_rates. Tampilkan disclaimer PMK tidak dicakup | CORE | TODO | 🔴 Kritis | 3 | 18 |
| 51 | 15.4* | Tariff Lookup | Property test: source traceability tarif | Test semua tarif (MFN/FTA/PPN/PPh22) punya source_id valid yang bisa di-trace ke BTKI asli | TEST | TODO | 🟡 Penting | 3 | 19 |
| 52 | 16.1 | API Backend | Implementasi classification orchestrator | Orkestrasi: Fact Engine → Candidate Generator → Rules Engine → Validator → Tariff Lookup. Simpan classification_cases + reasoning_events. Handle iterasi NEED_CLARIFICATION | CORE | TODO | 🔴 Kritis | 3 | 19 |
| 53 | 16.2 | API Backend | Buat REST API endpoint klasifikasi | POST /start, POST /:caseId/facts, GET /:caseId/status, GET /:caseId/reasoning, GET /:caseId/tariffs, POST /:caseId/export | CORE | TODO | 🔴 Kritis | 3 | 20 |
| 54 | 16.3 | API Backend | Autentikasi & otorisasi (JWT + RBAC) | JWT auth, role: Admin / Classifier / Viewer. Proteksi endpoint admin. Log semua request dengan user_id | CORE | TODO | 🔴 Kritis | 3 | 20 |

---

## FASE 4 — FRONTEND, INTEGRASI & DEPLOYMENT
*(Target: Bulan 4)*

| No | Task ID | Grup | Nama Task | Deskripsi Singkat | Tipe | Status | Prioritas | Bulan | Wave |
|----|---------|------|-----------|-------------------|------|--------|-----------|-------|------|
| 55 | 17.1 | Frontend | Update React app → koneksi backend V2 | Ganti Groq API calls ke backend C#. Form → POST /start. Iterasi pertanyaan. Tampilkan hasil: kode HS + tarif + disclaimers + reasoning chain | CORE | TODO | 🔴 Kritis | 4 | 21 |
| 56 | 17.2 | Frontend | Buat admin dashboard data center | Route admin: /admin/versions, /admin/upload, /admin/validation. UI upload BTKI/EN/Legal Notes, laporan validasi, antrian ambiguitas, aktivasi versi | CORE | TODO | 🔴 Kritis | 4 | 22 |
| 57 | 17.3 | Frontend | Error handling & user feedback | Pesan error yang user-friendly, loading state, tampilkan pertanyaan klarifikasi dengan context kandidat, disclaimer tarif, retry logic | CORE | TODO | 🟡 Penting | 4 | 22 |
| 58 | 18.1 | Deployment | Buat database seeding scripts | Script seed admin user pertama, metadata versi BTKI 2022, dokumentasi proses upload DOCX pertama kali | CORE | TODO | 🟡 Penting | 4 | 23 |
| 59 | 18.2 | Deployment | Konfigurasi production deployment | appsettings.json production, CORS, HTTPS, Serilog structured logging, health check, docker-compose (C# API + MySQL + React/nginx) | CORE | TODO | 🔴 Kritis | 4 | 24 |
| 60 | 18.3 | Deployment | Buat dokumentasi API | OpenAPI/Swagger, autentikasi, schema request/response, error codes. User guide admin data center. User guide klasifikasi | CORE | TODO | 🟡 Penting | 4 | 24 |
| 61 | 18.4* | Deployment | Integration test end-to-end | Test full flow: upload DOCX → validasi → aktivasi → klasifikasi → tarif. Test error scenarios, audit trail, version switching | TEST | TODO | 🟡 Penting | 4 | 25 |
| 62 | 19 | Checkpoint | ✅ Checkpoint Final — Sistem siap produksi | Review semua: test pass, 16 correctness properties validated, 20 requirements covered, audit trail lengkap, tarif dengan disclaimer | CHECKPOINT | TODO | 🔴 Kritis | 4 | — |

---

## Ringkasan per Grup

| Grup | Jumlah Task Core | Jumlah Test Opsional | Total |
|------|-----------------|---------------------|-------|
| Setup Proyek | 3 | 2 | 5 |
| Parser BTKI | 5 | 1 | 6 |
| Parser EN | 3 | 1 | 4 |
| Parser Legal Notes | 2 | 0 | 2 |
| **Template Ingestion (CSV/Excel)** | **5** | **0** | **5** |
| Fact Engine | 3 | 3 | 6 |
| Candidate Generator | 2 | 2 | 4 |
| Rules Engine (KUM HS 1-6) | 8 | 5 | 13 |
| Validator | 2 | 2 | 4 |
| Tariff Lookup | 3 | 1 | 4 |
| API Backend | 3 | 0 | 3 |
| Frontend | 3 | 0 | 3 |
| Deployment | 3 | 1 | 4 |
| Checkpoint | 3 | 0 | 3 |
| **TOTAL** | **48** | **18** | **66** |

---

## Ringkasan per Bulan

| Bulan | Fase | Core Tasks | Test Tasks | Target Selesai |
|-------|------|-----------|-----------|----------------|
| Bulan 1 | Database & Admin Data Center | 14 | 4 | Database BTKI aktif, admin bisa upload & aktivasi |
| Bulan 2 | Mesin Klasifikasi | 15 | 10 | Rules engine KUM HS 1-6 jalan, menghasilkan kode dari DB |
| Bulan 3 | Validasi, Tarif & API | 9 | 4 | API backend lengkap, tarif dengan disclaimer |
| Bulan 4 | Frontend, Integrasi & Deployment | 7 | 1 | Sistem live, siap dipakai user |

---

## Catatan

- Task bertanda `*` adalah **property-based test (opsional)** — bisa dilewati untuk MVP lebih cepat
- **Wave** menunjukkan urutan paralel: task dengan wave sama bisa dikerjakan bersamaan oleh developer berbeda
- Semua task CORE di **Fase 1** harus selesai sebelum Fase 2 dimulai (database harus ada sebelum rules engine)
- **Kode HS wajib dari database** — LLM hanya untuk ekstraksi fakta, bukan untuk generate kode HS
- Tarif FTA selalu ditampilkan dengan **disclaimer eligibilitas**

---

*Generated: 2026-08-19 | Project: HS Classification V2*
