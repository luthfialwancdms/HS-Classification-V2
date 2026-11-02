# DESIGN.md — Sistem desain v0 → hs-classifier

Sumber acuan: `ui-design-for-hs-classification-intelligence/` (ekspor v0, Next.js 16 + Tailwind v4 + shadcn base-nova).
Diekstrak: 29 Okt 2026, Langkah 2. Semua nilai di bawah **disalin apa adanya** dari kode acuan; yang bukan salinan ditandai `INTERPRETASI`.

Prinsip: v0 **bukan** aplikasi Tailwind-utility. Ia memakai stylesheet CSS biasa dengan variabel `:root` + nama kelas komponen (`.panel`, `.badge`, `.primary-button`, …). Karena itu penerapan yang paling aman dan paling mirip adalah **memindahkan stylesheet-nya**, bukan menuliskan ulang tampilan itu sebagai utility Tailwind.

---

## 1. Sumber nilai (dari kode acuan)

| Berkas acuan | Yang diambil |
|---|---|
| `app/globals.css` (13 baris, panjang) | seluruh token `:root`, reset, ±120 aturan kelas, 2 media query |
| `components/classification-dashboard.tsx` | pemakaian kelas per layar, struktur shell (topbar + sidebar + view), ikon lucide |
| `components/ui/button.tsx` | hanya referensi shadcn; **tidak dipakai** (butuh cva + base-ui + cn) |
| `app/layout.tsx` | `lang="id"`, judul, antialiased |
| `app/globals.css` baris 1 | font: Google Fonts `Lexend` wght **400;500;600;700;800** |

---

## 2. Palet

Nilai persis dari `:root` acuan:

| Token | Nilai | Dipakai untuk |
|---|---|---|
| `--background` | `#f5f8f7` | latar halaman |
| `--foreground` | `#18302d` | teks utama |
| `--card` | `#ffffff` | panel, kartu, topbar, sidebar |
| `--muted` | `#6d817d` | teks sekunder, label kecil, deskripsi |
| `--line` | `#dce8e4` | semua border 1px, pemisah |
| `--primary` | `#1d8175` | aksi utama, tautan, state aktif |
| `--primary-dark` | `#14665e` | hover tombol utama |
| `--navy` | `#183d3a` | kode HS, brand mark, judul tebal |
| `--green` | `#1e8a61` | sukses / confidence TINGGI / disetujui |
| `--green-bg` | `#e8f7ef` | latar badge sukses, blok validasi BTKI |
| `--amber` | `#b66a16` | peringatan / confidence SEDANG / pending |
| `--amber-bg` | `#fff4dc` | latar notice & badge peringatan |
| `--red` | `#c9464d` | error / confidence RENDAH / ditolak / urgent |
| `--red-bg` | `#fff0f1` | latar badge & tombol bahaya |
| `--blue-bg` | `#e9f4f3` | latar aksen tenang (chip terpilih, ikon state info) |
| `--coral` | `#e9775c` | avatar |
| `--radius` | `16px` | radius panel/kartu besar |

Warna netral tambahan yang dipakai lewat hex langsung di acuan (bukan token):
`#536070` (label form), `#667085` (nav/ikon), `#98a2b3` (eyebrow, meta), `#f0f2f5` (pemisah dalam panel), `#dfe4ea` (border input), `#f2f4f7` (ikon statistik), `#475467` (teks tegas dalam empty state).

> **Diperbarui (restyle/login-v0):** pengguna memutuskan tema **ungu** (`--primary` `#6600cc`, acuan v0) berlaku di seluruh aplikasi. Kolom "teal" di tabel bawah kini berarti turunan ungu; peta nilai terbaru ada di `docs/RINGKASAN_RESTYLE.md` §10. Label INTERPRETASI untuk layar login ("acuan v0 tidak punya layar login; kartu panel di tengah") **dihapus**: login kini disalin dari `app/login/page.tsx` v0. Yang masih INTERPRETASI di login hanya disclaimer DJBC dan fokus keyboard `.login-submit`/`.login-provider-button` (ditandai di CSS).

### 2.1 Sisa palet indigo di acuan — KEPUTUSAN

Acuan aslinya bertema indigo, lalu di-*re-theme* ke teal hanya sebagian. Nilai indigo berikut masih tertinggal dan **bertabrakan** dengan `--primary` teal:

