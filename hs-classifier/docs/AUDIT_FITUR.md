# AUDIT KESENJANGAN FITUR (read-only)

Sumber persyaratan: semua `.md` di `D:\Work\Project\Project_AI\docs`. Objek audit: frontend `hs-classifier` dan backend `hs-classifier-api`.

- **Branch yang diaudit:** `feature/ai-status`
- **HEAD yang diaudit:** `505e1b5`
- Isi HEAD: restyle v0, REV1, REV2, dan fitur Status AI. `main` = `origin/main` = `285020d`.
- Working tree punya perubahan lokal yang belum di-commit dan **tidak** diaudit: CORS port 5174 di `Program.cs`, `.env.example`, `graphify-out/`, dan file `.vs/`. Berkas `LANJUTAN_REV1.md` juga belum ter-track.
- Tidak ada file repo yang diubah, tidak ada branch atau commit baru. Satu-satunya file baru adalah laporan ini (tidak di-commit).

Cara baca status (lima tingkat yang diminta):

| Status | Arti |
|---|---|
| Ada dan teruji | Ada di kode dan ada tes otomatis (xUnit) yang menyentuh perilakunya. |
| Ada di kode | Ada di kode, tidak ada tes otomatis yang ditemukan. |
| Hanya teruji manual/stub | Hanya ada bukti di `hs-classifier/docs/CHECKLIST_FUNGSI.md` (DOM + fetch stub, backend asli tidak dipakai). |
| Tidak ditemukan | Tidak ada di kode setelah pencarian. |
| Tidak jelas | Bukti tidak cukup untuk memutuskan, atau persyaratan saling bertentangan (diberi awalan KONFLIK). |

Kata **SEBAGIAN** di kolom Catatan berarti sebagian isi persyaratan ada dan sebagian tidak. Status tetap salah satu dari lima tingkat di atas.

---

## A. Ringkasan

### A.1 Hitungan

Total persyaratan yang diekstrak: **281** (ID unik R-001 dan seterusnya, tidak digabung). Dari jumlah itu, **66** ditandai SEBAGIAN di kolom Catatan.

| Status | Jumlah | Persen |
|---|---|---|
| Ada dan teruji | 74 | 26.3% |
| Ada di kode | 86 | 30.6% |
| Hanya teruji manual/stub | 22 | 7.8% |
| Tidak ditemukan | 68 | 24.2% |
| Tidak jelas | 31 | 11% |
| **Total** | **281** | 100% |

Per dokumen:

| Bagian | Dokumen | Ada dan teruji | Ada di kode | Manual/stub | Tidak ditemukan | Tidak jelas | Total |
|---|---|---|---|---|---|---|---|
| B.1 | Master prompt V2 | 42 | 40 | 12 | 36 | 15 | 145 |
| B.2 | FLOW_BISNIS_V2 | 13 | 4 | 3 | 2 | 2 | 24 |
| B.3 | WORKFLOW_END_TO_END | 3 | 9 | 0 | 3 | 1 | 16 |
| B.4 | DATABASE_SCHEMA | 1 | 4 | 0 | 2 | 1 | 8 |
| B.5 | PRESENTATION | 1 | 3 | 0 | 9 | 5 | 18 |
| B.6 | TASK_LIST | 9 | 25 | 4 | 13 | 5 | 56 |
| B.7 | PROJECT_SUMMARY | 1 | 0 | 0 | 0 | 0 | 1 |
| B.8 | TECHNICAL dan USER_GUIDE | 3 | 1 | 2 | 1 | 1 | 8 |
| B.9 | UI_SUMMARY (turunan) | 1 | 0 | 1 | 2 | 1 | 5 |

Per area:

| Area | Ada dan teruji | Ada di kode | Manual/stub | Tidak ditemukan | Tidak jelas | Total |
|---|---|---|---|---|---|---|
| Klasifikasi | 37 | 50 | 14 | 22 | 14 | 137 |
| Non-fungsional | 1 | 13 | 1 | 29 | 6 | 50 |
| Data/BTKI | 6 | 11 | 0 | 8 | 3 | 28 |
| Admin | 13 | 4 | 3 | 2 | 1 | 23 |
| Review Ahli | 11 | 1 | 1 | 0 | 1 | 14 |
| Auth/Role | 1 | 4 | 1 | 0 | 3 | 9 |
| AI | 2 | 2 | 0 | 3 | 2 | 9 |
| Laporan/Ekspor | 0 | 1 | 2 | 2 | 1 | 6 |
| Riwayat | 3 | 0 | 0 | 2 | 0 | 5 |

Catatan hitungan: persyaratan yang sama kadang muncul di beberapa dokumen (misalnya MySQL, kuota harian, ekspor) dan dihitung per dokumen. Angka tidak boleh dibaca sebagai persen kelengkapan produk. Banyak baris `Tidak jelas` adalah konflik atau bukti yang kurang, bukan fitur yang pasti kosong.

### A.2 Sepuluh kesenjangan terbesar menurut dampak bisnis

| # | Kesenjangan | ID persyaratan | Mengapa penting |
|---|---|---|---|
| 1 | Tidak ada upload dokumen pengguna (invoice, datasheet, foto) dan ekstraksi fakta darinya. `FactExtractionService.MergeLlmFacts` ada tetapi tidak dipanggil siapa pun. | R-074, R-132, R-181, R-190 | Ini inti nilai V2 menurut master prompt dan WORKFLOW STEP 2. Saat ini semua fakta datang dari form. |
| 2 | Tidak ada ekspor hasil (JSON, DOCX, PDF, Excel) dan tidak ada endpoint `POST /{caseId}/export`. | R-148, R-503, R-647 | Pengguna tidak bisa membawa hasil keluar aplikasi. Ekspor disebut di master prompt, TASK_LIST 16.2, dan PRESENTATION (premium). |
| 3 | Audit trail belum memenuhi master prompt: tidak ada hash chain antar event, tidak ada versi engine/prompt per case, tidak ada tabel `audit_logs`, tidak ada halaman Audit Log, tidak ada log tindakan admin. | R-040, R-063, R-180 s/d R-183, R-406, R-512 | Syarat kepatuhan dan keterlacakan keputusan. |
| 4 | Keamanan multi-pengguna belum ada: tidak ada tenant isolation, tidak ada rate limit server, tidak ada kuota harian (`daily_usage`), tidak ada pemeriksaan MIME/magic bytes, tidak ada scan malware. | R-191 s/d R-193, R-198, R-404, R-507 | Tanpa ini aplikasi belum aman dibuka untuk lebih dari satu klien. |
| 5 | Parser DOCX BTKI/EN belum membaca struktur OOXML (style, numbering, merged cell, tracked changes, teks tersembunyi, footnote, source locator Word). Commit EN dan Catatan Bagian/Bab belum tersedia. | R-020 s/d R-025, R-028, R-029 | Integritas data referensi. Catatan Hukum dan EN tidak bisa masuk ke database lewat jalur resmi, sehingga confidence selalu terkena batas "EN belum tersedia". |
| 6 | Tidak ada golden case, tes regresi enam kasus master prompt, tes adversarial (prompt injection), tes property-based, dan tes integrasi end-to-end. | R-200 s/d R-219, R-515, R-604, R-655 | Tidak ada pagar kualitas untuk klaim "kode tidak di-hard-code" dan akurasi. |
| 7 | KUM HS 3(b), 2(b), dan 4 masih versi sederhana. 3(b) hanya mencocokkan fakta `material`, bukan enam faktor berbobot. KUM HS 4 selalu mengambil kandidat pertama lalu menandai review ahli. | R-095, R-098, R-099, R-101, R-631, R-634 | Menyangkut ketepatan hukum pada kasus campuran, set, dan barang tanpa pos yang jelas. |
| 8 | Model status 16 tingkat tidak ada (kode: `Determined`, `NeedClarification`, `NeedExpertReview`, `Rejected` plus status keputusan ahli). Tidak ada `Simpan Draft`, `Buat Versi Baru`, `SUPERSEDED`. | R-150, R-153, R-160 | Pengguna tidak bisa menyimpan pekerjaan setengah jadi atau membuat versi ulang keputusan. |
| 9 | UI wizard 11 langkah tidak ada. Yang ada form satu halaman 14 field dan modal klarifikasi. Tidak ada kartu kandidat, tab legal reasoning, decision tree, atau pemilihan skema FTA. | R-130, R-137 s/d R-141, R-151 | Penjelasan hukum per kandidat menjadi inti kepercayaan pengguna terhadap hasil. |
| 10 | Fitur komersial belum ada: paket free/premium, riwayat 7 hari untuk free, bulk CSV, akses API, notifikasi. | R-501, R-502, R-504, R-514 | Perlu keputusan bisnis dulu sebelum dikerjakan. |

### A.3 Konflik yang butuh keputusan (rincian di bagian E)

1. Database: MySQL (DATABASE_SCHEMA, TASK_LIST), PostgreSQL (PRESENTATION T4, TECHNICAL), SQL Server (kode).
2. Panjang minimal alasan keputusan ahli: kode 5 karakter. Angka 10 hanya ada di `UI_SUMMARY_FOR_FRONTEND_GENERATION.md`, yang ditulis agent, bukan atasan. Tidak ada md atasan yang menyebut angka itu.
3. Role: kode 4 role, master prompt 6 role, DATABASE_SCHEMA `admin/reviewer/user`, PRESENTATION `Admin, Reviewer, User`, TASK_LIST `Admin/Classifier/Viewer`.
4. Master prompt melarang LLM mengeluarkan kode, tetapi kode meminta LLM mengusulkan pos 4 digit (disaring ke database).
5. Master prompt melarang contoh di-hard-code, tetapi `CandidateGeneratorService` punya peta sinonim hard-coded.
6. Kuota free tier 10 req/hari (PRESENTATION L311, DATABASE_SCHEMA) bertentangan dengan 50 req/hari (PRESENTATION L228).
7. FLOW_BISNIS §10 sudah usang terhadap kode (pembeda angka kini dibaca otomatis; jalur PDF BTKI tidak disebut).
8. Status KUM HS 4: master prompt "wajib review ahli untuk risiko tinggi", kode selalu menandai review ahli.
9. Contoh PPh 22: 10% (WORKFLOW) lawan 7.5% (UI_SUMMARY). Keduanya contoh tampilan, bukan data.

---

## 0. Inventaris dokumen md

Kolom "Diminta oleh" diisi hanya bila tertulis di dokumen atau terjadi di sesi kerja ini. Selain itu: tidak tertulis.

| Berkas | Ukuran (byte) | Diubah | Tujuan (satu kalimat) | Diminta oleh |
|---|---|---|---|---|
| `AI_HS_Classification_Intelligence_V2_Product_Logic_UI_and_Coding_Prompt.md` | 48.626 | 2026-08-10 | Master prompt spesifikasi V2: prinsip, model data, KUM HS 1–6, wizard UI 11 langkah, status, confidence, keamanan, tes. | tidak tertulis |
| `DATABASE_SCHEMA.md` | 10.462 | 2026-08-07 | Skema MySQL bertahap untuk V1/V2 awal (users, classification_cases, daily_usage, expert_reviews, audit_logs, btki_reference). | tidak tertulis |
| `FLOW_BISNIS_V2.md` | 19.045 | 2026-10-26 | Alur bisnis V2 untuk Classifier, Expert, Admin, Viewer beserta batasan saat ini (§10). | tidak tertulis |
| `PRESENTATION.md` | 22.505 | 2026-08-07 | Deck MVP V1: AI agent, keamanan, deployment, free vs premium, roadmap T1–T21, data seed. | tidak tertulis |
| `PROJECT_SUMMARY.md` | 9.339 | 2026-08-18 | Ringkasan proyek V1 ke V2 dan rencana 3–4 bulan. | tidak tertulis |
| `TASK_LIST.md` | 18.254 | 2026-09-03 | Daftar 66 task V2 untuk breakdown di Excel, seluruhnya berstatus `TODO` di dokumen. | tidak tertulis |
| `TECHNICAL.md` | 16.959 | 2026-07-31 | Dokumentasi teknis MVP V1 (frontend saja, Groq dipanggil dari browser). | tidak tertulis |
| `UI_SUMMARY_FOR_FRONTEND_GENERATION.md` | 63.421 | 2026-10-29 | Ringkasan UI untuk dipakai AI lain menghasilkan frontend. **Ditulis oleh agent atas permintaan user di sesi ini, bukan oleh atasan.** Turunan, bukan sumber otoritatif. | user (sesi ini) |
| `USER_GUIDE.md` | 18.188 | 2026-07-31 | Panduan pengguna V1. | tidak tertulis |
| `WORKFLOW_END_TO_END.md` | 23.934 | 2026-08-18 | Alur end-to-end V2: Jalur A (admin), Jalur B (klasifikasi), validator, tarif, kode status. | tidak tertulis |

Dokumen frontend di `hs-classifier/docs/` (bukan sumber persyaratan, dipakai sebagai bukti dan untuk bagian D):

| Berkas | Ukuran | Diubah | Fungsi |
|---|---|---|---|
| `CHECKLIST_FUNGSI.md` | 30.239 | 2026-11-01 | Bukti uji manual/stub per butir UI (G, K, C, R, H, E, A, AI). |
| `RINGKASAN_RESTYLE.md` | 18.627 | 2026-11-01 | Ringkasan restyle dan daftar yang sengaja tidak dibuat. |
| `TEMUAN.md` | 1.380 | 2026-10-29 | Temuan F1 (notice hilang) dan F2 (alasan 5 vs 10 karakter). |
| `DESIGN.md` | 17.018 | 2026-10-29 | Sistem desain dan pertanyaan Q1–Q8. |
| `LANJUTAN_REV1.md` | 14.153 | 2026-11-01 | Catatan lanjutan REV1 (tidak ter-track). |

Sumber di luar folder yang diminta dan **tidak diaudit**: `.kiro/specs/hs-classification-v2/{requirements,design,tasks}.md`. Ada kemungkinan sumber persyaratan tambahan. Perlu konfirmasi apakah ikut diaudit.

Berkas non-md yang dipakai sebagai bukti: `docs/db/20261101141839_AddAiUsageLogs.sql` (dibuat agent).

---

## B. Tabel persyaratan per dokumen

Singkatan bukti: `RE` = `HsClassifier.Application/Classification/Rules/RulesEngine.cs`, `CG` = `.../Classification/ConditionGate.cs`, `CC` = `.../Classification/ConfidenceCalculator.cs`, `FES` = `.../Classification/FactExtractionService.cs`, `CGS` = `HsClassifier.Infrastructure/Services/CandidateGeneratorService.cs`, `ORC` = `.../Services/ClassificationOrchestrator.cs`, `ERS` = `.../Services/ExpertReviewService.cs`, `TLS` = `.../Services/TariffLookupService.cs`, `VAL` = `.../Services/ClassificationValidator.cs`, `VM` = `.../Services/VersionManager.cs`, `AIC` = `.../Controllers/AdminImportController.cs`. Tes ada di `hs-classifier-api/HsClassifier.Tests/`. Tag di awal kolom Persyaratan menunjuk area: [Klasifikasi], [Riwayat], [Review Ahli], [Admin], [AI], [Auth/Role], [Data/BTKI], [Laporan/Ekspor], [Non-fungsional].

### B.1 `AI_HS_Classification_Intelligence_V2_Product_Logic_UI_and_Coding_Prompt.md` (master prompt, "MP")

