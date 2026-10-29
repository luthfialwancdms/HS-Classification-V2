# RINGKASAN RESTYLE v0 — hs-classifier

Baseline: commit `285020d` (sebelum restyle). Semua pekerjaan di branch `restyle/v0`.
Acuan visual: `ui-design-for-hs-classification-intelligence/` (ekspor v0, Next.js). Folder acuan **tidak** diubah dan **tidak** di-commit dalam rangkaian ini.

Prinsip yang dipegang: hanya lapisan presentasi berubah. `services/`, `src/data/`, `main.jsx`, `vite.config.js`, `package.json` (kecuali 2 dependency font+lucide), dan file di luar `hs-classifier/` tidak berubah.

---

## 1. Commit per tahap

| Tahap | Commit | Isi utama |
|---|---|---|
| P0 | `dd0b89b` | `restyle: fondasi tokens + font Lexend` — `@fontsource/lexend@5.3.0`, `lucide-react@1.64.0`, `src/styles/design-system.css` (port `globals.css` v0), `src/index.css` (impor font + design system), hapus `src/App.css` mati, `index.html` (`lang="id"`, judul), `docs/CHECKLIST_FUNGSI.md`, `docs/DESIGN.md` |
| P1 | `f1cae34` | `restyle: shell, header, login` — topbar + sidebar 240px, drawer mobile, layar login, footer, banner klarifikasi |
| P2 | `9189934` | `restyle: layar klasifikasi` — form 5 seksi, panel hasil inline dengan semua state (loading/kosong/error/klarifikasi/tanpa kode/hasil+tarif+confidence+reasoning) |
| P3 | `025f4f9` | `restyle: layar riwayat` — toolbar filter, daftar `.case-card`, paginasi, detail (tabel fakta + keputusan ahli) |
| P3.1 | `9158133` | `restyle: batasi nama & tinggi daftar riwayat` — line-clamp 2 + daftar scroll 620px (≥721px) |
| P4 | `ece22e6` | `restyle: layar review ahli` — panel `.expert-panel` + semua gate keputusan |
| P5 | `921b68d` | `restyle: layar admin data center` — `.admin-nav` vertikal + 5 panel, `docs/TEMUAN.md` |
| P6 | _lihat commit terakhir_ | responsif + polish: `aria-orientation` dinamis, kontras `.btki-check`, fokus keyboard, dokumen ini |

---

## 2. Deviasi dari acuan v0 (disetujui)

| # | Deviasi | Alasan / nilai |
|---|---|---|
| D1 | **Indigo → teal.** 12 nilai indigo sisa di acuan dinormalisasi ke keluarga `--primary` | Acuan aslinya bertema indigo lalu di-*re-theme* sebagian. Peta lengkap: `docs/DESIGN.md` §2.1. Contoh: `#eef2ff`→`var(--blue-bg)`, `#315cf6`→`#1c7b6f`, `#f1f5ff`/`#f8faff`→`#f1f8f6`/`#f8fcfb` |
| D2 | **Token digelapkan** untuk kontras teks 9–11px | `--muted` `#6d817d`→`#637572` (4.55:1), `--primary` `#1d8175`→`#1c7b6f`, `--green` `#1e8a61`→`#1b7e58`, `--amber` `#b66a16`→`#a45f14`, `--red` `#c9464d`→`#c1434a`; teks `#98a2b3`→`var(--muted)` |
| D3 | `.btki-check span` `#4a8d71`→`#407961` (3.55:1 → **4.6:1**, hue sama) | P6 (c) |
| D4 | `.brand-name` `font-weight: 750` → **800** | Paket statis Lexend hanya kelipatan 100 |
| D5 | **Tanpa modal hasil** | Hasil tetap panel inline di kolom kanan (keputusan Q4) |
| D6 | **Tanpa tab** di detail riwayat/review | Semua seksi tampil bertumpuk sebagai kartu (Q7) |
| D7 | **Tanpa shadcn/cva/clsx/tailwind-merge** | Gaya tombol ditiru dengan className + CSS variable; dependency hanya font + lucide |
| D8 | `@import` Google Fonts, `tw-animate-css`, `shadcn/tailwind.css`, `@custom-variant dark` dibuang | Tidak dipakai; font dibundel |
| D9 | Aturan `.tabs`/`.tab` dan `.analysis-modal*` tidak diporting | Tidak dipakai (Q4/Q7); CSS mati tidak dibawa |
| D10 | `.admin-nav` memakai semantik tab ARIA + `aria-orientation` dinamis | v0 hanya tombol biasa (keputusan P5/P6) |