| Nilai tertinggal | Terpakai di | Usul pengganti (teal) |
|---|---|---|
| `#eef2ff` | nav aktif, workspace-icon, empty-icon, chip `code`, version-badge | `var(--blue-bg)` `#e9f4f3` |
| `#c9d5ff` | border badge BETA | `#c6e2dc` |
| `#9eb1ff` | border input saat fokus | `#7fb8ae` |
| `#315cf6` | shadow tombol utama, ring fokus | `#1d8175` (alpha sama) |
| `#dfe6ff` | border result-hero | `#d3e9e4` |
| `#f1f5ff` → `#f8faff` | gradien result-hero | `#f1f8f6` → `#f8fcfb` |
| `#f8faff` | latar detail-code, kandidat terpilih | `#f8fcfb` |
| `#6172b6` | label kecil result-hero | `#3f7d73` |
| `#59677f` | teks paragraf result-hero | `#4d5f5b` |
| `#bccaff` | border case-card hover/terpilih | `#a9d3cb` |
| `#aebdff` | border chip/kandidat terpilih | `#96c8bf` |
| `#d6def8` / `#f3f6ff` | border/latar step-number | `#d3e6e1` / `#f2f9f7` |

Rekomendasi: **normalisasi ke teal** (tabel di atas). Identitas visual tetap (sama layout, ukuran, radius, kontras), hanya hue aksen dibuat konsisten dengan `--primary`.

### 2.2 Kontras terukur (WCAG 2.1, dihitung dari nilai acuan)

| Pasangan | Rasio | Verdict |
|---|---|---|
| `--foreground` di `--background` | **13.11:1** | AA |
| `--foreground` di `--card` | **14.00:1** | AA |
| `--navy` di `--card` | **11.88:1** | AA |
| `#536070` (label) di `--card` | **6.41:1** | AA |
| `--primary` di `--card` | **4.72:1** | AA |
| putih di `--primary` | **4.72:1** | AA |
| `--muted` di `--card` | **4.13:1** | hanya AA-large ❌ untuk teks 10–11px |
| `--muted` di `--background` | **3.86:1** | ❌ |
| `#98a2b3` di `--card` | **2.58:1** | ❌ gagal |
| `--green` di `--green-bg` | **3.90:1** | ❌ untuk badge 9px bold |
| `--amber` di `--amber-bg` | **3.80:1** | ❌ |
| `--red` di `--red-bg` | **4.25:1** | ❌ |
| `--green` di `--card` (dot/ikon) | grafis dekoratif | n/a |

**KEPUTUSAN:** acuan memakai ukuran teks 8–11px, jadi beberapa pasangan di atas gagal AA untuk teks normal. Usul penyimpangan minimal (hanya untuk **teks**, bukan border/ikon/permukaan), dengan hue tetap:

| Penggunaan | Asli | Usul | Rasio baru |
|---|---|---|---|
| teks sekunder `--muted` | `#6d817d` | `#637572` | **4.55:1** |
| eyebrow/meta yang kini `#98a2b3` | `#98a2b3` | `--muted` `#637572` (warnanya ikut token) | **4.55:1** |
| teks badge sukses | `#1e8a61` | `#1b7e58` | **4.55:1** |
| teks badge peringatan | `#b66a16` | `#a45f14` | **4.55:1** |
| teks badge bahaya | `#c9464d` | `#c1434a` | **4.55:1** |
| teks aksen di `--blue-bg` | `#1d8175` | `#1c7b6f` | **4.55:1** |

Perubahan ini tidak mengubah tata letak, radius, atau hierarki — hanya menggelapkan teks 4–27%. Jika ditolak, acuan dipakai apa adanya dan kegagalan kontras dicatat sebagai utang aksesibilitas.

---

## 3. Tipografi

| Aspek | Nilai acuan |
|---|---|
| Familia | `'Lexend', ui-sans-serif, system-ui, sans-serif` |
| Bobot diunduh | 400, 500, 600, 700, 800 |
| Bobot yang dipakai di CSS | 400, 600, 700, 750, 800 |
| `body` | `font-size: 13px` |
| Kode HS / angka teknis | `monospace` (`ui-monospace`) |
| `h1` halaman | `24px`, `letter-spacing: -0.7px` |
| `h2` panel | `16px`, `letter-spacing: -0.2px` |
| Judul detail | `17px` / `14px` |
| Teks isi | `11px`, line-height 1.5–1.6 |
| Label field / nav | `11px`, weight 700 |
| Meta / chip / badge | `9–10px` |
| Eyebrow (`.eyebrow`, `.nav-label`, `.section-label`) | `9px`, weight 800, `letter-spacing: 1.1px`, UPPERCASE |
| Kode HS besar (`.hs-code`) | `31px`, weight 800, `letter-spacing: -1px` |
| Kode HS di kartu (`code`) | `10–21px`, monospace |