#### B.1.1 Prinsip, validasi, pembagian tugas LLM (MP §PRINSIP L102, §PEMBAGIAN TUGAS L676, §TESTING L1704)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-001 | [Klasifikasi] Kode final 8 digit dan ada di master aktif (Acceptance 1) | Ada di kode | `VAL.ValidateAsync` L14: regex `^\d{4}\.\d{2}\.\d{2}$`, versi aktif, `IsActive` | Tidak ada tes khusus validator. Jalur ahli diuji `ExpertReviewTests.Invalid_approvals_are_refused_and_nothing_changes`. |
| R-002 | [Klasifikasi] Kode 4 digit tidak boleh menjadi rekomendasi final (Regression 3) | Ada dan teruji | `RE.ApplyKumHs6` L235; `KumHs6Tests.Heading_without_tariff_lines_never_fabricates_a_code`, `Single_tariff_line_is_deterministic` | |
| R-003 | [Klasifikasi] Uraian resmi diambil persis dari database (Acceptance 2) | Ada di kode | `VAL.ValidateAsync` mengembalikan `DescriptionId` dari `BtkiTariffLines` | Validator tidak membandingkan uraian dengan teks lain (lihat R-639). |
| R-004 | [Klasifikasi] Kode atau uraian yang tidak ditemukan exact di BTKI ditolak (Regression 2) | Ada di kode | `VAL`: `CODE_NOT_FOUND`, `PARENT_MISSING`, `INVALID_FORMAT`, `NO_ACTIVE_VERSION` | Status `VALIDATION_FAILED` di WORKFLOW tidak ada di enum kode. |
| R-005 | [AI] LLM dilarang menghasilkan kode HS, uraian, catatan, atau EN (MP L690; TL 7.1) | Ada di kode | `CGS.FromSuggestionAsync` L174; `ORC.TryAiSuggestionAsync` ~L293; `TermSuggestion.Headings` | **SEBAGIAN / KONFLIK.** LLM mengusulkan istilah dan pos 4 digit. Pos disaring: hanya yang ada sebagai baris 4 digit aktif di versi aktif. Kode 6 dan 8 digit ditolak. Lihat E-04. |
| R-006 | [AI] Kode palsu atau source ID palsu dari LLM ditolak (Adversarial) | Ada dan teruji | `CGS.FromSuggestionAsync` L180–198 (`Rejected`); `GroqParseTests.Six_and_eight_digit_codes_are_rejected`, `Broken_or_empty_output_returns_null` | Alur ini tidak menerima source ID dari LLM. |
| R-007 | [Klasifikasi] Setiap kandidat, aturan, dan tarif punya source_id | Ada di kode | `CGS` L182 (`SourceId` pada kandidat); `TLS` (`SourceId` pada `RateInfo`); `BtkiTariffLines.SourceId` | **SEBAGIAN.** Tidak diperiksa bahwa setiap `ReasoningStep` membawa source_id. |
| R-008 | [Klasifikasi] Hanya node aktif yang dipakai | Ada di kode | `VAL` (`t.IsActive`); `CGS` (`IsActive`); `TLS` (`r.IsActive`) | |
| R-009 | [Klasifikasi] Fakta hilang menghasilkan pertanyaan, bukan tebakan (Prinsip; Acceptance 9; Regression 1) | Ada dan teruji | `FES.ExtractFromInput` (fakta kosong = `Unknown`, tidak pernah default); `ThresholdTests.Missing_value_asks_a_numeric_question`; `GroqParseTests.Without_generic_name_asks_for_it_once`; `KumHs6Tests.Ambiguous_children_ask_from_official_texts_not_first_child` | Skenario "pompa industri" persis tidak diuji. |
| R-010 | [Klasifikasi] Confidence dihitung backend, bukan LLM (Acceptance 11) | Ada dan teruji | `CC.Compute`; `ORC` L204; `ConfidenceTests` (7 tes) | |
| R-011 | [Klasifikasi] Exclusion pada Catatan Hukum mengalahkan kemiripan (Acceptance 7) | Tidak jelas | `ChapterNotesText` dan `SectionNotesText` dimuat ke kandidat dan dipakai di `CC` (LegalNotesTested). `KumHs6Tests.Kum_hs_6_ignores_terms_inside_exclusion_clauses` hanya menyangkut teks subpos. | Tidak ditemukan kode yang mengeliminasi kandidat karena exclusion Catatan Bagian/Bab. |
| R-012 | [Klasifikasi] EN tidak boleh mengalahkan sumber legal (Acceptance 8) | Tidak jelas | `CC` memakai `EnReviewed`. Commit EN tidak tersedia (`AIC` L153, FLOW §10). | Praktis EN belum ikut keputusan. |
| R-013 | [Klasifikasi] Golden case tidak di-hard-code (Acceptance 13; Prinsip 15) | Tidak jelas | `CGS` L13 `_synonyms` (contoh `["laptop"]`) | Peta sinonim adalah data bantu, bukan jawaban per kasus. Perlu keputusan apakah melanggar prinsip. Lihat E-05. |

#### B.1.2 Ingestion database dan Admin Data Center (MP §A1–A4 L211–329, §B L330, §C L366, §D L401)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-020 | [Data/BTKI] Hierarki BTKI dari DOCX dibaca lewat style, outline, numbering, indent OOXML (A1) | Tidak ditemukan | `Parsing/DocxParser.cs` (317 baris); pencarian `Style`, `Numbering`, `Outline` tidak menemukan apa pun | `DocxParser` membaca isi tabel (kolom tarif di L139). Hierarki produksi datang dari `BtkiPdfParser`. |
| R-021 | [Data/BTKI] Pemetaan merged cell tarif (A1; Data tests) | Tidak ditemukan | Pencarian `GridSpan`, `VerticalMerge` di `Parsing/` kosong | |
| R-022 | [Data/BTKI] Kolom dwibahasa (ID/EN) sejajar (Data tests) | Tidak jelas | `BtkiPdfTableTests.Heading_wraps_over_two_lines_in_both_languages`, `Indonesian_word_ending_at_the_corridor_edge_is_not_moved_into_english` menguji jalur PDF | Jalur DOCX tidak diuji. |
| R-023 | [Data/BTKI] Tracked changes, teks tersembunyi, footnote ditangani (A4) | Tidak ditemukan | Pencarian `Inserted`, `Deleted`, `Vanish`, `Footnote` di `Parsing/` kosong | |
| R-024 | [Data/BTKI] Source locator ke halaman/paragraf/sel Word dan round-trip (A3; TL 2.1) | Tidak ditemukan | `SourceId` ada pada baris, tetapi tidak ada locator Word | |
| R-025 | [Data/BTKI] Verifikasi visual: render Word dibandingkan hasil parser (A3) | Tidak ditemukan | | |
| R-026 | [Admin] Laporan validasi import dan rekonsiliasi hasil parse | Ada dan teruji | `AmbiguityQueueTests.Upload_persists_report_and_queue_then_decisions_unlock_commit`; `BtkiPdfFileTests.Scanned_chapter_is_reported_not_guessed`; `BtkiPdfTableTests.Unrecognised_code_is_reported_not_silently_dropped` | **SEBAGIAN.** Rekonsiliasi jumlah kode terhadap total di dokumen Word tidak ditemukan. |
| R-027 | [Admin] Antrian ambiguitas dan gerbang commit/aktivasi | Ada dan teruji | `AIC` ambiguity/{itemId}/decide; `AmbiguityQueueTests.Gate_blocks_pending_and_applies_decisions`, `Activation_is_refused_while_latest_batch_has_pending_items`; UI tab "Antrian Ambiguitas" | |
| R-028 | [Data/BTKI] Parser dan commit Explanatory Notes dari DOCX (A2) | Ada di kode | `AIC` en/upload L153 | **SEBAGIAN.** Hanya parse. Commit tidak tersedia (FLOW §10). |
| R-029 | [Data/BTKI] Parser dan commit Catatan Bagian/Bab dari DOCX | Ada di kode | `AIC` notes/upload L181 | **SEBAGIAN.** Hanya parse. |
| R-030 | [Data/BTKI] Segmentasi paragraf EN tidak memutus konteks exclusion (Data tests) | Tidak jelas | Enum `ParagraphType` (`Enums.cs` L15) ada. Logika segmentasi tidak diperiksa. | |
| R-031 | [Admin] Mesin status import (Uploaded, Parsing, Mapped, Validating, … Active) (MP §C) | Ada dan teruji | `Enums.cs` `ImportState`; `VM`; `VersionLifecycleTests` (3 tes); `AmbiguityQueueTests` | |
| R-032 | [Admin] Satu versi aktif per rentang tanggal, versi yang tumpang tindih dinonaktifkan | Ada dan teruji | `VM.ActivateVersionAsync` L58; `VersionLifecycleTests.Activating_a_new_version_leaves_the_old_one_ready_for_rollback` | |
| R-033 | [Admin] Rollback versi tanpa kehilangan data audit (Acceptance 15) | Ada dan teruji | `VM.DeactivateVersionAsync` L90; `VersionLifecycleTests.Deactivate_then_reactivate_round_trips` | **SEBAGIAN.** Tidak ada tes bahwa audit data tetap utuh setelah rollback. |
| R-034 | [Admin] Bandingkan versi | Ada dan teruji | `VersionsController` diff; `VersionDiffTests` (3 tes) | |
| R-035 | [Admin] Import idempotent (Acceptance 14) | Ada dan teruji | `PdfImportCarryTests.A_version_that_already_has_rows_is_not_committed_twice`; `AmbiguityQueueTests.Clean_upload_goes_straight_to_ReadyToCommit_and_upload_after_that_is_refused` | |
| R-036 | [Data/BTKI] Normalisasi kode ke bentuk kanonik (MP §D) | Ada dan teruji | `BtkiPdfTableTests.Code_formats_map_to_kinds_and_normalised_codes`; `GroqParseTests.Valid_json_is_parsed_and_heading_dots_normalized` | |
| R-037 | [Data/BTKI] Tes data: kode ganda, orphan parent, panjang digit, leading zero, tanggal berlaku tumpang tindih, kode nonaktif, EN tanpa heading, cross-reference tidak valid | Ada dan teruji | `BtkiPdfTableTests.Duplicate_code_keeps_the_first_row_and_warns`, `Built_nodes_pass_the_existing_structure_dry_run_without_errors_or_ambiguity`; `VersionDiffTests.Duplicate_codes_in_one_version_count_once` | **SEBAGIAN.** Leading zero, tanggal tumpang tindih, kode nonaktif, EN tanpa heading, cross-reference tidak punya tes yang ditemukan. |
| R-038 | [Admin] Tombol dan tab Admin Data Center (MP §B L330) | Hanya teruji manual/stub | `AdminDashboard.jsx` (6 tab: versi, upload dokumen, laporan validasi, antrian ambiguitas, template, Status AI); CHECKLIST A1–A12 | **SEBAGIAN.** Tidak semua tombol di MP §B dibuat; daftar tombol MP tidak dicocokkan satu per satu. |
| R-039 | [Admin] Backup dataset (Security 13) | Tidak ditemukan | | Rollback ada (R-033), backup tidak. |
| R-040 | [Admin] Catat tindakan admin terhadap database (Security 12) | Tidak ditemukan | Hanya `CreatedBy` pada batch import | Tidak ada tabel `audit_logs`. |

#### B.1.3 Model data minimum (MP L425–675)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-050 | [Data/BTKI] Tabel `nomenclature_version` | Ada di kode | `NomenclatureVersions` | Nama tabel berbeda dari MP. |
| R-051 | [Data/BTKI] Tabel `tariff_node` | Ada di kode | `BtkiTariffLines` | |
| R-052 | [Data/BTKI] Tabel `tariff_rate` | Ada di kode | `MfnRates`, `FtaRates`, `TaxRates`; `MfnParseTests` (2 tes parse) | |
| R-053 | [Data/BTKI] Tabel `legal_note` | Ada di kode | `SectionNotes`, `ChapterNotes`, `SubheadingNotes`; enum `EffectType` | |
| R-054 | [Data/BTKI] Tabel EN document dan paragraph | Ada di kode | `ExplanatoryNotes` | **SEBAGIAN.** Tabel ada, jalur commit tidak (R-028). |
| R-055 | [Data/BTKI] Tabel `kum_hs_rule` | Tidak ditemukan | Aturan ada di kode `RE`. `data/kumhs_rules.json` bukan tabel. | |
| R-056 | [Klasifikasi] Tabel `fact_definition` | Tidak ditemukan | Definisi fakta berupa array `FES.AllFactTypes` | |
| R-057 | [Klasifikasi] Tabel `decision_requirement` | Tidak ditemukan | | |
| R-058 | [Klasifikasi] Tabel `classification_case` | Ada di kode | `ClassificationCases` (termasuk `ConfidenceScore`, `ConfidenceJson`) | |
| R-059 | [Klasifikasi] Tabel `fact_observation` | Tidak jelas | Tidak ada tabel. Fakta tersimpan di dalam case. | Bentuk penyimpanan belum diperiksa rinci. |
| R-060 | [Klasifikasi] Tabel `candidate_evaluation` | Tidak jelas | Tidak ada tabel. Kandidat tampil lewat `ReasoningEvents`. | |
| R-061 | [Klasifikasi] Tabel `reasoning_event` | Ada di kode | `ReasoningEvents` | |
| R-062 | [Review Ahli] Tabel `expert_correction` | Ada dan teruji | `ExpertCorrections`; `ExpertReviewTests` (6 tes) | |
| R-063 | [Non-fungsional] Rantai hash `event_hash` / `previous_event_hash` antar event | Tidak ditemukan | Pencarian tidak menemukan apa pun selain checksum SHA-256 berkas dan hash password | |

#### B.1.4 Fakta dan kandidat (MP L738–847)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-070 | [Klasifikasi] Ekstraksi 35 tipe fakta | Ada di kode | `FES.AllFactTypes` L14 (lebih dari 35 entri, termasuk fakta kondisi barang) | |
| R-071 | [Klasifikasi] Status fakta unknown/not applicable/zero/conflicting/verified | Ada di kode | `ClassificationModels.cs` `FactStatus` | |
| R-072 | [Klasifikasi] Deteksi fakta bertentangan antar sumber | Ada di kode | `FES.MergeLlmFacts` L85 | **SEBAGIAN.** Tidak ada pemanggil `MergeLlmFacts`, jadi deteksi konflik tidak jalan di alur nyata. |
| R-073 | [Klasifikasi] Pertanyaan diurutkan menurut information gain (TL 6.2) | Tidak jelas | `QuestionGeneratorService.cs` (128 baris) | Urutan berdasarkan information gain tidak terbukti. |
| R-074 | [Klasifikasi] Ekstraksi fakta dari dokumen yang diunggah (invoice, datasheet, foto) | Tidak ditemukan | Tidak ada endpoint atau UI upload dokumen pengguna | Lihat R-132. |
| R-075 | [Klasifikasi] Normalisasi satuan dan unit converter | Ada dan teruji | `ThresholdTests.Quantities_from_user_answers` (kW, kVA, cc, liter, t); `FES.NormalizeValue` (berat) | **SEBAGIAN.** Tidak ada unit converter di UI. |
| R-076 | [Klasifikasi] Fact gate: fakta penentu dicek sebelum kandidat diputuskan | Ada dan teruji | `ORC`; `CG.Evaluate` L41; `ConditionGateTests` (39 tes) | |
| R-080 | [Klasifikasi] 14 strategi pembentukan kandidat (MP L812) | Ada di kode | `CGS` L64–99: lexical, sinonim, fungsi, material, overlap kata, sub-frasa; `PartsHeadingsForAsync`, `ScrapHeadingsAsync`, `FromSuggestionAsync` | **SEBAGIAN.** Lintas-referensi EN, rujukan Catatan Hukum, dan semantik/embedding tidak ditemukan. |
| R-081 | [Klasifikasi] Pengayaan kandidat: hierarki, catatan, subpos anak, source_id | Ada di kode | `CGS` `EnrichAsync` ~L340–376 | **SEBAGIAN.** Paragraf EN tidak ikut (R-028). |

