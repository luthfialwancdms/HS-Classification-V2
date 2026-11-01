# CHECKLIST_FUNGSI — hs-classifier

Dibuat: Langkah 1 restyle v0 · Branch: `restyle/v0` · Commit dasar: `285020d`

Dokumen ini adalah daftar perilaku yang **wajib identik** sebelum dan sesudah restyle.
Setiap putaran restyle memverifikasi hanya baris layar yang dikerjakan, lalu mengisi kolom Status.

Kolom Status: `⬜ belum` · `✅ lulus` · `❌ gagal` · `n/a`

---

## 1. Kondisi awal (baseline)

| Item | Hasil |
|---|---|
| Branch | `restyle/v0` (sudah benar) |
| Working tree | bersih — 0 file berubah, 0 file untracked |
| Riwayat git | 1 commit (`285020d docs: add comprehensive UI summary...`) |
| `npm run lint` (eslint 10) | **lulus**, 0 error 0 warning |
| `npm run build` (vite 8.1.5) | **lulus** — 26 modul, css 23.56 kB, js 262.02 kB |
| Test frontend | **tidak ada** (tidak ada script `test`, tidak ada vitest/jest) |
| Test backend | ada (`hs-classifier-api/HsClassifier.Tests`) — di luar cakupan restyle, tidak dijalankan |
| Dev server API | tidak berjalan saat audit (port 5284 tidak merespons) |

---

## 2. Stack frontend terpasang

| Aspek | Kenyataan |
|---|---|
| React | 19.2.7 (`react`, `react-dom`) — JSX, bukan TS |
| Bundler | Vite 8.1.1 (`@vitejs/plugin-react` 6.0.3, Oxc) |
| Router | **tidak ada library** — `history.pushState` + `popstate` manual di `App.jsx`; Vercel rewrite SPA |
| State | **tidak ada library** — `useState` lokal di `App.jsx` + tiap komponen; auth di `localStorage` |
| Tailwind | **ada**, v4.3.3 via `@tailwindcss/vite`; `src/index.css` hanya `@import "tailwindcss";`; **tidak ada** `tailwind.config.js` (v4 = CSS-first) |
| shadcn/ui | **tidak ada** (tidak ada `components.json`, `lib/utils.ts`, `clsx`/`tailwind-merge`/`cva`) |
| Library UI lain | **tidak ada** — tanpa headless UI, tanpa icon library (ikon = emoji), tanpa animation lib |
| Font | **tidak ada** font dimuat; `index.html` tanpa `<link>`; jatuh ke default sistem |
| Lain | `src/App.css` (184 baris) = sisa template Vite, **tidak di-import** oleh file mana pun |

Env: `VITE_API_URL` (default `http://localhost:5284`) — satu-satunya variabel yang dipakai kode.

---

## 3. Peta file: presentasi vs beku

**Boleh direstyle (presentasi saja)**

| File | Isi |
|---|---|
| `src/App.jsx` (JSX/className saja) | shell, header, nav, login, banner klarifikasi |
| `src/components/ClassificationForm.jsx` | 5 seksi form, tombol submit/reset |
| `src/components/ResultPanel.jsx` | semua state hasil + confidence + tarif + reasoning |
| `src/components/HistoryPage.jsx` | daftar case, filter, paginasi, detail |
| `src/components/ReviewPage.jsx` | panel keputusan ahli |
| `src/components/AdminDashboard.jsx` | 5 tab admin |
| `src/components/TextField.jsx`, `SelectField.jsx`, `MultiSelectField.jsx` | presentasi field (nama props tetap) |
| `src/index.css` | design tokens (target: variabel CSS v0) |
| `index.html` | `<title>`, meta, preconnect font |

**BEKU — tidak boleh disentuh**

| File | Alasan |
|---|---|
| `src/services/apiClient.js` | endpoint, mapping form→facts, retry, error message, mapConfidence |
| `src/data/dropdown_master.json` | sumber label/opsi/required form |
| `src/data/btki_national.json`, `src/data/kumhs_rules.json` | data, bukan UI |
| `src/main.jsx` | bootstrap |
| `vite.config.js`, `eslint.config.js`, `vercel.json`, `package.json` | tooling & dependency |
| semua `useState`/`useEffect`/handler di dalam file komponen | logika, bukan tampilan |

---