Catatan: `font-weight: 750` dipakai di `.brand-name`. Paket statis hanya punya kelipatan 100 → **dipetakan ke 800** (selisih tak terlihat pada 15px). `INTERPRETASI`

Adaptasi font: `@import url('https://fonts.googleapis.com/…Lexend…')` **dibuang**, diganti `@fontsource/lexend@5.3.0` (terverifikasi ada di npm, tanpa dependency transitif, menyediakan `latin-400.css` … `latin-800.css`). Impor 5 berkas weight latin saja.

---

## 4. Radius, bayangan, border

| Elemen | Nilai |
|---|---|
| Panel / kartu besar / admin-content / queue | `16px` (`--radius`) |
| Kartu sedang (stat-card, version-badge, result-hero, detail-code) | `8–9px` |
| Tombol, input, chip, case-card, field | `6–8px` |
| Badge / pill | `100px` (pill penuh) |
| Badge skala kecil (`.state`, `.ai-pill`) | `4px` |
| Border default | `1px solid var(--line)` — **kartu tidak memakai bayangan** |
| Bayangan tombol utama | `0 2px 4px <primary>26` |
| Bayangan modal | `0 24px 70px rgba(15,28,45,.22)` |
| Bayangan sidebar mobile | `8px 0 24px #17203318` |
| Ring fokus input | `0 0 0 3px <primary>14` + border `#7fb8ae` (hasil normalisasi) |

Perbedaan besar dari kondisi sekarang: aplikasi kita memakai `shadow-sm` di hampir semua kartu. v0 **flat**: border tipis, tanpa bayangan.

---

## 5. Spacing & layout

| Aspek | Nilai acuan |
|---|---|
| Topbar | tinggi `68px`, padding `0 32px`, sticky, border-bottom |
| Sidebar | lebar `240px`, padding `17px 12px`, border-right |
| Area view | `max-width: 1480px`, padding `31px 38px 56px` |
| Panel | padding `22px` |
| Jarak antar panel | `18px` (`classify-grid`), `14px` (riwayat), `16px` (review), `15px` (admin) |
| Grid form | 2 kolom, `gap: 11px` |
| Field | `gap: 6px`, `margin-bottom: 12px` |
| Grid statistik | 4 kolom, `gap: 11px` |
| Grid tarif | 4 kolom, `gap: 8px` |
| Section label | `margin: 20px 0 12px`, `padding-top: 16px`, border-top `#f0f2f5` |
| Page title | `margin-bottom: 26px` |
| Breakpoint | **1050px** (sidebar 205px, layout 1 kolom) dan **720px** (sidebar off-canvas, form 1 kolom) |

Struktur shell acuan:

```
.app-shell
├─ header.topbar          (mobile-menu, brand-mark, brand-name+brand-sub, .beta, top-actions)
└─ .body-shell
   ├─ aside.sidebar       (workspace, nav-label, nav-item*, version-card, sidebar-user)
   └─ main.main-content > .view   (page-title, lalu konten layar)
```

---

## 6. Aturan komponen (inventaris kelas acuan)

**Tombol** — tidak memakai shadcn. Varian yang ada:
- `.primary-button` — latar `--primary`, teks putih, radius 7px, padding `10px 14px`, 11px/700, shadow primary.
- `.outline-button` — latar putih, border `--line`, teks `#536070`, hover border `#bec8d5` + bg `#f9fafb`.
- `.ghost-button` — tanpa border/latar; dipakai untuk "Reset".
- `.danger-button` — latar `--red-bg`, teks `--red`, border `#ffd0d5`.
- `.link-button` — teks `--primary`, 11px/700.
- `.icon-button` — 7px padding, radius 8px, hover `#f2f4f7`.
- `.filter-button` — seperti outline, tinggi 35px.
- State: `:hover` per varian; **acuan tidak punya style `:disabled`** → tambahkan `opacity:.5; cursor:not-allowed` agar kondisi disabled (yang wajib tetap ada di aplikasi kita) tetap terlihat. `INTERPRETASI`

**Form**: `.field` (label di atas, span 11px/700 `#536070`, `em` wajib = `--red`), input/select/textarea 11px, border `#dfe4ea`, radius 6px, fokus ring teal. Select dibungkus `.select-wrap` + ikon chevron absolut.

**Chip multi-select**: `.chips` (flex wrap gap 6px) + `.chip` (border `#dfe4ea`, 10px) + `.chip.selected` (latar aksen, border aksen, weight 700, ikon centang).