#### B.1.5 Urutan penafsiran KUM HS 1–6 dan logic kondisi barang (MP L848–1157, §TESTING "KUM HS tests")

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-090 | [Klasifikasi] Tahap 0: validasi referensi (tanggal, versi) | Ada di kode | `VAL`, `CGS` memakai `GetActiveVersionAsync(DateTime.UtcNow)` | **SEBAGIAN.** Tanggal klasifikasi tidak bisa dipilih pengguna (R-130). |
| R-091 | [Klasifikasi] KUM HS 1 selalu diuji, heading langsung | Ada dan teruji | `RE.ApplyKumHs1` L75; `KumHs6Tests.Kum_hs_1_matches_whole_words_only`, `Kum_hs_1_uses_heading_text_not_child_line_mentions`, `Heading_about_electric_motors_beats_hand_tools_…` | |
| R-092 | [Klasifikasi] KUM HS 1 satu heading retained: loncat ke KUM HS 6 | Ada di kode | `RE.ApplyKumHs` L16 (early stop ~L34) | Tidak ada tes khusus early stop. |
| R-093 | [Klasifikasi] KUM HS 1 exclusion / referral | Tidak jelas | Lihat R-011 | |
| R-094 | [Klasifikasi] KUM HS 2(a) barang tidak lengkap dan CKD/SKD | Ada dan teruji | `CG` modul Incomplete L68; `ConditionGateTests.Ckd_asks_essential_character_and_same_shipment`, `Ckd_failing_2a_goes_to_expert`, `Incomplete_only_needs_essential_character` | |
| R-095 | [Klasifikasi] KUM HS 2(b) campuran material, hasil 2+ pos lanjut ke KUM HS 3 (TL 9.2) | Ada di kode | `RE.ApplyKumHs2` L154 hanya mencatat langkah, tidak mengeliminasi | **SEBAGIAN.** Modul Mixture di `CG` L139 teruji (R-119), tetapi pencabangan 2(b) ke 3 tidak. |
| R-096 | [Klasifikasi] KUM HS 3(a) pos paling spesifik | Ada di kode | `RE.ApplyKumHs3` L178 (skor = HeadingScore×10 + 2×Focus + SpecificityScore) | Tidak ada tes khusus. |
| R-097 | [Klasifikasi] KUM HS 3(a) proviso "sama spesifik" | Tidak jelas | | |
| R-098 | [Klasifikasi] KUM HS 3(b) campuran, komposit, set eceran | Ada dan teruji | Set eceran dan campuran: `ConditionGateTests.Valid_retail_set_classifies_by_essential_item`, `Mixture_needs_component_and_percentage_then_uses_essential_component` | **SEBAGIAN.** Barang komposit tidak punya modul (R-110). Langkah 3(b) di `RE` L178 disederhanakan. |
| R-099 | [Klasifikasi] KUM HS 3(b) enam faktor berbobot (fungsi 30%, material 15%, bulk 15%, nilai 15%, kebutuhan 15%, persepsi 10%) (MP L939; WF L346; TL 10.2) | Tidak ditemukan | `RE.ApplyKumHs3` hanya mencocokkan fakta `material` dengan uraian pos | |
| R-100 | [Klasifikasi] KUM HS 3(c) nomor terbesar | Ada di kode | `RE.ApplyKumHs3` (3(c) nomor numerik terakhir) | Tidak ada tes khusus. |
| R-101 | [Klasifikasi] KUM HS 4 barang paling mirip, tampilkan barang pembanding dan alasan, review ahli untuk risiko tinggi | Ada di kode | `RE` KUM HS 4 mengambil `retained.FirstOrDefault()` lalu `NeedExpertReview` | **SEBAGIAN.** Tidak ada perbandingan material/bentuk/fungsi, tidak ada barang pembanding. |
| R-102 | [Klasifikasi] KUM HS 5(a) wadah khusus dan 5(b) bahan kemas | Ada dan teruji | `CG` modul Packaging L388; `ConditionGateTests.Asks_kind_and_contents_first`, `Packing_5b_with_goods_unless_reusable`, `Unsure_goes_to_expert` | |
| R-103 | [Klasifikasi] Penetapan pos 4 digit: satu retained, semua eliminated tersimpan, alasan dan source | Ada di kode | `RE`; `ORC` menyimpan `ReasoningEvents` | Eliminasi tersimpan, kelengkapan source belum diperiksa. |
| R-104 | [Klasifikasi] KUM HS 6 hanya membandingkan tingkat yang sama | Ada dan teruji | `RE.NarrowByParentHeading` L341; `KumHs6Tests.Kum_hs_6_descends_level_by_level_with_user_choices`, `Choice_from_another_heading_is_ignored` | |
| R-105 | [Klasifikasi] KUM HS 6 ambang angka hilang menghasilkan pertanyaan | Ada dan teruji | `ThresholdTests.Missing_value_asks_a_numeric_question`, `Known_value_selects_the_single_matching_branch`, `Engine_uses_thresholds_before_asking_for_a_choice` | |
| R-106 | [Klasifikasi] KUM HS 6 menolak perbandingan indent yang tidak valid | Ada dan teruji | `ThresholdTests.Group_text_with_too_many_dashes_is_misplaced_and_ignored`, `Group_text_contradicting_its_own_lines_is_not_trusted` | |
| R-110 | [Klasifikasi] Logic barang komposit (MP L1083) | Tidak jelas | `CG` tidak punya modul Composite. Mixture L139 mungkin mencakup sebagian. | |
| R-111 | [Klasifikasi] Logic set eceran | Ada dan teruji | `CG` RetailSet L105; `ConditionGateTests.Retail_set_asks_three_tests`, `Not_a_retail_set_goes_to_expert_for_item_by_item` | |
| R-112 | [Klasifikasi] Logic part | Ada dan teruji | `CG` Part L163; `ConditionGateTests.Dedicated_part_routes_to_parent_machine`, `General_use_part_is_classified_as_its_own_article`, `Parts_heading_branch_citing_parent_heading_is_kept` | |
| R-113 | [Klasifikasi] Logic accessory | Ada dan teruji | `CG` (aksesori); `ConditionGateTests.Necessary_accessory_is_a_part`, `Standalone_accessory_is_classified_by_itself`, `Dedicated_accessory_routes_to_parts_and_accessories_heading` | |
| R-114 | [Klasifikasi] Logic mesin multifungsi | Ada dan teruji | `CG` Multifunction L235; `ConditionGateTests.Note3_principal_function_decides`, `Note4_functional_unit` | |
| R-115 | [Klasifikasi] Logic barang curah | Ada dan teruji | `CG` Bulk L292; `ConditionGateTests.Bulk_records_state_quantity_and_presentation`, `Bulk_net_quantity_feeds_numeric_thresholds` | |
| R-116 | [Klasifikasi] Logic barang bekas, waste, scrap | Ada dan teruji | `CG` UsedOrScrap L323; `CGS.ScrapHeadingsAsync` L147; `ConditionGateTests.Used_goods_that_still_work_are_classified_as_new`, `Unusable_unrepairable_used_goods_become_scrap` | |
| R-117 | [Klasifikasi] Logic kemasan atau wadah | Ada dan teruji | Lihat R-102 | |
| R-118 | [Klasifikasi] Logic CKD/SKD/unassembled | Ada dan teruji | `ConditionGateTests.Ckd_with_essential_character_and_complete_kit_is_treated_as_complete` | |
| R-119 | [Klasifikasi] Logic campuran (mixture) | Ada dan teruji | `CG` Mixture L139; `ConditionGateTests.Mixture_needs_component_and_percentage_then_uses_essential_component` | |

#### B.1.6 UI V2 dan navigasi global (MP L1158–1421)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-130 | [Klasifikasi] STEP 1 referensi: tanggal impor/ekspor, versi nomenklatur, versi BTKI, edisi EN, bahasa; tombol Konfirmasi Referensi, Lihat Peraturan, Bandingkan Versi | Tidak ditemukan | `ClassificationForm.jsx` tidak punya field referensi | |
| R-131 | [Klasifikasi] STEP 2 identitas barang: nama dagang, nama generik, brand/model, manufacturer, SKU, deskripsi invoice, HS supplier, keterangan | Hanya teruji manual/stub | `ClassificationForm.jsx` (14 field); CHECKLIST K1–K7 | **SEBAGIAN.** Manufacturer, SKU, HS supplier tidak ditemukan. |
| R-132 | [Klasifikasi] STEP 2 tombol Upload Invoice/Packing List/Catalogue/Datasheet/Drawing/Foto/MSDS, Ekstrak Fakta, Lihat Hasil Ekstraksi | Tidak ditemukan | Pencarian `type="file"` di `hs-classifier/src` hanya menemukan upload admin (`AdminDashboard.jsx`) | |
| R-133 | [Klasifikasi] STEP 3 kondisi saat diimpor: 17 pilihan, multi-select | Hanya teruji manual/stub | `ClassificationForm.jsx` radio `apakah_part`, `apakah_set`, `ada_komponen_tambahan`; CHECKLIST K | **SEBAGIAN.** Kondisi lain ditanyakan lewat klarifikasi dinamis (R-137). |
| R-134 | [Klasifikasi] STEP 4 fisik, material, komposisi: Tambah Material/Ingredient/Komponen/Coating, persentase, unit converter, Tandai Tidak Diketahui | Hanya teruji manual/stub | `ClassificationForm.jsx` (field material dan ukuran) | **SEBAGIAN.** Tombol tambah dan unit converter tidak ada. |
| R-135 | [Klasifikasi] STEP 5 fungsi dan cara kerja: fungsi sekunder, input/process/output, sumber tenaga, functional unit | Hanya teruji manual/stub | `ClassificationForm.jsx` | **SEBAGIAN.** |
| R-136 | [Klasifikasi] STEP 6 penggunaan dan konteks: principal/actual use, industri, area, mobilitas, mounting, parent machine, target user | Hanya teruji manual/stub | `ClassificationForm.jsx` | **SEBAGIAN.** |
| R-137 | [Klasifikasi] STEP 7 panel dinamis kondisi khusus (11 panel) | Hanya teruji manual/stub | `App.jsx` `ClarificationBanner` di `Modal`; pertanyaan dari `CG` dan `QuestionGeneratorService`; CHECKLIST C1–C6 | **SEBAGIAN.** Pertanyaan dinamis ada, panel per kondisi tidak. |
| R-138 | [Klasifikasi] STEP 8 validasi fakta: Lihat Fakta Kurang/Konflik, Unggah Bukti, Konfirmasi Fakta, Bentuk Kandidat | Hanya teruji manual/stub | Modal klarifikasi (CHECKLIST C) | **SEBAGIAN.** Tombol dan layar khusus tidak ada. |
| R-139 | [Klasifikasi] STEP 9 kartu kandidat (11 elemen) dan tombol Pertahankan/Tantang/Tambahkan Kandidat/Jalankan Legal Test | Tidak ditemukan | `ResultPanel.jsx` hanya menampilkan jumlah kandidat dieliminasi. `ReviewPage.jsx` menampilkan kandidat untuk ahli. | Lihat D. |
| R-140 | [Klasifikasi] STEP 10 sembilan tab legal reasoning | Hanya teruji manual/stub | `ResultPanel.jsx` reasoning `<details>` per langkah; CHECKLIST R11 | **SEBAGIAN.** Tab dan Decision Tree tidak ada. |
| R-141 | [Klasifikasi] STEP 10 tombol Jalankan Ulang, Buka Sumber, Lihat Rule Trigger, Jawab Klarifikasi, Koreksi Fakta, Minta Expert Review | Ada dan teruji | Minta Expert Review: `ExpertReviewTests.Request_review_moves_determined_case_to_queue_keeping_code_as_candidate`; `ResultPanel.jsx` | **SEBAGIAN.** Hanya Minta Expert Review dan Jawab Klarifikasi yang ada. |
| R-142 | [Laporan/Ekspor] STEP 11 rekomendasi: status, kode 8 digit, uraian resmi, legal identity, path, dasar hukum, retained/eliminated, fakta material, konfirmasi yang hilang, confidence, risk, ringkasan audit | Hanya teruji manual/stub | `ResultPanel.jsx`; CHECKLIST R1–R13 | **SEBAGIAN.** Legal identity, dasar hukum terstruktur, dan ringkasan audit tidak ada sebagai blok. |
| R-143 | [Klasifikasi] Panel "Tarif dari BTKI": MFN, tarif FTA per skema/negara, PPN, PPh 22 | Ada di kode | `TLS.GetRatesAsync` L14; `ResultPanel.jsx` (tabel tarif); CHECKLIST R | Tidak ada tes `TariffLookupService`. |
| R-144 | [Klasifikasi] Panel tarif menampilkan tanggal berlaku dan source reference | Ada di kode | `TariffResult` punya `SourceId`, tidak punya tanggal berlaku | **SEBAGIAN.** |
| R-145 | [Klasifikasi] Status tarif `RATE_REFERENCE_ONLY` / `FTA_ELIGIBILITY_CONFIRMED` | Tidak ditemukan | Hanya teks disclaimer (`TLS` L9) | |
| R-146 | [Klasifikasi] Peringatan: tarif FTA bukan kelayakan, perlakuan PMK untuk PPN/PPh 22 belum dianalisis (Acceptance 17–18) | Ada di kode | `TLS.FtaDisclaimer` dan `TaxDisclaimer` L9–L16; `ResultPanel.jsx`; CHECKLIST R | |
| R-147 | [Klasifikasi] Tarif hanya dari record BTKI aktif (Acceptance 16) | Ada di kode | `TLS.GetRatesAsync`: versi aktif dan `IsActive` | |
| R-148 | [Laporan/Ekspor] Tombol Lihat KADL, Ekspor DOCX, Ekspor PDF, Ekspor JSON | Tidak ditemukan | Tidak ada endpoint export di `ClassificationController`; frontend hanya mengunduh template admin (`AdminDashboard.jsx` `TemplatePanel.download` ~L703) | Lihat D. |
| R-149 | [Review Ahli] Tombol Setujui Rekomendasi dan Tolak Rekomendasi | Ada dan teruji | `ReviewController` approve/reject; `ReviewPage.jsx`; `ExpertReviewTests.Approve_within_candidate_heading_is_EXPERT_APPROVED_and_audited`, `Reject_sets_rejected_and_clears_code` | |
| R-150 | [Klasifikasi] Tombol Koreksi Fakta, Buat Versi Baru, Bandingkan Keputusan | Tidak ditemukan | | |
| R-151 | [Klasifikasi] Tombol Lihat Tarif MFN/FTA, Pilih Skema FTA, Bandingkan MFN vs FTA, Buka Sumber Tarif | Tidak ditemukan | Tabel tarif menampilkan semua skema sekaligus | |
| R-152 | [Klasifikasi] Navigasi: + Klasifikasi Baru, Daftar Case, Case Expert Review, Admin Data Center | Hanya teruji manual/stub | `App.jsx` `VIEWS` L12; CHECKLIST G | Label di kode: Klasifikasi, Riwayat, Review Ahli, Admin. |
| R-153 | [Klasifikasi] Navigasi: Simpan Draft | Tidak ditemukan | | |
| R-154 | [Non-fungsional] Navigasi: Golden Cases, Knowledge Base, Audit Log, User & Role, Settings | Tidak ditemukan | FLOW §10 juga menyatakan belum tersedia | |