---

## 3. Perubahan layout (bukan sekadar warna)

| Layar | Sebelum | Sesudah |
|---|---|---|
| Shell | navbar horizontal di header | **topbar 68px + sidebar 240px**; di ≤720px sidebar jadi drawer off-canvas + overlay, fokus balik ke tombol burger |
| Klasifikasi | 2 kolom Tailwind, hasil `lg:sticky` | **2 kolom ≥1051px** dengan kolom hasil sticky; **<1051px menumpuk** (form di atas) + auto-scroll ke hasil setelah analisis |
| Riwayat | grid 2 kolom penuh | `.master-detail` 330px + 1fr; daftar ≤620px dengan scroll sendiri (≥721px) |
| Review | panel Tailwind di kolom detail | `.expert-panel` v0 di kolom detail |
| Admin | tab horizontal di atas konten | `.admin-layout` 210px + 1fr, nav vertikal (horizontal ≤1050px) |

Catatan: acuan v0 menumpuk form & hasil dalam satu kolom di semua lebar. 2 kolom + sticky adalah keputusan Anda (Q2/P2), ditandai `INTERPRETASI`.

---

## 4. Daftar INTERPRETASI (tidak ada di acuan v0)

### 4.1 Kelas CSS baru
`.data-table` (+`caption/th/td/.num`), `.table-scroll`, `.mono`, `.badge.neutral`, `.badge.info`, `.state.info`, `.state.danger`, `.notice.danger`, `.notice.success`, `.login-page`, `.login-card`, `.login-brand`, `.login-sub`, `.page-footer`, `.clarify-wrap`, `.clarify-panel`, `.clarify-questions`, `.field-label`, `.hint`, `.choice-list`, `.choice`, `.sidebar-overlay`, `.result-column`, `.span-2`, `.result-section`, `.result-unresolved`, `.flag-list`, `.spinner`, `.reason-list`, `.reason-details`, `.rule-chip`, `.reason-body`, `.review-request`, `.case-date`, `.pager`, `.detail-column`, `.empty-state.compact`, `.metric-grid`, `.metric`, `.row-actions`, `.decision-cell`, `.cell-input`, `.diff-block`, `.code-block`, `.check-row`, `.file-input`, `.text-danger/.text-success/.text-warning`, `.val-old`, `.val-new`.

### 4.2 State & fokus yang ditambahkan ke kelas v0
`:disabled` (semua varian tombol) — acuan tidak punya gaya disabled padahal kondisi enabled/disabled adalah perilaku yang dibekukan; `:focus-visible` untuk tombol, `.nav-item`, `.chip`, `.admin-nav button`, `.case-card`, `.mobile-menu`, `.file-input`, `.check-row input`, `.choice input`, `.candidate input`, `summary`.

### 4.3 Struktur/isi
- Login: kartu `.panel` di tengah (v0 tidak punya login), error `.notice.danger`.
- Footer disclaimer dipertahankan (bukan bagian v0).
- Sidebar: **tanpa** pemilih workspace, notifikasi, Pengaturan, badge angka nav, dropdown user (Q8). Version card hanya memuat teks benar ("BTKI 2022", "Mode database").
- Banner klarifikasi memakai `.panel` + `.notice` + `.field`/`.choice`.
- Panel hasil: heading "Hasil Analisis" selalu tampil; urutan blok: hero → meta → tarif → confidence → flag expert → reasoning → minta review.
- Reasoning tetap `<details>` per langkah (tertutup default) — v0 selalu terbuka (perilaku R11).
- Pertanyaan ya/tidak form tetap `<select>` (bukan radio-grid v0) agar nama field & nilai tidak berubah.
- Riwayat: toolbar filter nyata (cari/status/dari/sampai) menggantikan tombol filter dekoratif; tanpa `stats-grid` (Q5); tanpa tab (Q7); data tabular tetap `.data-table`.
- Review: tanpa `.queue`/`.review-card`/badge URGENT (tidak ada data urgensi).
- Admin: tabel + `.metric-grid` (v0 hanya mencontohkan `.version-row`); angka hanya dari API.
- Tombol tanpa fungsi di acuan (Export PDF/Excel, Panduan, Browse BTKI, Pengaturan) **tidak dibuat**.