**Radio grid**: `.radio-grid` 3 kolom; tiap pertanyaan `fieldset` + `legend`; `accent-color: var(--primary)`.

**Badge**: `.badge` + tone `.success` / `.warning` / `.danger`, selalu dengan `.dot` 5px. Tidak ada varian netral di acuan → tambahkan `.badge.neutral` (`#f2f4f7` / `--muted`). `INTERPRETASI`

**Notice**: `.notice` — latar `--amber-bg`, teks `#855313`, border `#f5dfad`, 10px, ikon di kiri.

**Empty state**: `.empty-state` (min-height 610px, tengah, ikon `.empty-icon` bulat 64px) + `.empty-hint` (border dashed).

**Hasil analisis**: `.result-hero` (gradien + border, kiri label/kode/deskripsi, kanan badge) → `.result-meta` (baris meta) → `.tariff-grid`/`.tariff-card` → `.notice` → `.reasoning`/`.reason-step` (+ `.highlight` untuk langkah terakhir) → `.result-actions`.

**Riwayat**: `.toolbar` + `.search` + `.master-detail` (330px + 1fr) → `.case-list`/`.case-card` (border-kiri 3px sebagai penanda terpilih) → `.detail-card` (`.detail-header`, `.tabs`/`.tab`, `.detail-result` 210px + 1fr, `.timeline`).

**Review**: `.review-layout` (1.15fr / .85fr) → `.queue`/`.review-card` (+ `.urgent`) → `.expert-panel` (`.candidate`, `.or-divider`, `.inline-input`, `.btki-check`).

**Admin**: `.admin-layout` (210px + 1fr) → `.admin-nav` (tombol vertikal, `.active`, badge angka) → `.admin-content` (`.content-heading`, `.version-list`/`.version-row`, `.state.aktif|inactive|validating`, `.admin-placeholder`).

**Tabel**: acuan hampir tidak memakai tabel (memakai baris kartu). Aplikasi kita punya 6 tabel data nyata → pertahankan struktur tabel, beri gaya v0: header 9px uppercase `--muted`, baris `border-top: 1px solid #f0f2f5`, sel 10–11px, kode monospace. `INTERPRETASI`

---

## 7. Pemetaan semantik domain aplikasi kita

| Kondisi aplikasi | Kelas v0 |
|---|---|
| Confidence `TINGGI` | `.badge.success` |
| Confidence `SEDANG` | `.badge.warning` |
| Confidence `RENDAH` | `.badge.danger` |
| Status `Determined` / "Ditentukan" | `.badge.success` |
| Status `NeedClarification` / "Perlu klarifikasi" | `.badge.warning` + `.notice` untuk banner |
| Status `NeedExpertReview` / "Review ahli" | `.badge.danger` |
| Status `Rejected` / "Ditolak ahli" | `.badge.neutral` |
| Koreksi `EXPERT_APPROVED` | `.badge.success` |
| Koreksi `EXPERT_OVERRIDE` | `.badge` aksen (`--primary` ungu, `--blue-bg`) |
| Reasoning `RETAINED` / `ELIMINATED` / `UNRESOLVED` | `.badge.success` / `.danger` / `.warning` |
| Risk flag | `.chip` + tone `--amber-bg` |
| Versi `ACTIVE` | `.state.aktif` |
| Versi `INDEXED` / `COMMITTED` | `.state.info` (baru: `--blue-bg` + `--primary`) `INTERPRETASI` |
| Versi `DRYRUNFAILED` | `.state.danger` (baru: `--red-bg` + `--red`) `INTERPRETASI` |
| Error API | `.notice` versi bahaya (border `#ffd0d5`, latar `--red-bg`, teks `--red`) `INTERPRETASI` |

---

## 8. Adaptasi Next.js → Vite (tanpa mengubah tampilan)

| Acuan | Pengganti |
|---|---|
| `@import url(google fonts)` | `@fontsource/lexend` (5 berkas weight, dibundel Vite, offline) |
| `@import 'tw-animate-css'`, `@import 'shadcn/tailwind.css'` | dibuang — tidak ada komponen shadcn |
| `@custom-variant dark` | dibuang — acuan tidak punya blok `.dark`, jadi tidak ada tema gelap |
| `'use client'` | dibuang |
| `next/link`, `next/image` | tidak dipakai di acuan |
| `cn()` (clsx + tailwind-merge) | template string / `[a, b && c].filter(Boolean).join(' ')` — tanpa dependency |
| `lucide-react` (30+ ikon) | **menunggu keputusan** (lihat §10 Q2) |
| `next/font` | tidak dipakai (acuan memakai `@import` Google) |
| `components/ui/button.tsx` (cva + base-ui) | tidak dipakai; tombol dibuat dari kelas CSS v0 |