#### B.1.7 Status, output, confidence, audit, keamanan, tes (MP L1422–1795)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-160 | [Klasifikasi] 16 status case (DRAFT … SUPERSEDED) | Ada di kode | `Enums.cs` L16 `CaseStatus { Determined, NeedClarification, NeedExpertReview, Rejected }`; `ERS` L20–25 menambah string `EXPERT_APPROVED`, `EXPERT_OVERRIDE`, `EXPERT_REJECTED`, `REVIEW_REQUESTED` | **SEBAGIAN.** Empat enum + empat status string. Lihat E-08. |
| R-161 | [Klasifikasi] `RECOMMENDED` hanya jika semua gerbang validasi lulus | Ada di kode | `ORC` memanggil `VAL` sebelum `Determined` | **SEBAGIAN.** Nama status berbeda. |
| R-162 | [Laporan/Ekspor] Skema JSON output bertipe (MP L1449–1552) | Tidak jelas | DTO respons `ClassificationController` | Belum dibandingkan field demi field. |
| R-163 | [Laporan/Ekspor] Format output manusia (STATUS … FINAL OUTPUT), tanpa KUM HS yang tidak dipakai | Hanya teruji manual/stub | `ResultPanel.jsx`; CHECKLIST R | **SEBAGIAN.** |
| R-170 | [Klasifikasi] Bobot confidence 25/20/15/15/15/10 | Ada dan teruji | `CC.Compute`; `ConfidenceTests.Weights_follow_document_25_20_15_15_15_10` | |
| R-171 | [Klasifikasi] Batas confidence (fakta tidak lengkap maks 69, Catatan belum diuji maks 60, EN belum tersedia maks 75, ambiguitas maks 59) | Ada dan teruji | `CC`; `ConfidenceTests.Legal_notes_not_tested_caps_at_60`, `En_not_reviewed_caps_at_75`, `Missing_decisive_fact_caps_at_69_and_lowest_cap_wins` | |
| R-172 | [Klasifikasi] Risk flags (GRI 4, GRI 3(c), saran AI, kandidat kembar) | Ada dan teruji | `ConfidenceTests.Gri4_and_3c_and_ai_are_flagged` | |
| R-173 | [Klasifikasi] Classification risk sebagai tingkat tersendiri | Tidak ditemukan | Hanya daftar flag | |
| R-174 | [Klasifikasi] Confidence dapat direkonstruksi | Ada di kode | `ConfidenceReport` (komponen, cap, flag); `ConfidenceJson` pada case | |
| R-180 | [Non-fungsional] Audit trail immutable: input, fakta, kandidat, trigger tiap KUM HS, retained/eliminated, validator, confidence, risk | Ada di kode | `ReasoningEvents`; `GET /api/classification/{id}/reasoning`; `ExpertCorrection` bertanda immutable | **SEBAGIAN.** Tidak ada penegakan immutable di level database, tidak ada hash chain (R-063). |
| R-181 | [Non-fungsional] Audit dokumen pengguna dan checksum, hasil OCR | Tidak ditemukan | Checksum hanya untuk berkas import admin | Tergantung R-074. |
| R-182 | [Non-fungsional] Audit versi model, prompt, dan engine per case | Tidak ditemukan | `AiUsageLogs` mencatat provider/model per request, bukan per case | |
| R-183 | [Non-fungsional] Audit ekspor dan keputusan yang digantikan (superseded) | Tidak ditemukan | | Tergantung R-148, R-150. |
| R-184 | [Review Ahli] Koreksi dan persetujuan ahli tercatat | Ada dan teruji | `ExpertReviewTests.Approve_within_candidate_heading_is_EXPERT_APPROVED_and_audited` | |
| R-190 | [Non-fungsional] Dokumen unggahan diperlakukan sebagai untrusted, instruksi di dalamnya diabaikan | Tidak jelas | Tidak ada upload dokumen pengguna. Upload admin hanya diparse. | Belum relevan sampai R-074 dibuat. |
| R-191 | [Non-fungsional] Validasi MIME, ekstensi, magic bytes, ukuran berkas | Ada di kode | `AIC` L32 (ekstensi `.docx`/`.pdf`), L57 `RequestSizeLimit(100_000_000)`, L158 dan L186 (ekstensi) | **SEBAGIAN.** MIME dan magic bytes tidak ditemukan. |
| R-192 | [Non-fungsional] Scan malware | Tidak ditemukan | | |
| R-193 | [Non-fungsional] Tenant isolation | Tidak ditemukan | Pencarian `tenant` di .cs non-migrasi tidak menemukan kode nyata | Pemisahan hanya per pengguna (R-257). |
| R-194 | [Auth/Role] RBAC | Ada dan teruji | `[Authorize]` kelas pada controller admin; `CaseHistoryTests.Role_visibility`, `Classifier_sees_only_own_cases_reviewer_sees_all`; `AdminAiControllerGuardTests` (7 tes) | |
| R-195 | [Auth/Role] Enam role: USER, CLASSIFIER, EXPERT_REVIEWER, DATA_ADMIN, SYSTEM_ADMIN, AUDITOR | Tidak jelas | `Enums.cs` L17 `UserRole { Admin, Classifier, Viewer, ExpertReviewer }` | KONFLIK: empat role di kode. Lihat E-03. |
| R-196 | [Non-fungsional] API key tidak di frontend atau repository | Ada di kode | `hs-classifier/.env.example` hanya `VITE_API_URL`; `apiClient.js` L7 hanya memanggil backend; `appsettings.json` dipindai bersih | **SEBAGIAN.** `appsettings.Development.json` (berisi `Jwt:Key`) masih ter-track di `feature/ai-status`. Branch `chore/untrack-build-files` yang melepasnya belum digabung. |
| R-197 | [Non-fungsional] Redact data sensitif di log | Ada di kode | `Services/AiCall.cs` L7: metadata AI tanpa key, prompt, atau respons | **SEBAGIAN.** Log aplikasi lain tidak ditinjau. |
| R-198 | [Non-fungsional] Rate limit dan retry policy | Ada di kode | Retry: `apiClient.js` `apiFetch` (`attempt`, `sleep` L37). Batas cek AI sekali per ~10 detik di `AdminAiController`. | **SEBAGIAN.** Rate limit server untuk klasifikasi tidak ditemukan (tidak ada `AddRateLimiter`). |
| R-200 | [Klasifikasi] Regression 1: "pompa industri" tidak otomatis jadi pompa sentrifugal dengan confidence tinggi | Tidak ditemukan | Tidak ada tes dengan skenario ini | Perilaku umum "tanya, jangan tebak" teruji (R-009). |
| R-201 | [Klasifikasi] Regression 4: laptop dengan kamera, speaker, mikrofon diuji tanpa jawaban hard-coded | Tidak ditemukan | Tidak ada tes klasifikasi laptop | |
| R-202 | [Klasifikasi] Regression 5: dumper bernama dagang tractor | Tidak ditemukan | `docs/DUMPER TRACTOR.pdf` ada sebagai bahan, tanpa tes | |
| R-203 | [Klasifikasi] Regression 6: crane di atas barge, semua kandidat dan EN diuji | Tidak ditemukan | `docs/ANALISA KUM HS & EN CRANE BARGE.pdf` ada sebagai bahan, tanpa tes | |
| R-210 | [Klasifikasi] Daftar 17 tes KUM HS (1, 2(a), 2(b), 3(a), 3(b), 3(c), 4, 5(a), 5(b), 6) | Ada dan teruji | Lihat R-091 s/d R-106 | **SEBAGIAN.** Tidak ada tes untuk 2(b), 3(a), 3(c), 4. |
| R-212 | [AI] Adversarial: prompt injection dalam invoice atau catalogue | Tidak ditemukan | Tidak ada tes dan tidak ada upload | |
| R-213 | [AI] Adversarial: pengguna meminta AI mengabaikan Catatan | Tidak ditemukan | | |
| R-214 | [AI] Adversarial: HS supplier bertentangan dengan fakta | Tidak ditemukan | HS supplier tidak diinput (R-131) | |
| R-215 | [Klasifikasi] Adversarial: dokumen berbeda menyebut berat berbeda | Tidak ditemukan | | |
| R-216 | [Klasifikasi] Adversarial: OCR salah baca desimal atau koma | Ada dan teruji | `ThresholdTests.Quantities_from_user_answers` ("1.000 cc" dibaca sebagai ribuan) | **SEBAGIAN.** Input teks pengguna, bukan OCR. |
| R-217 | [Klasifikasi] Adversarial: satuan tidak didukung, bahasa campuran | Tidak jelas | | |
| R-218 | [Klasifikasi] Adversarial: produk tidak dikenal | Ada dan teruji | `GroqParseTests.With_generic_name_goes_to_expert_review`, `Without_generic_name_asks_for_it_once` | |
| R-219 | [Non-fungsional] Acceptance 10: setiap kandidat punya alasan dan sumber | Ada di kode | `ReasoningStep` per kandidat di `RE` | **SEBAGIAN.** Kelengkapan sumber tidak diperiksa. |


### B.2 `FLOW_BISNIS_V2.md` (FB)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-250 | [Auth/Role] Empat peran: Classifier, Expert, Admin, Viewer (FB §3) | Ada di kode | `Enums.cs` L17 `UserRole`; `App.jsx` `VIEWS` L12 dengan daftar role | Nama Expert di kode: `ExpertReviewer`. |
| R-251 | [Auth/Role] Login dengan JWT; pendaftaran pengguna hanya oleh Admin | Hanya teruji manual/stub | `AuthController` login dan register; `JwtService`; `PasswordHashing` (PBKDF2); `App.jsx` layar login; CHECKLIST G | Tidak ada tes backend untuk login. |
| R-252 | [Klasifikasi] Alur Classifier: isi form, hasil final atau pertanyaan klarifikasi (FB §5.1) | Hanya teruji manual/stub | `ClassificationForm.jsx`, `App.jsx`, `apiClient.classifyWithBackend`, `submitAnswers`; CHECKLIST K, C, R | Logika backend teruji sebagian (R-076, R-104). |
| R-253 | [Klasifikasi] Pertanyaan subpos KUM HS 6 dari uraian resmi (FB §5.2) | Ada dan teruji | `RE.BuildSubheadingQuestion` L403; `KumHs6Tests.Ambiguous_children_ask_from_official_texts_not_first_child`, `None_of_these_goes_to_expert_review` | |
| R-254 | [AI] Bantuan AI untuk nama pasar atau merek: terjemah ke istilah, kandidat tetap dari database (FB §5.3) | Ada dan teruji | `ORC.TryAiSuggestionAsync` ~L293; `GroqTermSuggester.Parse`; `GroqParseTests` (6 tes); `AiUsageLoggingTests` | Lihat E-04 untuk usulan pos 4 digit. |
| R-255 | [Klasifikasi] Hasil memuat kode 8 digit, uraian resmi, confidence Tinggi/Sedang/Rendah, tarif beserta sumber, disclaimer, reasoning yang bisa dibuka (FB §5.4) | Hanya teruji manual/stub | `ResultPanel.jsx`; CHECKLIST R1–R13; `CC.Level` | Skor dan batas Tinggi/Sedang/Rendah teruji (`ConfidenceTests.Level_bands`). |
| R-256 | [Klasifikasi] Kemungkinan hasil: Ditentukan, Perlu klarifikasi, Review ahli, Disetujui ahli, Dikoreksi ahli, Ditolak ahli (FB §5.5) | Ada dan teruji | `ERS` L20–25; `ExpertReviewTests`; `StatusBadge` di `HistoryPage.jsx` | |
| R-257 | [Riwayat] Riwayat: Classifier melihat miliknya, Expert dan Admin melihat semua; filter status, teks, rentang tanggal (FB §5.6) | Ada dan teruji | `CaseQueries.SeesAll`; `CaseHistoryTests.Role_visibility`, `Classifier_sees_only_own_cases_reviewer_sees_all`, `Filters_by_status_text_and_inclusive_date_range`; `HistoryPage.jsx`; CHECKLIST H1–H7 | |
| R-258 | [Review Ahli] Case masuk antrian ahli bila: tidak ada pos, pengguna pilih "tidak ada yang sesuai", KUM HS 4 dipakai, pengguna menekan Minta Expert Review (FB §6.1) | Ada dan teruji | `GroqParseTests.With_generic_name_goes_to_expert_review`; `KumHs6Tests.None_of_these_goes_to_expert_review`; `RE` KUM HS 4; `ExpertReviewTests.Request_review_moves_determined_case_to_queue_keeping_code_as_candidate` | Pemicu KUM HS 4 tidak punya tes (R-101). |
| R-259 | [Review Ahli] Ahli memilih kandidat atau mengetik kode lain lalu Cek di BTKI, atau menolak (FB §6.2) | Ada dan teruji | `ReviewController`; `BtkiController` `GET api/btki/{code}`; `ReviewPage.jsx`; `ExpertReviewTests`; CHECKLIST E1–E6 | |
| R-260 | [Review Ahli] Alasan wajib pada setiap keputusan (FB §6.3) | Ada dan teruji | `ERS.ReasonError` L87; `ExpertReviewTests.Invalid_approvals_are_refused_and_nothing_changes` | Panjang minimal: lihat R-901 dan E-02. |
| R-261 | [Review Ahli] Kode keputusan ahli harus ada di BTKI aktif (FB §6.3) | Ada dan teruji | `ExpertReviewTests.Invalid_approvals_are_refused_and_nothing_changes` | |
| R-262 | [Review Ahli] `EXPERT_APPROVED` untuk kode dari kandidat, `EXPERT_OVERRIDE` untuk kode di luar kandidat | Ada dan teruji | `ExpertReviewTests.Approve_within_candidate_heading_is_EXPERT_APPROVED_and_audited`, `Valid_code_outside_candidates_is_EXPERT_OVERRIDE` | |
| R-263 | [Review Ahli] Keputusan tidak bisa dihapus, tampil di "Riwayat keputusan ahli" pada detail case | Ada di kode | `ExpertCorrection` ("Immutable record"); tidak ada endpoint hapus pada controller | Tampilan riwayat keputusan di detail case tidak diperiksa terhadap CHECKLIST. |
| R-264 | [Review Ahli] Sistem mencatat siapa, kapan, kode sebelum dan sesudah, alasan (FB §6.2 langkah 5) | Ada dan teruji | `ExpertReviewTests.Approve_within_candidate_heading_is_EXPERT_APPROVED_and_audited` | |
| R-265 | [Admin] Siklus hidup versi BTKI (FB §7.1) | Ada dan teruji | `VersionsController`; `VersionLifecycleTests`; `AmbiguityQueueTests`; CHECKLIST A | |
| R-266 | [Admin] Jalur alternatif template CSV/Excel (FB §7.2) | Ada di kode | `AdminTemplateController` (download, validate, preview, commit); `Template/TemplateIngestionService.cs`; `AdminDashboard.jsx` `TemplatePanel` | Tidak ada tes backend untuk template. UI: CHECKLIST A. |
| R-267 | [Klasifikasi] Validator gagal mengirim case ke review ahli (FB §8) | Tidak jelas | `VAL` mengembalikan alasan gagal. Penanganan di `ORC` belum ditelusuri. | |
| R-268 | [Data/BTKI] Batasan §10: pembeda angka belum dibaca otomatis | Ada dan teruji | `ThresholdTests` (14 tes) | KONFLIK dokumen dan kode: kode sudah membaca ambang. FLOW usang. Lihat E-07. |
| R-269 | [Data/BTKI] Batasan §10: DOCX BTKI belum menangkap semua pos, data dari CSV | Ada dan teruji | Kode punya `BtkiPdfParser` dan `BtkiPdfTableTests` (19 tes), `BtkiCsvSeeder` | KONFLIK: jalur PDF tidak disebut di md mana pun. Lihat E-07. |
| R-270 | [Data/BTKI] Batasan §10: commit EN dan Catatan Bagian/Bab belum tersedia | Tidak ditemukan | Sesuai dengan kode (R-028, R-029) | Dokumen dan kode cocok. |
| R-271 | [Klasifikasi] Batasan §10: Rules of Origin belum diperiksa | Ada di kode | `TLS.FtaDisclaimer` | Dokumen dan kode cocok. |
| R-272 | [Non-fungsional] Batasan §10: belum tersedia Golden Cases, Knowledge Base, Audit Log, User & Role dari UI, ekspor PDF/DOCX | Tidak ditemukan | Lihat R-148, R-154 | Dokumen dan kode cocok. |
| R-273 | [Auth/Role] Viewer: hak akses baca saja | Tidak jelas | `App.jsx` memberi `null` (semua role) pada Klasifikasi dan Riwayat; `ClassificationController` start memakai policy "Writers" | Perilaku Viewer tidak diuji. |