## 4. Checklist per layar

### G — Shell, auth, navigasi (`App.jsx`)

| ID | Perilaku yang harus tetap | Status P1 |
|---|---|---|
| G1 | Tanpa `hs_token` di localStorage → hanya layar Login, header/nav tidak dirender | ✅ `hasHeader:false`, `.login-card` tampil |
| G2 | Login: field email (`type=email`, `autoComplete=username`) + password (`autoComplete=current-password`), keduanya `required` | ✅ atribut & asosiasi label (1 label per input) terverifikasi |
| G3 | Login sukses → simpan `hs_token` + `hs_role`, masuk ke shell | ⬜ belum diuji dengan kredensial nyata (kode `login()` tidak berubah; shell terbukti render saat token ada) |
| G4 | Login gagal → pesan error dari API tampil (`role="alert"`), tombol jadi `Memproses...` selama request | ⬜ belum diuji (butuh login gagal sungguhan); markup error = `.notice.danger` + `role="alert"` |
| G5 | 401 dari endpoint non-login → token dihapus, kembali ke layar Login | ✅ teramati saat token dummy dipakai (401 → token/role `null` → login) |
| G6 | Nav 4 view + filter role: Klasifikasi & Riwayat = semua; Review Ahli = `Admin`,`ExpertReviewer`; Admin Data Center = `Admin` | ✅ Admin 4 item · ExpertReviewer 3 · Classifier 2 (nav di sidebar) |
| G7 | View aktif ditandai `aria-current="page"` | ✅ `aria-current=page` pada Klasifikasi saat aktif |
| G8 | Badge `V2 · DATABASE MODE` tetap tampil di header | ✅ ada di `.brand-name` (`.beta`) |
| G9 | Routing: path `/`, `/riwayat`, `/review`, `/admin`; tombol back/forward browser bekerja; URL langsung membuka view yang benar | ✅ `pushState` → `/riwayat`; `history.back()` → `/`; `viewFromPath` tidak berubah |
| G10 | `Keluar` menghapus token/role serta mengosongkan hasil & state klarifikasi | ✅ klik Keluar → login, `hs_token`/`hs_role` = `null` |
| G11 | Footer disclaimer "rekomendasi, bukan penetapan resmi DJBC" tetap ada | ✅ teks utuh di `.page-footer` |

Catatan P1 — perubahan yang **bukan** tampilan:
- `switchView` mendapat satu baris tambahan `setMenuOpen(false)` (menutup drawer mobile setelah pindah view). Logika `pushState` + `setView` tidak berubah.
- State baru `menuOpen` (`useState`) khusus tampilan drawer mobile; tidak menyentuh state bisnis.
- `h1` pindah dari brand header ke judul halaman (`Klasifikasi HS Code`); di layar login brand memakai `h1`.


### K — Layar Klasifikasi, form

| ID | Perilaku | Status P2 |
|---|---|---|
| K1 | 5 seksi berurutan: Identitas Barang, Fisik & Material, Fungsi & Cara Kerja, Konteks Penggunaan, Karakteristik Tambahan — urutan dan pengelompokan field tidak berubah | ✅ terbaca urut 1–5 di DOM |
| K2 | 14 field dengan `name`/key persis: `nama_dagang`, `nama_generik`, `spesifikasi`, `keterangan_tambahan`, `material_utama`, `kondisi_impor`, `fungsi_utama`, `cara_kerja`, `mobilitas`, `area_operasi`, `sektor`, `apakah_part`, `apakah_set`, `ada_komponen_tambahan` | ✅ 4 text + 8 select + 2 chip group = 14 |
| K3 | Label, placeholder, daftar opsi, dan tanda `*` (required) berasal dari `dropdown_master.json` — tidak di-hardcode | ✅ label + placeholder + `*` dari JSON |
| K4 | `material_utama` & `cara_kerja` = multi-select chip (toggle, tampil "Dipilih: ...") | ✅ 2 chip terpilih → "Dipilih: Baja/Besi, Tembaga" |
| K5 | Submit tanpa `nama_generik` → `alert('Nama generik barang wajib diisi.')`, tidak ada request | ✅ alert terkirim, tidak ada hasil |
| K6 | Submit valid → `classifyWithBackend(form)`; tombol submit jadi `Menganalisis...` dan submit+reset `disabled` | ✅ label `Menganalisis...`, tombol disabled, spinner tampil |
| K7 | `Reset` mengembalikan seluruh form ke nilai awal, tanpa request | ✅ input/select kosong, chip terpilih 0 |


