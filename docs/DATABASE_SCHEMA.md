# Database Schema — AI HS Classification Intelligence

**Database:** MySQL 8.0+  
**Referensi:** BTKI 2022 (PMK 26/2022)  
**Stack backend:** C# (.NET)

---

## Daftar Tabel

| Tabel | Fungsi | Fase |
|-------|--------|------|
| `users` | Autentikasi, role, plan | Fase 1 |
| `classification_cases` | Riwayat klasifikasi + hasil AI | Fase 1 |
| `daily_usage` | Rate limiting free tier | Fase 1 |
| `expert_reviews` | Workflow review & koreksi | Fase 2 |
| `audit_logs` | Tracking semua aksi user | Fase 2 |
| `btki_reference` | Master data BTKI 8-digit | Fase 3 |

---

## 1. Tabel `users`

```sql
CREATE TABLE users (
    id            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    name          VARCHAR(100)  NOT NULL,
    email         VARCHAR(150)  NOT NULL UNIQUE,
    password_hash VARCHAR(255)  NOT NULL,
    role          ENUM('admin','reviewer','user') NOT NULL DEFAULT 'user',
    plan          ENUM('free','starter','professional','enterprise') NOT NULL DEFAULT 'free',
    tenant_id     INT UNSIGNED  NULL,
    is_active     TINYINT(1)    NOT NULL DEFAULT 1,
    created_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login    DATETIME      NULL,
    updated_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

**Keterangan kolom:**

| Kolom | Tipe | Keterangan |
|-------|------|------------|
| `id` | INT UNSIGNED | Primary key auto increment |
| `name` | VARCHAR(100) | Nama lengkap user |
| `email` | VARCHAR(150) | Email unik, digunakan untuk login |
| `password_hash` | VARCHAR(255) | Hash bcrypt password |
| `role` | ENUM | `admin` = kelola sistem, `reviewer` = expert review, `user` = pengguna biasa |
| `plan` | ENUM | Tier langganan user |
| `tenant_id` | INT UNSIGNED | Untuk multi-tenant — NULL jika user individual |
| `is_active` | TINYINT(1) | 1 = aktif, 0 = nonaktif/banned |
| `last_login` | DATETIME | Waktu login terakhir |

---

## 2. Tabel `classification_cases`

```sql
CREATE TABLE classification_cases (
    id                      INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id                 INT UNSIGNED  NOT NULL,
    tenant_id               INT UNSIGNED  NULL,

    -- Input form (14 fields)
    nama_dagang             VARCHAR(255)  NULL,
    nama_generik            VARCHAR(255)  NOT NULL,
    material_utama          JSON          NULL,
    kondisi_impor           VARCHAR(100)  NULL,
    fungsi_utama            VARCHAR(150)  NULL,
    cara_kerja              JSON          NULL,
    mobilitas               VARCHAR(100)  NULL,
    area_operasi            VARCHAR(100)  NULL,
    sektor                  VARCHAR(100)  NULL,
    apakah_part             VARCHAR(20)   NULL,
    apakah_set              VARCHAR(20)   NULL,
    ada_komponen_tambahan   VARCHAR(50)   NULL,
    spesifikasi             TEXT          NULL,
    keterangan_tambahan     TEXT          NULL,

    -- Output AI
    hs_code                 VARCHAR(20)   NULL,
    uraian_btki             TEXT          NULL,
    confidence              ENUM('TINGGI','SEDANG','RENDAH') NULL,
    risk_notes              TEXT          NULL,
    reasoning_steps         JSON          NULL,
    kandidat_eliminasi      JSON          NULL,
    fakta_perlu_konfirmasi  JSON          NULL,
    perlu_expert_review     TINYINT(1)    NOT NULL DEFAULT 0,
    alasan_expert_review    TEXT          NULL,

    -- Metadata AI
    prompt_used             LONGTEXT      NULL,
    ai_model_used           VARCHAR(100)  NULL,

    -- Status workflow
    status                  ENUM('submitted','under_review','approved','rejected')
                            NOT NULL DEFAULT 'submitted',

    created_at              DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at              DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
                            ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(id),
    INDEX idx_user_id   (user_id),
    INDEX idx_status    (status),
    INDEX idx_hs_code   (hs_code),
    INDEX idx_created   (created_at)
);
```

**Keterangan kolom input form:**

| Kolom | Contoh nilai |
|-------|-------------|
| `material_utama` | `["Baja/Besi", "Aluminium"]` |
| `cara_kerja` | `["Diesel / Motor bakar", "Hidrolik"]` |
| `kondisi_impor` | `"Lengkap dan jadi (siap pakai)"` |
| `fungsi_utama` | `"Menggali/Earthmoving (excavator, bulldozer)"` |
| `mobilitas` | `"Self-propelled (bergerak sendiri)"` |
| `area_operasi` | `"Off-highway / Perkebunan / Pertambangan"` |

**Keterangan kolom output AI:**

| Kolom | Contoh nilai |
|-------|-------------|
| `hs_code` | `"8429.52.00"` |
| `confidence` | `"TINGGI"` |
| `reasoning_steps` | `[{"rule":"KUM HS 1","penjelasan":"..."},{"rule":"KUM HS 6","penjelasan":"..."}]` |
| `kandidat_eliminasi` | `[{"kode":"8429.59","alasan":"..."}]` |
| `fakta_perlu_konfirmasi` | `["Konfirmasi berat unit tidak melebihi 10 kg"]` |
| `ai_model_used` | `"openai/gpt-oss-120b"` |

---

## 3. Tabel `daily_usage`

```sql
CREATE TABLE daily_usage (
    id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id     INT UNSIGNED NOT NULL,
    usage_date  DATE         NOT NULL,
    req_count   INT UNSIGNED NOT NULL DEFAULT 0,

    UNIQUE KEY  uk_user_date (user_id, usage_date),
    FOREIGN KEY (user_id) REFERENCES users(id)
);
```

**Fungsi:** Tracking jumlah request per user per hari untuk rate limiting free tier.

Logika C# backend:
```csharp
var today = DateTime.Today;
var usage = await _db.DailyUsage
    .FirstOrDefaultAsync(u => u.UserId == userId && u.UsageDate == today);