### B.3 `WORKFLOW_END_TO_END.md` (WF)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-301 | [Admin] Jalur A: upload, parse, map, validasi, dry run, commit, aktivasi | Ada dan teruji | Lihat R-031, R-032, R-027 | |
| R-302 | [Data/BTKI] Parser menyimpan source location: halaman, paragraf, sel tabel (WF L55) | Tidak ditemukan | Lihat R-024 | |
| R-303 | [Klasifikasi] STEP 1 input pengguna | Ada di kode | `ClassificationForm.jsx`, `POST /api/classification/start` | |
| R-304 | [Klasifikasi] STEP 2 fact extraction dari input dan dokumen | Ada di kode | `FES.ExtractFromInput` | **SEBAGIAN.** Hanya dari form (R-074). |
| R-305 | [Klasifikasi] STEP 3 kandidat hanya dari database | Ada dan teruji | `CGS`; `GroqParseTests.Six_and_eight_digit_codes_are_rejected` | |
| R-306 | [Klasifikasi] STEP 4 reasoning chain direkam per langkah dengan source_ids | Ada di kode | `ORC`, `ReasoningEvents`, `ReasoningStep` | Lihat R-007. |
| R-307 | [Klasifikasi] STEP 4 early stopping: satu heading retained langsung ke KUM HS 6 | Ada di kode | `RE.ApplyKumHs` L16 | Lihat R-092. |
| R-308 | [Klasifikasi] STEP 4 KUM HS 3(b) enam faktor berbobot (WF L346–350) | Tidak ditemukan | Lihat R-099 | |
| R-309 | [Klasifikasi] STEP 5 validator V1–V6: format, kode ada, aktif, parent, uraian exact match, tanggal aktif, source_id | Ada di kode | `VAL.ValidateAsync` | **SEBAGIAN.** Format, versi aktif, kode ada dan aktif, parent diperiksa. Uraian exact match, tanggal aktif eksplisit, source_id valid tidak. |
| R-310 | [Klasifikasi] STEP 5 hasil invalid tidak ditampilkan, dikembalikan ke Rules Engine, dilog | Tidak jelas | Lihat R-267 | |
| R-311 | [Klasifikasi] STEP 6 tarif: MFN, FTA, PPN, PPh 22 dari tabel tarif aktif | Ada di kode | `TLS.GetRatesAsync` | |
| R-312 | [Klasifikasi] STEP 6 verifikasi tanggal efektif tarif | Tidak ditemukan | `TLS` hanya memfilter `IsActive` | |
| R-313 | [Klasifikasi] STEP 6 satuan tarif PERCENT, SPECIFIC, COMPOUND | Ada di kode | `RateInfo.Unit`; enum `RateUnit` | Tidak ada tes. |
| R-314 | [Klasifikasi] STEP 7 status DETERMINED, NEED_CLARIFICATION, NEED_EXPERT_REVIEW, VALIDATION_FAILED, ERROR | Ada di kode | `ClassificationModels.cs` `ResultStatus { Determined, NeedClarification, NeedExpertReview }` | **SEBAGIAN.** `VALIDATION_FAILED` dan `ERROR` tidak ada. |
| R-315 | [Klasifikasi] Alur iteratif: jawaban klarifikasi menghitung ulang | Ada dan teruji | `POST /api/classification/{id}/facts`; `KumHs6Tests.Kum_hs_6_descends_level_by_level_with_user_choices` | |
| R-316 | [AI] Peran LLM terbatas pada terjemah istilah dan ekstraksi fakta; kode dan uraian dari database | Ada di kode | Lihat R-005 | **SEBAGIAN / KONFLIK.** Ekstraksi fakta oleh LLM tidak ada (R-072). |

### B.4 `DATABASE_SCHEMA.md` (DB)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-401 | [Non-fungsional] Database MySQL | Tidak jelas | `Program.cs` memakai `UseSqlServer`; migrasi EF Core untuk SQL Server | KONFLIK: kode SQL Server. Lihat E-01. |
| R-402 | [Auth/Role] Tabel `users`: email, password_hash, role (`admin/reviewer/user`), plan, tenant_id | Ada di kode | `AppUsers` (Email, PasswordHash, Role, CreatedAt) | **SEBAGIAN.** `plan` dan `tenant_id` tidak ada. Nama role berbeda (E-03). |
| R-403 | [Klasifikasi] Tabel `classification_cases` menyimpan input per kolom dan output AI | Ada di kode | `ClassificationCases` | Skema kolom berbeda. |
| R-404 | [Non-fungsional] Tabel `daily_usage` dan HTTP 429 setelah 10 request per hari untuk free tier | Tidak ditemukan | Pencarian `daily_usage`, `AddRateLimiter` tidak menemukan kode nyata | Lihat E-06 untuk angka. |
| R-405 | [Review Ahli] Tabel `expert_reviews` | Ada dan teruji | `ExpertCorrections`; `ExpertReviewTests` | Nama berbeda. |
| R-406 | [Non-fungsional] Tabel `audit_logs`: log semua aksi pengguna | Tidak ditemukan | | |
| R-407 | [Data/BTKI] Tabel `btki_reference` | Ada di kode | `BtkiTariffLines` | Nama berbeda. |
| R-408 | [Klasifikasi] Kolom hasil: confidence, reasoning_steps, kandidat_eliminasi, fakta_perlu_konfirmasi, ai_model_used | Ada di kode | `ConfidenceScore`, `ConfidenceJson`, `ReasoningEvents` | **SEBAGIAN.** `ai_model_used` tidak disimpan di case; `AiUsageLogs` terpisah. |

### B.5 `PRESENTATION.md` (PR)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-501 | [Non-fungsional] Free tier 10 klasifikasi per hari, premium tanpa batas (PR §8 L311) | Tidak ditemukan | Lihat R-404 | KONFLIK dalam dokumen: PR L228 menulis 50 req/hari. |
| R-502 | [Riwayat] Riwayat free 7 hari, premium selamanya | Tidak ditemukan | `CaseQueries` tidak membatasi umur riwayat | |
| R-503 | [Laporan/Ekspor] Ekspor hasil PDF/Excel (premium) | Tidak ditemukan | Lihat R-148 | |
| R-504 | [Non-fungsional] Bulk CSV dan akses API (premium) | Tidak ditemukan | | |
| R-505 | [Non-fungsional] T1: API key LLM dipindah ke backend | Ada di kode | `GroqTermSuggester`, `Services/Ai*` di backend; `apiClient.js` | |
| R-506 | [Auth/Role] T2: autentikasi pengguna | Ada di kode | `AuthController`, `JwtService` | |
| R-507 | [Non-fungsional] T3: rate limiting free tier 10 req/hari | Tidak ditemukan | Lihat R-404 | |
| R-508 | [Non-fungsional] T4: database PostgreSQL | Tidak jelas | | KONFLIK dengan MySQL dan SQL Server (E-01). |
| R-509 | [Riwayat] T5: riwayat klasifikasi tersimpan per pengguna | Ada dan teruji | `CaseHistoryTests` | |
| R-510 | [Auth/Role] RBAC dengan role Admin, Reviewer, User | Tidak jelas | | KONFLIK nama role (E-03). |
| R-511 | [Non-fungsional] Tenant isolation: data pengguna A tidak terlihat pengguna B (PR L230) | Tidak ditemukan | Lihat R-193 | Isolasi per pengguna ada untuk Classifier (R-257), per tenant tidak. |
| R-512 | [Non-fungsional] Audit log setiap request: siapa, kapan, input, output (PR L231) | Tidak ditemukan | Lihat R-406 | |
| R-513 | [Non-fungsional] Validasi dan sanitasi input di backend | Ada di kode | Validasi di controller dan `ERS`; format kode di `VAL` | **SEBAGIAN.** Tidak ada audit sistematis. |
| R-514 | [Non-fungsional] T21: notifikasi | Tidak ditemukan | | |
| R-515 | [Klasifikasi] Gold dataset 150 kasus dan tes regresi | Tidak ditemukan | | |
| R-516 | [AI] Model AI `gpt-oss-120b` lewat Groq (PR §4) | Tidak jelas | Penyedia dan model diatur konfigurasi (Groq/Gemini), cek koneksi di `AdminAiController` | KONFLIK: TECHNICAL menyebut llama-3.3. Lihat E-09. |
| R-517 | [Non-fungsional] Deployment (HTTPS, hosting) | Tidak jelas | Di luar repo | |
| R-518 | [Data/BTKI] Data seed: kamus sinonim, dataset (PR §10) | Tidak jelas | `CGS._synonyms`; `data/*.json` dan skrip Python; `BtkiCsvSeeder` | Kecukupan seed tidak diperiksa. |