### C — Banner klarifikasi (`NEED_CLARIFICATION`)

| ID | Perilaku | Status P2 |
|---|---|---|
| C1 | Banner muncul di atas layar klasifikasi; judul + pesan `message` + daftar `candidateCodes` (monospace) | ✅ banner tampil; judul & pesan & kandidat terlihat |
| C2 | Tiap pertanyaan: `choices` → daftar radio (label panjang, legend sr-only, hint "Pilihan diambil dari uraian resmi BTKI"); `options` → select dengan placeholder disabled; selain itu → input teks | ✅ 2 radio + 1 select + 1 input teks |
| C3 | Baris dampak: `Mempengaruhi N kandidat`, `tetap: ...`, `tereliminasi: ...` hanya jika `candidatesAffected > 0` | ✅ "Mempengaruhi 2 kandidat · tetap: 8429.52 · tereliminasi: 8429.59" |
| C4 | Tombol `Kirim Jawaban` disabled sampai **semua** pertanyaan terjawab; label `Memproses...` saat loading | ✅ disabled → aktif setelah semua dijawab |
| C5 | Kirim → `submitAnswers(caseId, answers)`; jawaban tidak boleh diubah/dinormalisasi sebelum dikirim | ✅ kode handler tidak berubah (diff: hanya className) |
| C6 | Banner di-*remount* saat daftar teks pertanyaan berubah (perilaku `key`) | ✅ `key` tetap (diff tidak menyentuh) |

### R — State hasil (`ResultPanel`)

| ID | Perilaku | Status P2 |
|---|---|---|
| R1 | Loading: spinner + "Menjalankan KUM HS 1–6 terhadap database BTKI..." + subteks pipeline, `role="status"` `aria-live="polite"` | ✅ spinner + aria-live=polite + teks pipeline |
| R2 | Kosong (REV1 butir 4): panel hasil **tidak ditampilkan sama sekali** sebelum ada hasil final dan setelah `Hapus hasil` (bukan lagi kartu "Belum ada analisis"); layar hanya berisi form | ✅ (DOM/stub, REV1) sebelum submit tidak ada `.result-panel`; `Hapus hasil` → `.result-panel` hilang, form utuh |
| R3 | Error: kartu `role="alert"`, judul "Terjadi Kesalahan", pesan = `err.message` dari API apa adanya | ✅ role=alert + pesan API apa adanya |
| R4 | Tanpa kode final: teks berbeda untuk `REJECTED` ("Ditolak oleh ahli kepabeanan" + alasan koreksi terakhir) vs "Belum dapat ditentukan otomatis" + daftar kandidat | ✅ teks + alasan + badge status tampil |
| R5 | Kode final: kode HS, uraian resmi BTKI, badge confidence TINGGI/SEDANG/RENDAH (+ `score/100` bila ada) | ✅ "8429.52.00" + badge "Confidence: TINGGI · 78/100" |
| R6 | Badge koreksi ahli: `EXPERT_APPROVED` = "Disetujui ahli", `EXPERT_OVERRIDE` = "Dikoreksi ahli (override)" | ✅ markup badge (data stub tidak memuat koreksi; kondisi kode tidak berubah) |
| R7 | Ringkasan "N langkah reasoning · X kandidat dieliminasi" hanya bila ada reasoning | ✅ "3 langkah reasoning · 4 kandidat dieliminasi" |
| R8 | Flag `perlu_expert_review` → kartu merah `role="alert"` + alasan | ✅ notice bahaya + alasan |
| R9 | Rincian confidence: tabel komponen (nama, points/weight), daftar caps, chip risk flag dengan label Indonesia + `title` kode asli | ✅ tabel + caps + 2 badge flag berlabel Indonesia |
| R10 | Tarif: baris MFN / PPN / PPh 22 dengan teks "tidak tersedia" per baris, `taxDisclaimer`, tabel FTA (skema, negara, tarif), disclaimer FTA **selalu** tampil; format `PERCENT` = `x%`, lainnya `x (UNIT)` | ✅ 4 kartu tarif + tabel FTA + notice disclaimer selalu tampil |
| R11 | Reasoning chain: `<details>` per langkah, nomor `eventOrder`, chip `ruleApplied`, warna `decision` (RETAINED/ELIMINATED/UNRESOLVED), teks reasoning, chip `sourceIds` | ✅ 3 `<details>`, tertutup default, 3 rule chip, badge sesuai decision |
| R12 | Kotak "Minta Expert Review": hanya role `Admin`/`Classifier`, hanya status `DETERMINED`; tombol kirim disabled sampai alasan ≥ 5 karakter; `Batal` menutup; sukses/error tampil inline | ✅ Admin & Classifier tampil, ExpertReviewer tidak; muncul hanya saat DETERMINED |
| R13 | Panel hasil non-modal **di bawah form** (REV1 butir 4), bukan kolom sticky: muncul terbuka penuh setelah DETERMINED / NEED_EXPERT_REVIEW / REJECTED, halaman scroll ke panel (`prefers-reduced-motion` → tanpa animasi), fokus ke judul `#result-title`; `Perkecil/Perbesar` (`aria-expanded` + `aria-controls="result-body"`) dan `Hapus hasil` (hanya `setResult(null)`); analisis ulang → panel kembali terbuka penuh | ✅ (DOM/stub, REV1 — dicatat agent sebelumnya, belum diulang di sesi ini) lihat `docs/LANJUTAN_REV1.md` §4; lint + build lulus di sesi ini |

