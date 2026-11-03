# Database MySQL (UAT)

Aplikasi mendukung dua mesin database. Bawaannya SQL Server (tidak ada yang berubah untuk pengembangan lokal). MySQL dipilih lewat konfigurasi.

## Memakai MySQL

Simpan pengaturan di user-secrets, bukan di file repo (jalankan dari folder `hs-classifier-api`):

```
dotnet user-secrets set "Database:Provider" "MySql" --project HsClassifier.API
dotnet user-secrets set "Database:MySqlVersion" "8.4.11" --project HsClassifier.API
dotnet user-secrets set "ConnectionStrings:MySql" "Server=<host>;Port=3306;Database=dbhscode_uat;User ID=<user>;Password=<password>;SslMode=Required" --project HsClassifier.API
```

Kembali ke SQL Server: hapus `Database:Provider` (`dotnet user-secrets remove "Database:Provider" --project HsClassifier.API`). Database di MySQL tidak berubah.

`Database:MySqlVersion` bersifat opsional. Tanpa itu, aplikasi membuka satu koneksi untuk membaca versi server lalu menyimpannya di memori.

## Skema

`001_schema.sql` dibuat dari model EF dengan `dotnet ef dbcontext script` (Database__Provider=MySql). Isinya hanya `CREATE TABLE` dan `CREATE INDEX` (17 tabel, 27 indeks tambahan, 10 relasi). Jalankan sekali di database kosong. Dijalankan lagi, pernyataan pertama gagal dan tidak ada yang berubah.

Migrasi EF di `HsClassifier.Infrastructure/Migrations` khusus SQL Server dan tidak dipakai untuk MySQL. Bila model berubah, buat skrip MySQL berikutnya dari perubahan itu.
`002_ai_tariff_estimate.sql` menambah dua tabel (`AppSettings`, `AiTariffEstimates`) untuk estimasi AI atas PPN, PPh 22 dan skema FTA. Jalankan sekali setelah `001_schema.sql`. Tanpa dua tabel ini aplikasi di MySQL tetap jalan, tetapi saklar estimasi AI di menu Admin tidak bisa dibaca atau disimpan.

## Riwayat

2 Nov 2026, `dbhscode_uat` (MySQL 8.4.11, Lightsail ap-southeast-1):
- Database kosong sebelum skema dibuat. Akun `uat_developer` hanya punya hak atas `dbhscode_uat`.
- Skema dibuat dari `001_schema.sql` (17 tabel, 44 indeks termasuk primary key, 10 relasi).
- Data disalin 1:1 dari SQL Server lokal ke 17 tabel dengan ID yang sama (sekitar 107 ribu baris: 49.140 baris tarif, 32.421 tarif MFN, 22.695 paragraf EN, 261 kasus, 1.921 langkah reasoning, 3 versi, 3 pengguna, dan lainnya).
- Perbandingan nilai per nilai pada setiap kolom di semua tabel: tidak ada selisih.
- 100 kasus uji (`hs-classifier-api/docs/golden/export_import_100.json`, tanpa AI) menghasilkan jawaban yang sama persis di MySQL dan SQL Server.

## Catatan

- `sql_mode` server UAT tidak strict (`NO_ENGINE_SUBSTITUTION`): teks yang kepanjangan dipotong diam-diam, bukan ditolak. Kolom dibatasi panjangnya oleh model; salinan pertama diperiksa sampai tidak ada nilai yang terpotong.
- Nama tabel peka huruf besar-kecil di server ini (`lower_case_table_names=0`). Skrip dan aplikasi memakai ejaan yang sama (`BtkiTariffLines`).
- Latensi: menjalankan 100 kasus dari komputer lokal ke UAT di Singapura memakan sekitar 324 detik, dibanding sekitar 101 detik ke SQL Server lokal. Selisihnya waktu tempuh jaringan per query, bukan indeks. Menjalankan API di AWS wilayah yang sama akan menghilangkan sebagian besar selisih itu.
- Akun `uat_developer` adalah akun admin. Setelah tahap ini, aplikasi sebaiknya memakai akun dengan hak terbatas (SELECT, INSERT, UPDATE, DELETE pada `dbhscode_uat`) dan password akun admin diganti bila pernah tertempel di tempat lain.
- Alat penyalin yang dipakai (SQL Server ke MySQL, hanya ke tabel kosong, dengan perbandingan sesudahnya) masih berupa alat sekali pakai di luar repo.