### B.6 `TASK_LIST.md` (TL, seluruhnya berstatus `TODO` di dokumen)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-601 | [Non-fungsional] 1.1 Clean Architecture ASP.NET Core 8 (EF Core, MySQL, OpenXML, Serilog, AutoMapper) | Ada di kode | Proyek `Domain`, `Application`, `Infrastructure`, `API`, `Tests`; paket `Serilog.AspNetCore` | MySQL berbeda dari kode (E-01). AutoMapper tidak diperiksa. |
| R-602 | [Non-fungsional] 1.2 Skema database untuk 11 tabel yang disebut | Ada di kode | 16 tabel di SQL Server, termasuk `AiUsageLogs` (tambahan) | |
| R-603 | [Admin] 1.3 Version Manager mesin status | Ada dan teruji | Lihat R-031 | |
| R-604 | [Non-fungsional] 1.4*, 1.5* tes property: mesin status dan satu versi aktif | Tidak ditemukan | Semua tes berbasis contoh (xUnit), tidak ada tes property-based | |
| R-605 | [Data/BTKI] 2.1 DOCX parser OpenXML: kode, uraian ID/EN, indent, tarif, source location | Ada di kode | `DocxParser.cs` | **SEBAGIAN.** Lihat R-020 s/d R-024. |
| R-606 | [Data/BTKI] 2.2 Hierarki dan parent, flag `STRUCTURE_AMBIGUITY` | Ada dan teruji | `BtkiPdfTableTests.Lines_hang_under_their_subheading_and_subheadings_under_the_heading`; `AmbiguityQueueTests.Prefix_parents_are_not_ambiguous_conflicting_parent_is` | Jalur PDF. |
| R-607 | [Data/BTKI] 2.3 Structure validator | Ada dan teruji | `BtkiPdfTableTests.Built_nodes_pass_the_existing_structure_dry_run_without_errors_or_ambiguity` | |
| R-608 | [Admin] 2.4 Dry run dan commit; cek total 14.273 | Ada dan teruji | `AmbiguityQueueTests`, `PdfImportCarryTests` | **SEBAGIAN.** Pencocokan total 14.273 tidak ditemukan. |
| R-609 | [Admin] 2.5 API dan UI admin import BTKI | Ada dan teruji | `AIC` btki/upload dan btki/commit; `AmbiguityQueueTests`; UI: CHECKLIST A | |
| R-610 | [Non-fungsional] 2.6* tes property source traceability | Tidak ditemukan | | |
| R-611 | [Data/BTKI] 3.1–3.3 Parser EN, linking paragraf, import dan total 65.833 | Ada di kode | `AIC` en/upload L153 | **SEBAGIAN.** Parse saja. Linking prev/next dan total 65.833 tidak terbukti. |
| R-612 | [Non-fungsional] 3.4* tes property segmentasi EN | Tidak ditemukan | | |
| R-613 | [Data/BTKI] 4.1–4.2 Parser dan import Legal Notes dengan `effect_type` | Ada di kode | `AIC` notes/upload L181; enum `EffectType` | **SEBAGIAN.** Parse saja. |
| R-614 | [Admin] 5B.1 Template CSV dan Excel, endpoint download | Ada di kode | `AdminTemplateController` download csv/xlsx; `AdminDashboard.jsx` `TemplatePanel.download` ~L703 | |
| R-615 | [Admin] 5B.2 Validasi template per baris | Ada di kode | `TemplateIngestionService`; `AdminTemplateController` validate | Tidak ada tes. |
| R-616 | [Admin] 5B.3 Preview diff INSERT/UPDATE/UNCHANGED dan commit | Ada di kode | `AdminTemplateController` preview dan commit | Tidak ada tes. |
| R-617 | [Admin] 5B.4 Kolom `ingestion_method` | Tidak jelas | Tidak diperiksa | |
| R-618 | [Admin] 5B.5 UI admin upload template | Hanya teruji manual/stub | `AdminDashboard.jsx` `TemplatePanel`; CHECKLIST A | |
| R-619 | [Klasifikasi] 6.1 Fact Extractor 35 tipe via LLM | Ada di kode | `FES` | **SEBAGIAN.** Dari form, bukan dari LLM (R-072, R-074). |
| R-620 | [Klasifikasi] 6.2 Dynamic question generator berdasarkan information gain | Tidak jelas | `QuestionGeneratorService.cs` | Lihat R-073. |
| R-621 | [Klasifikasi] 6.3 Deteksi konflik dan konfirmasi pengguna | Ada di kode | `FES.MergeLlmFacts` | **SEBAGIAN.** Lihat R-072. |
| R-622 | [Non-fungsional] 6.4*–6.6* tes property fakta | Tidak ditemukan | | |
| R-623 | [Klasifikasi] 7.1 Multi-strategy: exact, sinonim, multilingual, fungsi, material, rujukan Catatan Hukum, lintas-referensi EN | Ada di kode | `CGS` L64–99; pencarian kata juga mencakup `DescriptionEn` | **SEBAGIAN.** Rujukan Catatan Hukum dan lintas-referensi EN tidak ditemukan. |
| R-624 | [Klasifikasi] 7.2 Pengayaan kandidat | Ada di kode | Lihat R-081 | |
| R-625 | [Non-fungsional] 7.3*, 7.4* tes property kandidat | Tidak ditemukan | Tes contoh terkait: `GroqParseTests` | |
| R-626 | [Klasifikasi] 8.1–8.2 KUM HS 1 dan early stop | Ada dan teruji | Lihat R-091, R-092 | |
| R-627 | [Non-fungsional] 8.3*, 8.4* tes property KUM HS 1 | Tidak ditemukan | | |
| R-628 | [Klasifikasi] 9.1 KUM HS 2(a) dengan keluaran TREAT_AS_COMPLETE, CLASSIFY_AS_PRESENTED, NEED_CLARIFICATION | Ada dan teruji | `ConditionGateTests.Ckd_with_essential_character_and_complete_kit_is_treated_as_complete`, `Ckd_failing_2a_goes_to_expert` | Nama keluaran tidak sama persis. |
| R-629 | [Klasifikasi] 9.2 KUM HS 2(b) | Ada di kode | Lihat R-095 | **SEBAGIAN.** |
| R-630 | [Klasifikasi] 10.1 KUM HS 3(a) | Ada di kode | Lihat R-096 | |
| R-631 | [Klasifikasi] 10.2 KUM HS 3(b) enam faktor berbobot | Tidak ditemukan | Lihat R-099 | |
| R-632 | [Klasifikasi] 10.3 KUM HS 3(c) | Ada di kode | Lihat R-100 | |
| R-633 | [Non-fungsional] 10.4* tes property KUM HS 3(a) | Tidak ditemukan | | |
| R-634 | [Klasifikasi] 11.1 KUM HS 4 | Ada di kode | Lihat R-101 | **SEBAGIAN.** |
| R-635 | [Klasifikasi] 11.2 KUM HS 5 | Ada dan teruji | Lihat R-102 | |
| R-636 | [Klasifikasi] 12.1 KUM HS 6 turun ke 6 lalu 8 digit, tidak default ke `.00` | Ada dan teruji | `KumHs6Tests.Kum_hs_6_descends_level_by_level_with_user_choices`, `Heading_without_tariff_lines_never_fabricates_a_code` | |
| R-637 | [Non-fungsional] 12.2* tes property KUM HS 6 same-level | Tidak ditemukan | Tes contoh terkait: `KumHs6Tests` | |
| R-639 | [Klasifikasi] 14.1 Validator komprehensif: uraian exact match, tanggal aktif, source_id valid | Ada di kode | `VAL` | **SEBAGIAN.** Lihat R-309. |
| R-640 | [Klasifikasi] 14.2 Validasi gagal: tidak ditampilkan, kembali ke Rules Engine, dilog | Tidak jelas | Lihat R-267 | |
| R-641 | [Non-fungsional] 14.3*, 14.4* tes property validator | Tidak ditemukan | | |
| R-642 | [Klasifikasi] 15.1 MFN: PERCENT/SPECIFIC/COMPOUND, tanggal efektif, source_id | Ada di kode | `TLS` | **SEBAGIAN.** Tanggal efektif tidak diverifikasi (R-312). |
| R-643 | [Klasifikasi] 15.2 FTA per skema dan negara, label "Tarif FTA Tersedia", disclaimer | Ada di kode | `TLS.FtaRates`, `FtaDisclaimer`; `ResultPanel.jsx` | Label persis belum dicocokkan. |
| R-644 | [Klasifikasi] 15.3 PPN dan PPh 22 dengan disclaimer PMK | Ada di kode | `TLS` Ppn, Pph22, `TaxDisclaimer` | |
| R-645 | [Non-fungsional] 15.4* tes property keterlacakan tarif | Tidak ditemukan | | |
| R-646 | [Klasifikasi] 16.1 Orchestrator: fakta, kandidat, rules, validator, tarif, simpan, iterasi | Ada di kode | `ClassificationOrchestrator.cs` (`ClassifyAsync`, `PersistAsync`) | Hanya `DecideNoCandidate` yang diuji langsung (`GroqParseTests`). Alur lengkap tidak diuji secara terintegrasi. |
| R-647 | [Laporan/Ekspor] 16.2 Endpoint `/start`, `/:caseId/facts`, `/status`, `/reasoning`, `/tariffs`, `/export` | Ada di kode | `ClassificationController`: cases, `{id}`, start, `{id}/facts`, request-review, status, reasoning, tariffs | **SEBAGIAN.** `/export` tidak ada. |
| R-648 | [Auth/Role] 16.3 JWT dan RBAC (Admin/Classifier/Viewer), log semua request dengan user_id | Ada di kode | `AuthController`, `JwtService`, `[Authorize]`; paket Serilog | **SEBAGIAN.** Log setiap request dengan user_id tidak diverifikasi. |
| R-649 | [Klasifikasi] 17.1 Frontend terhubung ke backend V2 | Hanya teruji manual/stub | `apiClient.js`; CHECKLIST | Backend asli tidak dipakai di uji UI. |
| R-650 | [Admin] 17.2 Admin dashboard: `/admin/versions`, `/admin/upload`, `/admin/validation` | Hanya teruji manual/stub | `AdminDashboard.jsx` | **SEBAGIAN.** Satu halaman dengan tab, bukan tiga route. |
| R-651 | [Non-fungsional] 17.3 Error handling, loading state, retry, disclaimer tarif | Hanya teruji manual/stub | `apiClient.apiFetch` (retry), `Modal.jsx`, notice `role="alert"`; CHECKLIST | |
| R-652 | [Non-fungsional] 18.1 Skrip seeding: admin pertama, versi BTKI 2022, dokumentasi upload pertama | Tidak jelas | `BtkiCsvSeeder` ada. Seed admin pertama tidak diperiksa. | |
| R-653 | [Non-fungsional] 18.2 Konfigurasi produksi: CORS, HTTPS, Serilog, health check, docker-compose | Ada di kode | `Program.cs` (CORS), paket `Serilog.AspNetCore` dan `Serilog.Sinks.File` di `HsClassifier.API.csproj` | **SEBAGIAN.** `docker-compose` dan `Dockerfile` tidak ditemukan. Endpoint health check dan HTTPS produksi tidak diperiksa. |
| R-654 | [Non-fungsional] 18.3 Dokumentasi API (Swagger) dan panduan | Ada di kode | `Program.cs` L25 `AddSwaggerGen`, L98 `UseSwagger` (hanya Development) | Panduan admin dan klasifikasi V2 tidak ditemukan. |
| R-655 | [Non-fungsional] 18.4* tes integrasi end-to-end | Tidak ditemukan | Semua tes memakai EF InMemory | |
| R-656 | [Non-fungsional] 19 Checkpoint final: 16 correctness properties, audit trail lengkap | Tidak ditemukan | Lihat R-604, R-180 | |
| R-657 | [Non-fungsional] Kolom Status di TASK_LIST | Tidak jelas | Semua `TODO`, padahal sebagian besar sudah dikerjakan | KONFLIK dokumen dan kode. Lihat E-10. |

### B.7 `PROJECT_SUMMARY.md` (PS)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-801 | [Non-fungsional] Fitur V2 inti: Admin Data Center, Fact Engine, Rules Engine, Validator, Tarif, Expert Review (PS §Fitur-Fitur V2) | Ada dan teruji | Lihat B.1, B.3 | **SEBAGIAN.** Bagian lain PS adalah rencana waktu, bukan persyaratan yang bisa diuji. |

### B.8 `TECHNICAL.md` (TE) dan `USER_GUIDE.md` (US), keduanya dokumen V1

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-810 | [Non-fungsional] TE §1, §6: aplikasi frontend saja, Groq dipanggil dari browser dengan `VITE_GROQ_API_KEY` | Tidak ditemukan | `apiClient.js` hanya memanggil backend | Digantikan arsitektur V2. KONFLIK arsitektur, bukan kekurangan. |
| R-811 | [Riwayat] TE §11: "stateless, tidak ada history" sebagai batasan | Ada dan teruji | `CaseHistoryTests` | Batasan V1 sudah diatasi. |
| R-812 | [AI] TE: model llama-3.3 | Tidak jelas | | KONFLIK dengan PR (E-09). |
| R-813 | [Klasifikasi] US §3: form 14 field dalam 5 seksi | Hanya teruji manual/stub | `ClassificationForm.jsx`; CHECKLIST K1–K7 | |
| R-814 | [Klasifikasi] US §4: hasil analisis berisi kode, confidence, reasoning, tarif | Hanya teruji manual/stub | `ResultPanel.jsx`; CHECKLIST R | |
| R-815 | [Klasifikasi] US §7: confidence Tinggi, Sedang, Rendah | Ada dan teruji | `ConfidenceTests.Level_bands` (60 dan 80 sebagai batas) | Batas angka tidak disebut di dokumen yang dibaca. |
| R-816 | [Review Ahli] US §8: kapan meminta expert review | Ada dan teruji | Lihat R-258 | |
| R-817 | [Non-fungsional] US §10 dan TE §11: batasan V1 (tanpa auth, tanpa audit, validasi hanya klien) | Ada di kode | Auth ada (R-506), validasi backend ada (R-513) | Batasan V1 sebagian sudah diatasi, audit belum (R-180). |

### B.9 `UI_SUMMARY_FOR_FRONTEND_GENERATION.md` (UI, turunan, ditulis agent)

| ID | Persyaratan | Status | Bukti | Catatan |
|---|---|---|---|---|
| R-901 | [Review Ahli] Alasan keputusan ahli minimal 10 karakter (UI L673) | Tidak jelas | `ERS` L25 `MinReasonLength = 5`; `ReviewPage.jsx` ambang 5 karakter | KONFLIK. Angka 10 tidak ada di md atasan mana pun (pencarian `10 karakter` hanya cocok di berkas ini). Lihat E-02. |
| R-902 | [Review Ahli] Kode manual harus dicek lewat "Cek di BTKI" sebelum Setujui; tombol Setujui nonaktif bila kode tidak ditemukan (UI L675–676) | Hanya teruji manual/stub | `ReviewPage.jsx`; CHECKLIST E1–E6; sisi server: `ExpertReviewTests.Invalid_approvals_are_refused_and_nothing_changes` | |
| R-903 | [Riwayat] Detail riwayat dengan tab Hasil, Fakta, Reasoning, Tarif, Keputusan Ahli | Tidak ditemukan | Bagian detail bertumpuk tanpa tab (`DESIGN.md` Q7) | Sengaja ditunda (D). |
| R-904 | [Klasifikasi] Tab Tarif menampilkan tanggal berlaku | Tidak ditemukan | Lihat R-144 | |
| R-905 | [Admin] Tab Antrian Ambiguitas | Ada dan teruji | Lihat R-027 | |

### B.10 Fitur yang ada di kode tetapi tidak diminta oleh md atasan

| Fitur | Bukti | Catatan |
|---|---|---|
| Tab Status AI (koneksi, pemakaian, riwayat request) | `AdminAiController`, `AiStatusPanel.jsx`, tabel `AiUsageLogs`, tes AI (43 tes) | Diminta user di sesi kerja ini, bukan dari md. |
| Parser PDF BTKI per bab | `BtkiPdfParser.cs`, `BtkiPdfTableTests`, `BtkiPdfFileTests` | Menggantikan jalur DOCX dalam praktik. Tidak disebut di md. |
| Seeder CSV BTKI | `BtkiCsvSeeder` | FLOW §10 menyebut dataset CSV. |
| Modal klarifikasi, restyle v0, sidebar | `Modal.jsx`, `design-system.css` | Hasil pekerjaan restyle REV1/REV2. |

---

## C. Belum dikerjakan dan sebagian, diurutkan prioritas

Ukuran: S = beberapa hari, M = 1–2 minggu, L = lebih dari 2 minggu. Ukuran adalah perkiraan kasar dari struktur kode saat ini, bukan estimasi komitmen. "Tipe ketergantungan" menunjuk apa yang harus ada dulu: **backend**, **data**, atau **keputusan atasan**.

### C.1 Prioritas tinggi

| # | Pekerjaan | ID | Ukuran | Ketergantungan |
|---|---|---|---|---|
| 1 | Upload dokumen pengguna dan ekstraksi fakta (invoice, datasheet, foto). Hubungkan `MergeLlmFacts`, tambah penyimpanan berkas dan checksum, validasi MIME/magic bytes. | R-074, R-132, R-181, R-190, R-191, R-072, R-304, R-619 | L | Backend (penyimpanan, ekstraksi LLM atau OCR, endpoint). Keputusan atasan: jenis dokumen yang wajib, lokasi penyimpanan, kebijakan keamanan. |
| 2 | Ekspor hasil: JSON dulu (S), lalu DOCX dan PDF (M). Endpoint `POST /{caseId}/export` dan tombol di panel hasil. | R-148, R-647, R-503, R-142 | M | Backend (endpoint, pustaka DOCX/PDF). Keputusan atasan: format dan isi laporan (KADL), apakah ekspor khusus premium. |
| 3 | Audit trail lengkap: hash chain event, versi engine dan prompt per case, log tindakan admin, tabel `audit_logs`, tampilan Audit Log. | R-040, R-063, R-180 s/d R-183, R-406, R-512 | L | Backend dan skema database. Keputusan atasan: cakupan log dan masa simpan. |
| 4 | Keamanan multi-klien: tenant isolation, rate limit server, kuota harian, scan malware. | R-193, R-198, R-404, R-501, R-507, R-511, R-192 | M sampai L | Keputusan atasan dulu: model tenant, angka kuota (10 atau 50), paket free/premium. |
| 5 | Parser DOCX penuh (style, numbering, merged cell, tracked changes, hidden text, footnote, source locator) dan commit EN serta Catatan Bagian/Bab. | R-020 s/d R-025, R-028, R-029, R-611, R-613, R-302 | L | Data (contoh DOCX asli, rekonsiliasi total 14.273 dan 65.833). Keputusan atasan: apakah jalur PDF/CSV sudah cukup dan DOCX tidak perlu. |
| 6 | KUM HS 3(b) enam faktor berbobot, 2(b) pencabangan, 4 barang pembanding, 3(a) proviso, barang komposit, exclusion Catatan Hukum. | R-095, R-097, R-098, R-099, R-101, R-110, R-011, R-308, R-629, R-631, R-634 | M sampai L | Data (EN dan Catatan sudah di database). Keputusan atasan atau ahli: bobot faktor dan definisi risiko tinggi. |
| 7 | Golden case, enam regresi master prompt, tes adversarial, tes property-based, tes integrasi end-to-end. | R-200 s/d R-203, R-212 s/d R-215, R-515, R-604, R-610, R-612, R-622, R-625, R-627, R-633, R-637, R-641, R-645, R-655, R-656 | M sampai L | Data (kumpulan kasus emas dari atasan, 150 kasus di PR). Backend (harness). |
| 8 | Model status 16 tingkat, `Simpan Draft`, `Buat Versi Baru`, `SUPERSEDED`, `VALIDATION_FAILED`, `ERROR`. | R-150, R-153, R-160, R-161, R-183, R-314 | M | Backend dan skema. Keputusan atasan: apakah semua 16 status diperlukan (E-08). |
| 9 | Validator penuh V1–V6 dan penanganan hasil invalid (uraian exact match, tanggal aktif, source_id valid; kembali ke Rules Engine atau ke review ahli). | R-309, R-310, R-267, R-639, R-640 | S sampai M | Backend. |

### C.2 Prioritas sedang