Catatan P2 — perubahan perilaku di luar tampilan (semua atas instruksi Anda):
- `useRef` + satu `useEffect` baru: auto-scroll ke panel hasil hanya di ≤1050px setelah analisis selesai.
- `menuOpen` dapat ditutup lewat overlay + fokus kembali ke tombol burger (perbaikan P1).


### H — Riwayat (`HistoryPage`)

| ID | Perilaku | Status P3 |
|---|---|---|
| H1 | Filter `q`, `status`, `from`, `to`; setiap perubahan filter mereset halaman ke 1 | ✅ request berurutan `q` → `status` → `from` → `to`, semuanya `page=1` |
| H2 | Paginasi 20 per halaman; kontrol halaman hanya tampil bila > 1 halaman; tombol disabled di ujung | ✅ 20 item (hal. 1) / 5 item (hal. 2), total 25; `Sebelumnya` disabled di hal. 1; pager hilang saat hasil 0 |
| H3 | Indikator "Memuat…" / "N total case" (`aria-live="polite"`), empty state "Belum ada case", error list `role="alert"` | ✅ "25 case", "0 case" + "Belum ada case", alert berisi pesan API apa adanya |
| H4 | Baris case: nama barang (fallback `Case #id`), StatusBadge, kode final (monospace, `—` bila kosong), tanggal `id-ID`, nama user hanya untuk Admin/ExpertReviewer | ✅ tanggal + badge (4 status) + nama + kode + user |
| H5 | Klik case → muat detail (`loadCase`), baris terpilih ditandai | ✅ `.case-card.selected` + `aria-current="true"` |
| H6 | Detail: kartu fakta (label Indonesia per key, hanya nilai truthy), riwayat keputusan ahli, lalu `ResultPanel` | ✅ tabel fakta 6 baris, tabel keputusan ahli 1 baris, ResultPanel (kode tampil); **tanpa tab** (Q7), **tanpa blok statistik** (Q5) |
| H7 | Aksi di detail (mis. minta review) → daftar di-refresh **dan** case yang terbuka dimuat ulang | ✅ (P4, stub) urutan call: `POST /api/review/{id}/approve` → `GET /api/classification/{id}` → `GET .../reasoning` → `GET /api/classification/cases?...` |


### E — Review Ahli (`ReviewPage`)