### 4.4 Perilaku UI yang ditambahkan (presentasi saja)
- `menuOpen` + overlay drawer + fokus balik ke burger.
- `useEffect` auto-scroll ke hasil di ≤1050px setelah analisis selesai.
- `useNarrow()` (matchMedia 1050px) → `aria-orientation` dinamis pada nav admin; keyboard menerima ↑↓←→ + Home/End.
- Nama barang di kartu riwayat dibatasi 2 baris.

---

## 5. Aksesibilitas

- Label form: setiap kontrol punya label (wrapper `<label class="field">`), kecuali kontrol yang memang diberi `sr-only` label.
- `role="alert"` untuk error, `role="status"` + `aria-live` untuk loading/notice.
- `aria-current="page"` pada nav aktif; `aria-selected`, `tabIndex` roving, `aria-controls`/`aria-labelledby` pada tab admin; `aria-expanded`/`aria-controls` pada tombol drawer.
- Tombol ikon-saja punya `aria-label` (burger); ikon dekoratif `aria-hidden`.
- Fokus keyboard terlihat di semua kontrol yang diuji Tab: tombol (outline 2px `--primary`), input/select/textarea (ring teal), radio/checkbox, chip, kartu riwayat, `summary`.
- Kontras terukur (WCAG 2.1, dihitung dari nilai final): `--foreground` 13.1:1; label `#536070` 6.4:1; `--primary` di putih 4.7:1; teks sekunder 4.55:1; badge sukses/peringatan/bahaya 4.55:1; `.btki-check span` 4.6:1.

---

## 6. Metode verifikasi (jujur, apa adanya)

| Metode | Dipakai untuk |
|---|---|
| **Stub `window.fetch` via CDP** (tidak ada file proyek/backend yang diubah) | Semua state data: hasil klasifikasi (DETERMINED/NEED_EXPERT_REVIEW/REJECTED), klarifikasi, error 500, daftar & detail riwayat, antrian review, 5 panel admin, endpoint tulis (approve/reject/parse/commit/decide/activate) — **tidak pernah** memanggil endpoint tulis backend asli |
| **DOM / computed style** | Semua item checklist K/C/R/H/E/A, layout di 1280/1050/720/390, overflow horizontal, sticky, auto-scroll, drawer, radius/ikon, fokus keyboard |
| **Backend asli** | **Tidak** diverifikasi oleh saya. Login, alur klasifikasi, aksi review, dan layar admin diuji manual oleh Anda dan dilaporkan aman |
| **Penilaian visual** | **Tidak ada** — penyedia vision 503 sepanjang sesi; saya tidak pernah melihat satu screenshot pun. Screenshot disediakan untuk Anda nilai |
| **Test otomatis** | Tidak ada test frontend di proyek ini (tidak ada script `test`) |

Catatan alat: Chromium menolak lebar jendela <660px, sehingga screenshot 390px diambil lewat iframe 390×900 (viewport iframe benar 390px, media query aktif). Transisi CSS tidak maju di tab background — saat mengukur drawer, transisi dimatikan lebih dulu agar nilai akhir terbaca.

---

## 7. Temuan terbuka (bukan bagian restyle)

Lihat `docs/TEMUAN.md`: **F1** notice sukses keputusan hilang karena remount; **F2** ambang alasan 5 karakter vs spesifikasi 10. Keduanya **tidak** diperbaiki.

Di luar lingkup dan tidak disentuh: `hs-classifier/.env.example` (perubahan port ke 5174 oleh Anda), artefak build `hs-classifier-api/`, `graphify-out/cache`.

---

## 8. File yang berubah terhadap `285020d`

`src/App.jsx`, `src/components/*` (7 file), `src/styles/design-system.css`, `src/index.css`, `src/App.css` (dihapus), `index.html`, `package.json` + `package-lock.json` (hanya `@fontsource/lexend` dan `lucide-react`), `docs/CHECKLIST_FUNGSI.md`, `docs/DESIGN.md`, `docs/TEMUAN.md`, `docs/RINGKASAN_RESTYLE.md`.

Tidak ada perubahan di `src/services/`, `src/data/`, `src/main.jsx`, `vite.config.js`, `eslint.config.js`.