| # | Pekerjaan | ID | Ukuran | Ketergantungan |
|---|---|---|---|---|
| 10 | Kartu kandidat, tab legal reasoning, decision tree di UI. Data reasoning sudah ada di `GET /{id}/reasoning`. | R-139, R-140, R-141, R-903 | M | Frontend; sebagian backend (EN per kandidat bergantung R-028). Keputusan atasan: tab mana yang wajib. |
| 11 | Wizard 11 langkah: Step 1 referensi (tanggal, versi), panel kondisi khusus, field tambahan (manufacturer, SKU, HS supplier), tombol tambah material. | R-130, R-131, R-133 s/d R-138 | L | Frontend dan sebagian backend (pilih versi BTKI per tanggal). Keputusan atasan: apakah form satu halaman saat ini cukup. |
| 12 | Tarif: tanggal berlaku, `RATE_REFERENCE_ONLY`, pilih skema FTA, bandingkan MFN dan FTA, verifikasi tanggal efektif, sumber tarif. | R-144, R-145, R-151, R-312, R-642, R-904 | S sampai M | Backend (tanggal efektif sudah ada di data atau belum, perlu dicek) dan frontend. |
| 13 | Strategi kandidat tambahan: lintas-referensi EN, rujukan Catatan Hukum, semantik. | R-080, R-623 | L | Data (EN ter-commit, R-028). |
| 14 | Urutan pertanyaan berdasarkan information gain. | R-073, R-620 | M | Backend. |
| 15 | Classification risk sebagai tingkat, bukan hanya flag. | R-173 | S | Keputusan atasan: definisi tingkat. |
| 16 | Role lengkap (6 role) dan halaman User & Role. | R-195, R-154, R-402, R-510 | M | Keputusan atasan (E-03). |
| 17 | Backup dataset dan log tindakan admin. | R-039, R-040 | M | Backend, infrastruktur. |
| 18 | Penegakan immutable audit di level database. | R-180 | S sampai M | Backend. |
| 19 | Kuota, paket free/premium, riwayat 7 hari, `plan` pada pengguna. | R-402, R-501, R-502 | M | Keputusan atasan. |

### C.3 Prioritas rendah

| # | Pekerjaan | ID | Ukuran | Ketergantungan |
|---|---|---|---|---|
| 20 | Golden Cases, Knowledge Base, Settings (halaman). | R-154 | M per halaman | Keputusan atasan; Golden Cases bergantung #7. |
| 21 | Notifikasi, bulk CSV, akses API. | R-504, R-514 | L | Keputusan bisnis. |
| 22 | `docker-compose`, Dockerfile, skrip seed admin pertama, panduan admin V2. | R-652, R-653, R-654 | S sampai M | Infrastruktur. |
| 23 | Sinkronkan alasan minimal 5 atau 10 karakter, dan notice sukses keputusan ahli (TEMUAN F1 dan F2). | R-901, R-260 | S | Keputusan atasan (E-02). |
| 24 | Perbarui dokumen yang usang: FLOW §10, TASK_LIST (status), TECHNICAL/USER_GUIDE (V1). | R-268, R-269, R-657, R-810 | S | Keputusan siapa pemilik dokumen. |

---

## D. Sengaja ditunda (dari `RINGKASAN_RESTYLE.md` dan `TEMUAN.md`)

Sumber penundaan: `RINGKASAN_RESTYLE.md` L106–L114 (bagian 4.3, "Tombol tanpa fungsi di acuan … tidak dibuat"), `DESIGN.md` Q5, Q7, Q8, dan `TEMUAN.md` F1 dan F2. Kolom "Dicakup oleh" menyebut persyaratan md yang menyentuh item itu. Kolom "Diperlukan" menyebut apa yang kurang.

| Item yang ditunda | Alasan penundaan (dokumen) | Dicakup oleh persyaratan md | Yang diperlukan |
|---|---|---|---|
| Export PDF | Tombol di acuan v0 tanpa fungsi (RINGKASAN 4.3; DESIGN Q8) | R-148 (MP STEP 11 "Ekspor PDF"), R-272 (FLOW §10), R-503 (PR §8 premium), R-647 (TL 16.2 endpoint export) | Endpoint export di backend, pustaka PDF, keputusan isi laporan. Tombol UI sesudahnya. Ukuran M. |
| Export Excel | Sama | R-503 (PR §8 premium). MP tidak menyebut Excel. | Endpoint dan pustaka XLSX, keputusan apakah khusus premium. Ukuran M. |
| Lihat Audit Trail | Tombol tanpa fungsi di acuan | MP STEP 10 tab "Audit Trail", MP navigasi global "Audit Log", MP §AUDIT TRAIL, R-272 (FLOW §10), R-180 s/d R-183, R-406 | Dua tingkat. Audit trail per case: data sudah ada di `GET /api/classification/{id}/reasoning`, tinggal tab di UI (S). Audit Log global: butuh tabel `audit_logs` dan endpoint (L). |
| Browse BTKI | Tombol tanpa fungsi di acuan | Tidak ada persyaratan md atasan yang menyebut "Browse BTKI" persis. Terdekat: MP STEP 9 "Buka Uraian BTKI" dan navigasi "Knowledge Base" (R-154, R-139). | Endpoint `api/btki/search`, `api/btki/{code}`, `api/btki/stats` sudah ada dan dipakai di Review Ahli (`searchBtki`, `lookupCode`). Perlu halaman penelusuran dan keputusan bahwa ini dibutuhkan. Ukuran M. |
| Blok statistik | API tidak menyediakan agregat (DESIGN Q5) | Tidak ada persyaratan md atasan. Hanya `UI_SUMMARY` (turunan) yang menggambarkan kartu statistik. | Endpoint agregat case (jumlah per status, per hari). Hanya dibuat bila atasan memintanya. Ukuran S sampai M. |
| Badge urgent dan usia case | Tidak ada data urgensi (RINGKASAN 4.3 "tanpa badge URGENT") | Tidak ada persyaratan md atasan tentang urgensi atau SLA. | Definisi "urgent" dari atasan. Usia case bisa dihitung dari `CreatedAt`. Ukuran S setelah definisi ada. |
| Tab di detail riwayat | Konten bertumpuk dipilih agar tidak tersembunyi (DESIGN Q7) | MP STEP 10 (sembilan tab legal reasoning), R-140, R-903 (UI_SUMMARY) | Keputusan UX. Datanya sudah ada. Ukuran S sampai M. |
| Notifikasi | Tidak ada data atau endpoint (DESIGN Q8) | R-514 (PR T21) | Backend notifikasi (email atau in-app), keputusan kanal dan pemicu. Ukuran L. |
| Notice sukses setelah keputusan ahli (TEMUAN F1) | Perubahan perilaku, bukan restyle | R-259, R-264 (FLOW §6.2 langkah 5 menyebut sistem mencatat, tidak menyebut notice) | Perbaikan state di `ReviewPage.jsx`. Ukuran S. |
| Alasan minimal 5 vs 10 karakter (TEMUAN F2) | Perubahan validasi | R-260 (FLOW §6.3 "alasan wajib", tanpa angka), R-901 (hanya UI_SUMMARY) | Keputusan angka (E-02). Ukuran S. |

---

## E. Konflik dan pertanyaan

### E.1 Konflik antar md, dan antara md dan implementasi (dicatat, tidak dipilih)

| ID | Konflik | Pihak yang bertentangan | Bukti |
|---|---|---|---|
| E-01 | **Database.** MySQL, PostgreSQL, atau SQL Server. | `DATABASE_SCHEMA.md` (MySQL), `TASK_LIST.md` 1.1 dan 1.2 (MySQL), `WORKFLOW_END_TO_END.md` (MySQL), `PRESENTATION.md` T4 (PostgreSQL) walau §7 menyebut MySQL, `TECHNICAL.md` (PostgreSQL). Kode: SQL Server. | `Program.cs` `UseSqlServer`; migrasi EF Core; skrip `docs/db/20261101141839_AddAiUsageLogs.sql`. |
| E-02 | **Panjang minimal alasan keputusan ahli.** Kode 5 karakter. Angka 10 hanya di `UI_SUMMARY_FOR_FRONTEND_GENERATION.md` (ditulis agent) dan, menurut `TEMUAN.md` F2, pada placeholder acuan v0. Tidak ada md atasan yang menyebut angka. | `UI_SUMMARY` L673 dan `TEMUAN.md` F2 lawan kode | `ERS` L25 `MinReasonLength = 5`; `ReviewPage.jsx`. FLOW §6.3 hanya menulis "wajib diisi". |
| E-03 | **Daftar role.** | Kode: `Admin, Classifier, Viewer, ExpertReviewer`. MP: `USER, CLASSIFIER, EXPERT_REVIEWER, DATA_ADMIN, SYSTEM_ADMIN, AUDITOR`. FLOW §3: Classifier, Expert, Admin, Viewer. TL 16.3: `Admin/Classifier/Viewer`. DB: `admin/reviewer/user`. PR: `Admin, Reviewer, User`. | `Enums.cs` L17 |
| E-04 | **LLM dan kode HS.** MP dan TL 7.1 melarang LLM menghasilkan kode. Kode meminta LLM mengusulkan istilah dan pos 4 digit, lalu menyaring terhadap database. | MP L690, TL 7.1 lawan kode | `GroqTermSuggester.Parse` (menerima `headings`), `CGS.FromSuggestionAsync`; `GroqParseTests.Six_and_eight_digit_codes_are_rejected` |
| E-05 | **Contoh di-hard-code.** MP melarang. Kode punya peta sinonim `_synonyms` (misal `laptop`). | MP Prinsip 15 dan Acceptance 13 lawan kode | `CGS` L13–L50 |
| E-06 | **Kuota free tier.** 10 request per hari lawan 50 request per hari, dalam dokumen yang sama. | `PRESENTATION.md` L311 dan `DATABASE_SCHEMA.md` (10) lawan `PRESENTATION.md` L228 (50) | |
| E-07 | **FLOW_BISNIS §10 usang.** Pembeda angka kini dibaca otomatis. Pembacaan BTKI dari PDF ada tetapi tidak disebut. | `FLOW_BISNIS_V2.md` §10 lawan kode | `ThresholdTests` (14 tes); `BtkiPdfParser.cs`; `BtkiPdfTableTests` (19 tes) |
| E-08 | **Model status.** MP 16 status. WORKFLOW lima kode (`DETERMINED`, `NEED_CLARIFICATION`, `NEED_EXPERT_REVIEW`, `VALIDATION_FAILED`, `ERROR`). Kode: tiga di `ResultStatus`, empat di `CaseStatus`, plus string status ahli. | MP L1422, WF L621 lawan kode | `ClassificationModels.cs` L6; `Enums.cs` L16; `ERS` L20–25 |
| E-09 | **Model AI.** `gpt-oss-120b` (PR, DATABASE_SCHEMA `ai_model_used`), llama-3.3 (TECHNICAL). Kode: penyedia dan model dari konfigurasi (Groq atau Gemini). | `PRESENTATION.md` §4, `TECHNICAL.md` §6 lawan kode | `AdminAiController`, `AiConnectionChecker` |
| E-10 | **Status task.** `TASK_LIST.md` menandai semua task `TODO`, padahal sebagian besar sudah ada di kode. | `TASK_LIST.md` lawan kode | Lihat B.6 |
| E-11 | **KUM HS 4.** MP: tampilkan barang pembanding, review ahli untuk risiko tinggi. FLOW §6.1: KUM HS 4 selalu masuk antrian ahli. Kode: selalu `NeedExpertReview` dengan kandidat pertama. | MP L984 lawan FLOW §6.1 lawan kode | `RE` KUM HS 4 |
| E-12 | **Contoh PPh 22.** 10% di layout hasil WORKFLOW L539, 7.5% di `UI_SUMMARY`. Keduanya contoh tampilan. Nilai nyata berasal dari tabel tarif. | `WORKFLOW_END_TO_END.md` lawan `UI_SUMMARY` | Tidak berdampak pada kode. |
| E-13 | **Sumber BTKI.** MP, WF, TL mensyaratkan jalur DOCX. Praktik di kode: PDF per bab dan CSV. | MP §A1, TL 2.1 lawan kode | `AIC` L24–L40 menerima `.docx` atau `.pdf` |

### E.2 Pertanyaan untuk atasan

1. Database resmi: SQL Server (seperti kode), MySQL, atau PostgreSQL? Jika bukan SQL Server, siapa yang memigrasikan?
2. Panjang minimal alasan keputusan ahli: 5, 10, atau lain? Siapa pemilik angka ini?
3. Role final: empat role sekarang, atau enam role MP?
4. Bolehkah LLM mengusulkan pos 4 digit yang disaring database, atau harus istilah saja?
5. Apakah peta sinonim hard-coded diperbolehkan sebagai data bantu?
6. Apakah paket free/premium dan kuota harian masuk lingkup V2? Jika ya, angkanya 10 atau 50?
7. Apakah jalur DOCX untuk BTKI masih diwajibkan, atau PDF dan CSV sudah menggantikan?
8. Apakah folder `.kiro/specs/hs-classification-v2/` juga sumber persyaratan?
9. Bobot enam faktor KUM HS 3(b) (fungsi 30%, material 15%, bulk 15%, nilai 15%, kebutuhan 15%, persepsi 10%) final?
10. Ekspor: format yang wajib (JSON, DOCX, PDF, Excel) dan apakah hanya untuk premium?
11. Apakah daftar tombol di master prompt STEP 1–11 wajib dibuat semua, atau form satu halaman saat ini diterima?

---

## F. Batas audit

- **Runtime.** Tidak ada aplikasi yang dijalankan, tidak ada migrasi, tidak ada endpoint tulis. Perilaku nyata terhadap database SQL Server dan penyedia AI tidak diverifikasi.
- **Tes tidak dijalankan ulang.** Percobaan `dotnet test` di sesi audit ini gagal membangun karena file DLL terkunci (Visual Studio dan IIS Express Worker Process memegang `HsClassifier.API\bin`). Jumlah 236 tes lulus berasal dari run sebelumnya di sesi kerja ini pada HEAD yang sama. `npm run lint` dan `npm run build` tidak dijalankan ulang dalam audit ini. Hitungan tes per berkas di bagian B dihitung dari atribut `[Fact]` dan `[Theory]` di kode.
- **Bukti UI hanya manual/stub.** Seluruh status "Hanya teruji manual/stub" bersandar pada `CHECKLIST_FUNGSI.md` (DOM dan fetch stub). Backend asli tidak dipakai. Penilaian visual tidak dilakukan.
- **Tidak ada tes frontend otomatis** (`RINGKASAN_RESTYLE.md`: tidak ada script `test`).
- **Pembacaan kode berbasis pencarian.** Status "Tidak ditemukan" berarti pencarian teks dan penelusuran struktur tidak menemukan, bukan bukti mutlak tidak ada. Pencarian yang dipakai: `event_hash`, `tenant`, `daily_usage`, `AddRateLimiter`, `GridSpan`, `VerticalMerge`, `Footnote`, `Vanish`, `Inserted`, `Deleted`, `Numbering`, `MergeLlmFacts`, `VALIDATION_FAILED`, `docker-compose`, `type="file"`.
- **Tidak dibaca rinci:** isi `QuestionGeneratorService.cs`, `TemplateIngestionService.cs`, `BtkiImportService.cs`, `DocxParser.cs` (hanya dicari kata kunci), dan kolom DTO respons klasifikasi terhadap skema JSON MP L1449–1552 (R-162, R-073, R-030, R-267).
- **Kelengkapan pembacaan md.** Bagian TASK_LIST nomor 1–62 dibaca. Daftar tombol Admin Data Center MP §B tidak dicocokkan satu per satu. Bagian Milestone, Deliverable, dan Instruksi Mulai di MP tidak diubah menjadi persyaratan karena merupakan instruksi kerja untuk AI pembuat kode.
- **Bukan sumber otoritatif.** `UI_SUMMARY_FOR_FRONTEND_GENERATION.md` ditulis agent. Persyaratan yang hanya muncul di sana ditandai (R-901 s/d R-905).
- **Di luar lingkup.** `.kiro/specs/hs-classification-v2/`, berkas `.docx`/`.pdf`/`.pptx` di `docs/` (tidak dibaca, hanya `.md`), perubahan lokal yang belum di-commit, dan branch `chore/untrack-build-files` (2 commit, belum digabung atau di-push).
- **Nomor persyaratan.** ID dikelompokkan per dokumen dengan celah (misalnya R-040 ke R-050). Celah disengaja agar ID tetap stabil bila baris ditambahkan.