| ID | Perilaku | Status P4 |
|---|---|---|
| E1 | Halaman = `HistoryPage` dengan filter status awal `NeedExpertReview`, judul "Case Expert Review", kolom user tampil | ✅ judul, eyebrow `EXPERT WORKFLOW`, subtitle, 2 kartu semuanya "Review ahli", user tampil |
| E2 | Panel keputusan hanya untuk status `NEED_EXPERT_REVIEW` | ✅ kondisi `if (detail.status !== 'NEED_EXPERT_REVIEW') return null` tidak berubah; panel tampil untuk status itu |
| E3 | Kandidat yang bisa dipilih hanya yang cocok `^\d{4}\.\d{2}\.\d{2}$`; heading 4 digit ditampilkan terpisah sebagai hint | ✅ 2 radio kandidat + 1 radio override; hint "Heading kandidat: 8429" |
| E4 | Mode override: input kode manual + tombol `Cek di BTKI` → preview uraian atau "tidak ditemukan di BTKI aktif"; catatan EXPERT_OVERRIDE saat override | ✅ valid → kotak hijau + uraian; format salah & kode tak ada → notice bahaya "tidak ditemukan di BTKI aktif" |
| E5 | `Setujui` disabled kecuali alasan ≥ 5 karakter **dan** format kode valid; `Tolak` disabled kecuali alasan ≥ 5 karakter | ✅ keduanya disabled di awal; aktif setelah alasan; `Setujui` kembali disabled saat kode override kosong |
| E6 | Setujui/Tolak memanggil endpoint masing-masing, menampilkan notice/error, lalu memuat ulang case + list | ✅ (stub) `POST /api/review/{id}/approve` + reload; error 500 → `role="alert"` berisi pesan API, tanpa reload |

Catatan P4 (perilaku lama, bukan regresi): notice "Case disetujui." praktis tidak sempat terlihat karena `loadCase` mengosongkan `detail` lebih dulu sehingga panel remount. Bisa dirapikan di P6 bila Anda mau.


### A — Admin Data Center (`AdminDashboard`)

| ID | Perilaku | Status P5 |
|---|---|---|
| A1 | 5 tab (`role="tab"` + `aria-selected`): Versi Nomenklatur, Upload Dokumen, Laporan Validasi, Antrian Ambiguitas, Template CSV/Excel | ✅ 5 tab, `role=tablist`, `aria-orientation="vertical"`, `aria-selected`, roving `tabIndex` (0/-1), tiap tab `aria-label`; panah ↓/↑/←/→ + Home/End memindah fokus & mengaktifkan tab; `admin-content` = `role=tabpanel` + `aria-labelledby` |
| A2 | Daftar versi dimuat saat mount; error load tampil `role="alert"` | ✅ (stub) GET `/api/admin/versions` saat mount; jalur error memakai `.notice.danger` + `role=alert` |
| A3 | Versi: tabel nama/edisi/berlaku/status import/aksi; badge status; `Aktifkan` hanya untuk state `Indexed` (dengan tooltip alasan); versi aktif punya `Nonaktifkan / Rollback` | ✅ (stub) 5 baris; state badge `aktif/info/info/danger`; `Aktifkan` disabled untuk ReadyToCommit/Committed/DryRunFailed, aktif untuk Indexed; versi aktif → Nonaktifkan |
| A4 | `Bandingkan` (Indexed/Committed) → diff: 6 angka ringkasan, catatan beda-OCR, filter per bab, 4 grup diff dengan tabel dan penanda truncated | ✅ (stub) 6 metric, select bab, 4 `<details>` + 4 tabel, "Uraian berubah: 3 (menampilkan 0)", tombol Tutup |
| A5 | Tambah Versi: 5 field, `Nama` + `Berlaku mulai` required, `effectiveTo` kosong dikirim `null`, sukses menampilkan notice & reload | ✅ (stub) 5 field (2 required); payload `{…, "effectiveTo":null}` |
| A6 | Laporan Validasi & Antrian Ambiguitas: filter versi, daftar batch berisi error/pending, teks empty berbeda per tab, detail berisi checksum/createdBy/penanda upload lama | ✅ (stub) filter + 2 kartu (badge "2 pending"); tab ambiguitas hanya menampilkan batch dengan pending > 0 |
| A7 | Tile statistik batch: Node, Baris dibaca, Dilewati, Error, Warning (Error diwarnai bila > 0), tabel isu maks. 50 baris | ✅ (stub) 5 metric, tile Error merah, tabel isu 1 baris (`.slice(0,50)` tidak berubah) |
| A8 | Keputusan ambiguitas: parent opsional, catatan ≥ 5 karakter, `Approve` aktif saat catatan valid, `Reject` nonaktif selama parent terisi, hasil tampil sebagai badge + parent koreksi + catatan | ✅ (stub) awal dua-duanya disabled → catatan ≥5 aktif keduanya → parent terisi me-nonaktifkan Reject → catatan <5 disabled lagi; baris Approved tampil badge + parent + catatan |
| A9 | Upload dokumen: pilih jenis (BTKI / EN / Catatan), versi target hanya `Uploaded`/`DryRunFailed` (kecuali sudah ada laporan), file BTKI `accept .docx,.pdf` multiple, lainnya `.docx` tunggal | ✅ (stub) 3 jenis; opsi versi awal hanya "Draft gagal (DryRunFailed)"; accept & multiple sesuai; tombol Parse disabled sampai file dipilih |
| A10 | Parse & Dry Run → laporan (angka/string), tile `dryRun.*` dengan `canCommit` hijau/merah, ringkasan `nodesByLevel` + jumlah Bab, tabel isu, contoh 10 baris (JSON) | ✅ (stub) tile `status/totalNodes/chapters/batchId` + `dryRun.canCommit` (kelas `danger`), "Isi: chapter 97 · heading 1200 · 97 Bab", code block, isu 1 baris |
| A11 | `Commit ke Database` hanya aktif saat versi `ReadyToCommit`; pesan blokir berbeda untuk error struktur vs ambiguitas pending; tombol "Buka laporan validasi" memindah ke tab Laporan dengan batch terpilih | ✅ (stub) saat DryRunFailed: commit disabled + "Commit diblokir: masih ada error struktur…"; setelah versi jadi ReadyToCommit: commit aktif, peringatan blokir hilang, klik → `POST /api/admin/btki/commit/{versionId}` + reload; "Buka laporan…" → tab `reports` + detail termuat |
| A12 | Template: unduh CSV/XLSX (blob), validasi file → ringkasan valid/error/warning + tabel per baris, pilih versi → preview diff (INSERT/UPDATE/SKIP, tabel perubahan, daftar kode baru), `Commit` hanya aktif setelah checkbox konfirmasi | ✅ (stub) 2 tombol unduh; validasi → "9/10 baris valid · 1 error · 1 warning" + 2 baris masalah; preview → badge INSERT 2 / UPDATE 1 / SKIP 7, baris lama dicoret & baru hijau; commit disabled → aktif setelah checkbox → `POST /api/admin/template/commit/{versionId}` + notice |