if (user.Plan == "free" && (usage?.ReqCount ?? 0) >= 10)
    return StatusCode(429, "Batas harian free tier tercapai (10 request/hari).");

// Increment
if (usage == null)
    _db.DailyUsage.Add(new DailyUsage { UserId = userId, UsageDate = today, ReqCount = 1 });
else
    usage.ReqCount++;

await _db.SaveChangesAsync();
```

---

## 4. Tabel `expert_reviews`

```sql
CREATE TABLE expert_reviews (
    id              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    case_id         INT UNSIGNED NOT NULL,
    reviewer_id     INT UNSIGNED NOT NULL,
    original_hs     VARCHAR(20)  NOT NULL,
    corrected_hs    VARCHAR(20)  NULL,
    is_correct      TINYINT(1)   NULL,
    catatan         TEXT         NULL,
    status          ENUM('approved','rejected','modified') NOT NULL,
    reviewed_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (case_id)     REFERENCES classification_cases(id),
    FOREIGN KEY (reviewer_id) REFERENCES users(id),
    INDEX idx_case     (case_id),
    INDEX idx_reviewer (reviewer_id)
);
```

**Keterangan kolom:**

| Kolom | Keterangan |
|-------|------------|
| `original_hs` | Kode HS yang direkomendasikan AI |
| `corrected_hs` | Kode HS koreksi reviewer — NULL jika disetujui tanpa koreksi |
| `is_correct` | 1 = AI benar, 0 = AI salah, NULL = belum ditentukan |
| `status` | `approved` = AI benar, `modified` = AI salah dan dikoreksi, `rejected` = butuh analisis ulang |

---

## 5. Tabel `audit_logs`

```sql
CREATE TABLE audit_logs (
    id          BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id     INT UNSIGNED    NULL,
    case_id     INT UNSIGNED    NULL,
    action      VARCHAR(50)     NOT NULL,
    detail      JSON            NULL,
    ip_address  VARCHAR(45)     NULL,
    user_agent  VARCHAR(500)    NULL,
    created_at  DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_user    (user_id),
    INDEX idx_case    (case_id),
    INDEX idx_action  (action),
    INDEX idx_created (created_at)
);
```

**Nilai `action` yang digunakan:**

| Action | Kapan dicatat |
|--------|--------------|
| `login` | User berhasil login |
| `classify` | User submit klasifikasi |
| `review` | Reviewer buka case untuk review |
| `approve` | Reviewer approve hasil AI |
| `modify` | Reviewer koreksi kode HS |
| `reject` | Reviewer reject case |
| `export` | User export hasil ke PDF/Excel |
| `register` | User baru daftar |

---

## 6. Tabel `btki_reference`

```sql
CREATE TABLE btki_reference (
    id              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    hs8             VARCHAR(15)  NOT NULL UNIQUE,
    hs6             VARCHAR(10)  NOT NULL,
    hs4             VARCHAR(6)   NOT NULL,
    bab             VARCHAR(5)   NOT NULL,
    seksi           VARCHAR(5)   NOT NULL,
    uraian_id       TEXT         NOT NULL,
    uraian_en       TEXT         NULL,
    catatan_penting TEXT         NULL,
    btki_version    VARCHAR(10)  NOT NULL DEFAULT '2022',
    is_active       TINYINT(1)   NOT NULL DEFAULT 1,
    created_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ON UPDATE CURRENT_TIMESTAMP,

    INDEX idx_hs4    (hs4),
    INDEX idx_hs6    (hs6),
    INDEX idx_bab    (bab),
    INDEX idx_seksi  (seksi)
);
```

**Contoh data:**

| hs8 | hs6 | hs4 | bab | uraian_id |
|-----|-----|-----|-----|-----------|
| 8429.52.00 | 842952 | 8429 | 84 | Excavator hydraulik dengan superstruktur berputar 360 derajat |
| 8471.30.20 | 847130 | 8471 | 84 | Laptop termasuk notebook dengan keyboard fisik terintegrasi |
| 8704.10.31 | 870410 | 8704 | 87 | Dumper off-highway selain CKD GVW tidak melebihi 5 ton |

**Catatan:** Tabel ini akan menggantikan file `btki_national.json` di Fase 3. Backend C# akan query tabel ini berdasarkan bab/heading yang relevan dengan input user, lalu inject ke prompt secara dinamis — lebih efisien dari inject semua sekaligus.

---

## Urutan Pembuatan (Fase 1 dulu)

```sql
-- Fase 1: jalankan dalam urutan ini
CREATE TABLE users (...);
CREATE TABLE classification_cases (...);
CREATE TABLE daily_usage (...);

-- Fase 2
CREATE TABLE expert_reviews (...);
CREATE TABLE audit_logs (...);

-- Fase 3
CREATE TABLE btki_reference (...);
```

---

## Relasi Antar Tabel

```
users (1) ──────────────── (N) classification_cases
users (1) ──────────────── (N) daily_usage
users (1) ──────────────── (N) audit_logs
classification_cases (1) ── (N) expert_reviews
classification_cases (1) ── (N) audit_logs
```