---

## G. Pembaruan setelah implementasi (bukan bagian dari snapshot audit)

Bagian A sampai F memotret `feature/ai-status` @ `505e1b5` dan tidak diubah. Pekerjaan sesudahnya ada di empat cabang yang ditumpuk berurutan, belum digabung dan belum di-push:

| Urutan | Cabang | Commit | Isi |
|---|---|---|---|
| 1 | `feature/quota-security` | `dfa2136` | Kuota harian, rate limit, pemeriksaan isi berkas upload |
| 2 | `feature/export` | `b3717fc` | Ekspor JSON, DOCX, PDF |
| 3 | `feature/kum-hs-rules` | `f27aae3` | KUM HS 3(b) berbobot, 2(b), bukti KUM HS 4 |
| 4 | `feature/regression-tests` | `d5e8f1e` | Kasus regresi, tes adversarial, tes validator |

Hasil `dotnet test` di cabang terakhir: 299 lulus, 2 dilewati dengan alasan tertulis (celah yang ditemukan, lihat G.3). Sebelumnya 236.

### G.1 Perubahan status (berlaku bila keempat cabang digabung)

| ID | Persyaratan | Status audit | Status sekarang | Bukti |
|---|---|---|---|---|
| R-148, R-647, R-503 (JSON, DOCX, PDF) | Ekspor hasil | Tidak ditemukan | Ada dan teruji | `ExportService`, `ClassificationController.Export`, `ExportServiceTests` (6), `ResultPanel.jsx` `ExportBox`, CHECKLIST X1–X6. Excel tidak dibuat. |
| R-404, R-507, R-501 | Kuota harian per pengguna | Tidak ditemukan | Ada dan teruji | `DailyQuotaService`, tabel `DailyUsages`, migrasi `AddDailyUsage`, `DailyQuotaTests` (8 kasus). Paket free/premium tetap tidak ada. |
| R-198 | Rate limit server | Ada di kode (sebagian) | Ada di kode | `AddRateLimiter` di `Program.cs`, kebijakan `classify` dan `auth`. Tidak diuji otomatis (tidak ada tes integrasi HTTP). |
| R-191 | Validasi isi berkas upload | Ada di kode (sebagian) | Ada dan teruji | `UploadGuard`, `UploadGuardTests` (9). Scan malware tetap tidak ada. |
| R-099, R-308, R-631 | KUM HS 3(b) enam faktor berbobot | Tidak ditemukan | Ada dan teruji | `EssentialCharacter`, `RulesEngine.ApplyKumHs3`, `EssentialCharacterTests` (14). Bobot di `Rules:EssentialCharacter`. |
| R-095, R-629 | KUM HS 2(b) | Ada di kode (sebagian) | Ada dan teruji (sebagian) | Mencatat pos yang menyebut bahan barang. Tidak mengeliminasi, sesuai aturan. |
| R-101, R-634 | KUM HS 4 | Ada di kode (sebagian) | Ada dan teruji (sebagian) | Lihat G.2. |
| R-004 | Kode tidak ada ditolak | Ada di kode | Ada dan teruji | `ClassificationValidatorTests` (11 kasus). |
| R-200, R-201, R-202, R-203 | Empat kasus regresi master prompt | Tidak ditemukan | Ada dan teruji (sebagian) | `RegressionCasesTests`, fixture mini-BTKI. Hasil yang diharapkan untuk dumper dan crane belum dikonfirmasi ahli. |
| R-212, R-213 | Instruksi di dalam input dan keluaran AI | Tidak ditemukan | Ada dan teruji | `AdversarialInputTests`. Upload dokumen pengguna belum ada, jadi injeksi lewat invoice belum relevan. |
| R-217 | Satuan tidak dikenal | Tidak jelas | Ada dan teruji | `AdversarialInputTests.A_unit_the_engine_does_not_know_...` |

### G.2 Koreksi atas audit

- **R-101 dan KUM HS 4.** Audit menulis KUM HS 4 "selalu mengambil kandidat pertama lalu minta review ahli". Itu keliru. `ApplyKumHs3` selalu memilih satu pos (3(c) selalu menentukan), sehingga cabang KUM HS 4 setelahnya tidak pernah terjangkau. Cabang itu sudah dihapus. Kasus yang seharusnya masuk KUM HS 4 (tidak ada pos yang cocok menurut KUM HS 1) tetap berjalan seperti sebelumnya. Bedanya, mesin kini mencatat sampai tiga pos paling mirip beserta kesamaannya sebagai langkah `KUM_HS_4_COMPARE`, tanpa menandai "GRI 4 dipakai". Sesuai keputusan Anda, perilaku kode final tidak diubah: tidak ada kode final baru yang dihilangkan.
- **R-309 dan Swagger.** Deskripsi Swagger di `Program.cs` menyebut kode gagal `DESCRIPTION_MISMATCH`, `INACTIVE_CODE`, `SOURCE_ID_INVALID`. Validator tidak menghasilkannya. Kode tidak aktif dilaporkan sebagai `CODE_NOT_FOUND`. Teks Swagger belum diperbaiki.

### G.3 Celah baru yang ditemukan oleh tes regresi (tes ditandai Skip)

1. **Dumper bernama dagang "Tractor", dijelaskan dengan kata sehari-hari.** KUM HS 1 hanya membandingkan fakta dengan teks resmi pos (4 digit). Teks pos 8704 ("Kendaraan bermotor untuk pengangkutan barang") tidak memuat kata "dumper"; kata itu hanya ada di baris tarifnya. Fakta "mengangkut material dengan bak pembuang" juga tidak berbagi kata dengan "pengangkutan barang" (awalan berbeda: *meng-* dan *peng-…-an*; tidak ada stemming). Akibatnya pos 8704 dieliminasi, pos 8701 (traktor) dipertahankan karena nama dagang, dan pengguna diminta memilih subpos traktor. Bila fakta memakai kata yang sama dengan teks pos ("pengangkutan barang"), hasilnya benar (8704.10.00). Catatan: teks fixture adalah pendekatan teks BTKI, bukan salinan; periksa dengan data asli.
2. **"Pompa" saja.** Pos 8413 (cairan) dan 8414 (udara/vakum) sama-sama dipertahankan. KUM HS 3(a) memilih 8413 dari skor teks, lalu mesin bertanya subpos di bawah 8413. Pengguna tidak pernah ditanya apakah pompa itu untuk cairan atau udara, padahal master prompt meminta jenis pompa.
3. **"Pompa industri" (dua kata).** Pencarian tidak menemukan kandidat (satu kata bersama dianggap bukan bukti), jadi hasilnya langsung `NEED_EXPERT_REVIEW`. Aman, tetapi tidak bertanya jenis pompa seperti diminta master prompt.
4. **Crane di atas barge.** Pos 8426 (crane) dan 8905 (kapal, termasuk crane terapung) sama-sama dievaluasi, tetapi KUM HS 3(a) memilih 8426 dari skor teks tanpa menanyakan apakah crane itu terapung atau terpasang tetap. Fakta `mounting` tidak dipakai mesin. Hasil yang benar menurut ahli belum diketahui.

### G.4 Hal yang perlu Anda ketahui sebelum menggabung

- **Lisensi QuestPDF.** Dipilih sesuai keputusan Anda. Lisensi Community gratis untuk individu, nirlaba, proyek sumber terbuka, dan organisasi dengan pendapatan tahunan di bawah USD 1 juta; lembaga publik dan perusahaan tercatat bursa tidak memenuhi syarat ([QuestPDF Community License](https://questpdf.com/license/community.html)). Catatan yang sama ada di `CaseExportPdf.cs`. Bila tidak memenuhi syarat, perlu lisensi komersial atau ganti pustaka.
- **Migrasi `AddDailyUsage` belum diterapkan ke database lokal Anda.** Skrip ada di `docs/db/20261101160037_AddDailyUsage.sql`. Tanpa tabel itu, `POST /api/classification/start` akan gagal untuk role Classifier.
- **Dua perubahan lokal Anda tidak ikut commit:** port CORS 5174 di `Program.cs` dan `VITE_API_URL` di `.env.example` tetap sebagai perubahan belum di-commit.
- **Perilaku yang berubah dan terlihat pengguna:** (1) pengguna Classifier dibatasi 10 klasifikasi baru per hari (ubah di `Quota:DailyClassifications`, 0 = tanpa batas); (2) seri KUM HS 3(a) pada barang bukan campuran dan bukan set kini langsung ke 3(c), tidak lagi memakai pencocokan fakta `material`; (3) untuk campuran dan set yang masih seri, pengguna menjawab enam pertanyaan karakter pokok.
- **Tidak diverifikasi:** backend asli dan database SQL Server, tampilan hasil cetak PDF/DOCX, rate limit di bawah beban. Tes memakai EF InMemory (pencarian teks di InMemory peka huruf besar-kecil, di SQL Server tidak).

### G.5 EN dan Catatan: cabang `feature/en-notes-import` (ditumpuk di atas keenam cabang sebelumnya)

Sumber data: berkas milik Anda, bukan internet. EN dari `docs/EN HS 2022 KPU TANJUNG PRIOK v2 (AutoRecovered).docx`, Catatan Bagian/Bab/Subpos dari Lampiran II `docs/26~PMK.010~2022 BTKI 2022.docx`. Temuan pemeriksaan berkas ada di `TEMUAN_EN.md`.

**Yang dibangun** (hasil `dotnet test`: 395 lulus, 2 dilewati yang sama seperti G.3):

| Bagian | Isi |
|---|---|
| Parser | `EnTextParser` (EN), `LegalNotesTextParser` (Catatan, dua bahasa per halaman dipisah per jalur) |
| Muat ke database | `EnImportService`, `LegalNotesImportService`. Dua langkah seperti BTKI: dry run (laporan disimpan) lalu commit berkas yang sama (checksum). Hanya ke versi non-aktif. `replace` mengganti isi lama. Kolom `ImportBatch.Kind` (Btki, En, Notes) agar batch EN dan Catatan tidak menghalangi commit atau aktivasi BTKI. |
| Klon | `VersionCloneService`: salin versi aktif menjadi draft Indexed non-aktif. |
| Mesin | `EnrichAsync` kini memuat Catatan Bagian dan seluruh catatan pengecualian Bab, plus paragraf EN pengecualian. KUM HS 1 hanya mengeliminasi bila satu klausa menyebut nama barang sebagai butir yang dikecualikan, tanpa syarat ("yang ...", "untuk ...", "jika ..."), dan mengarahkannya ke pos atau bab lain. Kata di dalam tanda kurung dianggap contoh. Alasan eliminasi memuat asal catatan, kutipan klausa, dan `source_id`. |
| Shadow run | `POST /api/admin/versions/{id}/shadow-run` (Admin): kasus yang sama dijalankan di versi aktif dan draft, tanpa menyimpan apa pun, tanpa kuota, tanpa AI. |
| UI | Tab Upload Dokumen memuat EN dan Catatan ke draft; tab Versi punya "Klon jadi draft" dan "Shadow run". CHECKLIST bagian 12. |

**Yang sudah dijalankan di database lokal Anda** (setelah backup `C:\Users\Public\hs_backup\HsClassifierDb_before_kind_20261102_0706.bak`):

- Migrasi `AddImportBatchKind` diterapkan (kolom `Kind`, batch lama menjadi `Btki`).
- Draft baru "BTKI 2022 PDF resmi + EN + Catatan (draft)", id `30c94df4-2dd2-4b94-9006-0341fdd2048b`, status Indexed, tidak aktif. Isinya salinan versi aktif (16.322 baris) ditambah 22.695 paragraf EN (891 pos), 45 catatan Bagian, 361 catatan Bab, 66 catatan Subpos. 17 catatan subpos dilewati karena posnya tidak ada di versi itu (cakupannya bab 2 digit atau pos yang tidak ada).
- Versi aktif tidak berubah. Belum ada yang diaktifkan.
- Catatan Bagian hanya ada untuk 9 dari 21 Bagian (I, II, IV, VI, VII, XI, XV, XVI, XVII). Itu sesuai HS: Bagian lain memang tidak punya catatan Bagian.

**Hasil shadow run di database asli** (tanpa AI, 7 kasus bawaan dan 100 nama barang umum):

- Tidak ada kode final yang berubah antara versi aktif dan draft. Tidak ada eliminasi oleh Catatan di 100 nama barang setelah perbaikan klausa bersyarat. Sebelum perbaikan, "biskuit" dikeluarkan dari pos 1905 oleh klausa "biskuit ... untuk makanan hewan"; sekarang ada tes untuk itu.
- Kepercayaan naik karena batas 60 (tanpa Catatan) dan 75 (tanpa EN) terangkat: laptop 52 (Low) menjadi 82 (High), crane di atas tongkang 45 (Low) menjadi 75 (Medium).
- 11 dari 100 kasus berubah status, semuanya dari "review ahli" menjadi "perlu klarifikasi" (8 kasus) atau "ditetapkan" (laptop 8471.30.20, obeng 8205.40.00, robot industri 8428.70.00). Pos 8205, 8428, dan 8471 memang punya paragraf Inclusion di EN, yang menjelaskan perubahan itu (lihat butir 1 di bawah).

**Hal yang perlu keputusan Anda sebelum mengaktifkan draft**

1. **Aturan "Inclusion" lama di KUM HS 1 menjadi longgar.** Kode lama mempertahankan pos mana pun yang punya paragraf EN bertipe Inclusion, tanpa memeriksa isinya. Waktu EN kosong, aturan ini tidak pernah aktif. Dengan EN terisi, 729 dari 1.233 pos punya paragraf Inclusion, jadi KUM HS 1 mempertahankan banyak pos yang teks posnya tidak cocok dengan barang. Dampaknya terlihat positif di sampel (obeng dan robot ditemukan karena nama barang hanya ada di baris anak pos), tetapi ini keberuntungan, bukan pemeriksaan. Pilihan: biarkan, atau ketatkan agar Inclusion hanya mempertahankan pos bila paragraf itu menyebut barangnya (hasilnya mendekati versi aktif karena EN berbahasa Inggris). Saya belum mengubah apa pun.
2. **Crane di atas tongkang.** Di data asli, versi aktif dan draft sama-sama menetapkan 8426.11.00 (Medium 75 di draft), padahal tes dengan fixture meminta klarifikasi karena ada pos 8905 (crane terapung). Ini sudah ada sebelum EN, tapi dengan kepercayaan lebih tinggi hasil yang mungkin keliru jadi lebih menonjol. Perlu ahli menentukan hasil yang benar.
3. **Skor "tinggi" hanya karena Catatan dan EN ada.** Komponen "Catatan diuji" dan "EN ditinjau" kini terpenuhi bila datanya dimuat untuk pos itu, walau hanya pemeriksaan pengecualian yang benar-benar dijalankan. Ini belum mengubah label bila batas lain tidak terpenuhi, tetapi berarti skor 80 ke atas lebih mudah tercapai. Kalibrasi ulang kepercayaan (pertanyaan Anda yang belum dijawab) sebaiknya dikerjakan sebelum draft diaktifkan.