---

## 9. Rencana penerapan (paling aman, bertahap)

**P0 — Fondasi (tanpa mengubah layar)**
1. `npm install @fontsource/lexend@5.3.0` (dependency baru, disetujui).
2. Berkas baru `src/styles/design-system.css`: salinan token + kelas dari `globals.css` acuan, minus impor shadcn/tw-animate/dark/Google Fonts, plus 3 tambahan kecil yang didokumentasikan (`.badge.neutral`, `.state.info|danger`, varian error `.notice`).
3. `src/index.css`: `@import "tailwindcss";` lalu `@import "./styles/design-system.css";`.
4. Impor 5 weight font. Dicoba lewat `@import "@fontsource/…"` di CSS; jika Vite menolak, pindah ke 5 baris impor di `main.jsx` (disclosed).
5. Verifikasi: `lint` + `build` + dev server, screenshot login (belum direstyle) — hanya font & token yang boleh berubah.
6. Commit: `restyle: fondasi tokens + font Lexend`.

**P1 — Shell, header, login** (G1–G11): topbar + sidebar + view, form login, footer. Commit `restyle: shell, header, login`.

**P2 — Klasifikasi + semua state hasil** (K1–K7, C1–C6, R1–R13). Commit `restyle: layar klasifikasi`.

**P3 — Riwayat** (H1–H7). **P4 — Review Ahli** (E1–E6). **P5 — Admin** (A1–A12). Masing-masing satu commit + stop.

**P6 — Responsif 1050/720 + polish**: keyboard, label, kontras, state disabled/hover/focus.

Aturan tetap: tidak ada hex/font di luar `design-system.css`; semua warna lewat `var(--…)`; file `services/`, `data/`, `main.jsx`, `vite.config.js`, `package.json` (kecuali penambahan dependency font) tidak berubah.

---

## 10. Keputusan yang dibutuhkan sebelum P0

**Q1 — Sisa indigo:** normalisasi ke teal (§2.1) atau salin persis termasuk indigo?

**Q2 — Ikon:** acuan memakai `lucide-react`. Pilihan: (a) pasang `lucide-react` (1 dependency, ±1 KB per ikon, hasil identik), (b) pertahankan emoji (tampilan jauh dari acuan), (c) salin SVG manual (±30 ikon, tanpa dependency, kode membengkak). Rekomendasi: (a).

**Q3 — Shell:** acuan memakai topbar + sidebar 240px; aplikasi kita sekarang top navbar. Mengadopsi sidebar = perubahan struktur layout (diizinkan), dan nav berbasis role dipindah ke sidebar. Setuju?

**Q4 — Hasil analisis:** di acuan hasil muncul sebagai **modal** setelah klik Analisis. Aplikasi kita menampilkan panel hasil permanen di kolom kanan (dan banner klarifikasi di atas). Rekomendasi: **tetap panel inline**, hanya gaya visualnya diambil dari acuan. Modal akan mengubah alur interaksi (perlu tutup/buka, menyembunyikan state klarifikasi).

**Q5 — Blok statistik:** acuan punya `stats-grid` di Riwayat/Review/Admin. API kita tidak menyediakan agregat (hanya `total` pada respons daftar). Rekomendasi: tampilkan hanya metrik yang berasal dari data yang sudah ada (`total case`), sisanya dilewati — bukan angka karangan.

**Q6 — Skala teks & kontras:** terapkan penyesuaian kontras §2.2, atau salin persis nilai acuan?

**Q7 — Tab di detail riwayat:** acuan memakai tab (Hasil / Fakta / Reasoning / Tarif / Keputusan Ahli). Aplikasi kita menampilkan semua seksi bertumpuk. Rekomendasi: **tanpa tab** (konten tidak tersembunyi, perilaku tidak berubah), tiap seksi jadi kartu bergaya v0.

**Q8 — Kontrol tanpa fungsi di acuan:** "Pengaturan", pemilih workspace "PT Nusantara Logistik", "Export PDF/Excel", "Panduan", "Browse BTKI", jumlah notifikasi, badge "12" di Review Ahli. Semua itu tidak punya data/endpoint di aplikasi kita. Rekomendasi: **tidak dibuat** (jangan menambah tombol mati).