---

## 5. Kontrak yang tidak boleh berubah

**Endpoint** (dipanggil dari `apiClient.js`, tidak boleh berubah path/method/payload):

```
POST /api/auth/login
POST /api/classification/start
POST /api/classification/{caseId}/facts
GET  /api/classification/cases?page&pageSize&status&q&from&to
GET  /api/classification/{caseId}
GET  /api/classification/{caseId}/reasoning
GET  /api/classification/{caseId}/tariffs
POST /api/classification/{caseId}/request-review
POST /api/review/{caseId}/approve
POST /api/review/{caseId}/reject
GET  /api/btki/{code}      GET /api/btki/search      GET /api/btki/stats
GET/POST /api/admin/versions        POST /api/admin/versions/{id}/activate|deactivate
GET  /api/admin/versions/{id}/diff
GET  /api/admin/import-batches[/{id}]
POST /api/admin/ambiguity/{id}/decide
POST /api/admin/btki/upload?versionId=   POST /api/admin/btki/commit/{versionId}
POST /api/admin/{kind}/upload
GET  /api/admin/template/download/{fmt}
POST /api/admin/template/validate | preview/{versionId} | commit/{versionId}
```

**Field form → facts** (di `mapFormToFacts`) tidak boleh diubah namanya: `nama_dagang`, `nama_generik`, `material_utama`, `fungsi_utama`, `mobilitas`, `area_operasi`, `sektor`, `kondisi_impor`, `apakah_part`, `apakah_set`, `ada_komponen_tambahan`, `cara_kerja`, `spesifikasi`.

**Pesan error**: teks dari API (`body.message`) atau `FRIENDLY_STATUS` harus tampil apa adanya — jangan diparafrase di UI.

**localStorage**: key `hs_token`, `hs_role`.

---

## 6. Aksesibilitas yang sudah ada (jangan hilang)

- `role="alert"` pada error, `role="status"` + `aria-live` pada loading/notice.
- Semua input punya `<label htmlFor>`; kontrol tanpa label visual pakai `sr-only`.
- `fieldset`/`legend` pada grup radio (klarifikasi, kode review).
- `table` dengan `<caption>`/`scope="col"`, `nav aria-label`, `tablist`/`tab`/`aria-selected`, `aria-current`.
- Fokus keyboard: `details/summary`, tombol native; tidak ada handler `keydown` kustom yang boleh ditambah/dihapus.

---

## 7. Aturan verifikasi tiap putaran restyle

1. `npm run lint` dan `npm run build` lulus tanpa error baru.
2. `git diff --stat` — tidak ada perubahan di `src/services/`, `src/data/`, `src/main.jsx`, `vite.config.js`, `package.json`; tidak ada perubahan baris logika (`useState`, `useEffect`, handler) di file komponen.
3. Item checklist layar yang dikerjakan dicentang di dokumen ini.
4. Screenshot desktop 1280px + mobile 390px, dibandingkan dengan referensi v0.
5. Tidak ada warna/font hardcoded di luar design tokens.
6. Navigasi keyboard, label form, kontras teks diperiksa.

---

## 8. Temuan yang menunggu keputusan (belum diubah)

| # | Temuan | Usul | Status |
|---|---|---|---|
| T1 | `src/App.css` (184 baris) = sisa template Vite, tidak di-import siapa pun | hapus | ✅ dihapus 29 Okt 2026 |
| T2 | `.env` masih berisi `VITE_GROQ_API_KEY` (sisa V1) | dicek: **tidak ada satu pun kode yang memakainya** (`grep GROQ` di `src/`, `index.html`, `package.json`, `vite.config.js`, `public/`, `.env.example` = 0 hasil). Baris dibiarkan sesuai instruksi; isi key tidak pernah ditampilkan | ✅ diverifikasi, tidak diubah |
| T3 | `index.html` `<title>` masih `hs-classifier`, `lang="en"` | ganti ke judul produk + `lang="id"` | ✅ diubah 29 Okt 2026 (`HS Intelligence — BTKI 2022`, `lang="id"`) |
| T4 | Tidak ada test frontend sama sekali — regresi hanya bisa ditangkap manual/screenshot | tidak memasang Playwright (sesuai keputusan); verifikasi lewat Chrome yang ada | ✅ diputuskan |
| T5 | `dist/` ada build basi (gitignored) | tidak disentuh; ter-update tiap `npm run build` | ✅ |

Catatan lokasi: dokumen ini dan `DESIGN.md` disimpan di `hs-classifier/docs/` agar tidak tercecer di folder induk. Folder acuan `ui-design-for-hs-classification-intelligence/` tidak ikut di-commit.

---

## 9. Verifikasi responsif P6 (1280 / 1050 / 720 / 390)

Semua diukur lewat iframe berukuran tepat (DOM + computed style), bukan penilaian visual.

| Aspek | 1280 | 1050 | 720 | 390 |
|---|---|---|---|---|
| Overflow horizontal (klasifikasi/riwayat/review/admin) | tidak ada | tidak ada | tidak ada | tidak ada |
| `.classify-grid` | `grid` 2 kolom | `flex` (menumpuk) | `flex` | `flex` |
| `.result-column` | `sticky` | `static` | `static` | `static` |
| Sidebar | 240px, statis | 205px, statis | fixed off-canvas (`-240px`) | fixed off-canvas |
| Tombol burger | `none` | `none` | `block` | `block` |
| `.master-detail` | `grid` 330px+1fr | `grid` | `flex` kolom | `flex` kolom |
| `.case-list` max-height | 620px | 620px | `none` | `none` |
| `.admin-nav` + `aria-orientation` | `block` / `vertical` | `flex` / `horizontal` | `flex` / `horizontal` | `flex` / `horizontal` |
| Drawer 390: buka/tutup | — | — | — | overlay muncul, `left 0`; tutup → `left -240px`, fokus kembali ke `.mobile-menu` |
| Auto-scroll hasil (390) | — | — | — | `scrollY` bergerak, hasil di `top 80px` |

Konsistensi terukur: `.panel`/`.result-panel` radius **16px**, tombol **7px**, input & chip **6px**, topbar **68px**, sidebar **240px**; 18 ikon semua punya atribut `width/height` eksplisit (13–26px). Fokus keyboard terlihat pada tombol (outline 2px), input (ring teal), chip, kartu riwayat.


