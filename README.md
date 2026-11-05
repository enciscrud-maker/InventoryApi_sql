# Inventory API (.NET 8)

REST API inventory: ASP.NET Core 8, EF Core 8 (Code First, mapping eksplisit ke ERD), SQL Server, JWT, FluentValidation, Swagger, ClosedXML.

```
src/
  Inventory.Domain          entity (MD_*, TR_*, CFG_*), enum, konstanta role/permission
  Inventory.Application     DTO, validator, service (bisnis rules), abstraksi
  Inventory.Infrastructure  AppDbContext, IEntityTypeConfiguration (ToTable/HasColumnName eksplisit), seeder, JWT, file storage
  Inventory.Api             controller, middleware (ProblemDetails), Program.cs, Swagger
tests/Inventory.Tests       integration test (xUnit + Testcontainers SQL Server, butuh Docker)
```

## Menjalankan
```bash
docker compose up -d                       # SQL Server dev
cd src/Inventory.Api
dotnet run                                 # Development: seed sample + admin
# Swagger: https://localhost:<port>/swagger
# Login admin (Development): admin@inventory.local / NIK 0000000001, password Admin#12345
```

### Migration (WAJIB dibuat sekali)
Kode ini ditulis tanpa SDK di mesin penulis, jadi folder Migrations belum ada. Selama belum ada migration, startup memakai `EnsureCreated`. Untuk migration sungguhan:
```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate -p src/Inventory.Infrastructure -s src/Inventory.Api -o Persistence/Migrations
dotnet ef database update -p src/Inventory.Infrastructure -s src/Inventory.Api
```
Setelah ada migration, startup otomatis memanggil `Migrate()`.
Perubahan skema tidak diterapkan ke database lama oleh `EnsureCreated`; buat dan jalankan migration sebelum memakai database yang sudah ada.

Untuk menambahkan antrean permintaan reset password ke database yang sudah berjalan, jalankan `src/Inventory.Infrastructure/Persistence/UpgradePasswordResetRequests.sql` satu kali pada database Inventory API sebelum memperbarui aplikasi. Script ini idempotent dan hanya membuat tabel, relasi, serta index baru.

Untuk database legacy sebelum pemisahan master business unit/status aset, hentikan API, buat dan verifikasi backup database, lalu jalankan `src/Inventory.Infrastructure/Persistence/UpgradeLegacySchema.sql` terhadap database API yang sama. Script ini bersifat transaksional dan akan berhenti tanpa perubahan jika tabel aset, lokasi, goods, atau stock opname sudah berisi data legacy yang perlu dipetakan manual. Kolom/tabel lama yang tidak lagi dipakai tetap dipertahankan. Script ini juga menambahkan `ActualLocationId` untuk lokasi aktual tiap item stock opname dan kolom `MD_User.IsMainAdmin` secara aditif.

Saat startup, akun dengan NIK `0000000001` ditetapkan sebagai satu-satunya Main Admin dan diberi role Admin. Autentikasi juga mengenali NIK tersebut sebagai Main Admin jika flag database belum tersinkron; akun itu mendapat seluruh izin API dan akses halaman. Jika sebelumnya ada Main Admin lain, penetapan dipindahkan ke akun tersebut. Jika akun dengan NIK tersebut belum ada, seeder mempertahankan fallback sebelumnya: akun ber-role Admin dengan `UserId` terkecil menjadi Main Admin bila belum ada yang ditetapkan. Pastikan menjalankan skrip upgrade sebelum memakai build ini pada database lama.

### Konfigurasi produksi
- `Jwt__Key` (>= 32 karakter, WAJIB diganti), `ConnectionStrings__DefaultConnection`.
- Docker Compose memakai database `InventoryApiDocker` secara default (`DATABASE_NAME` untuk mengubahnya), agar tidak memakai database lama dengan skema yang tidak cocok.
- `Seed__AdminPassword` mengisi admin awal; kosong = admin tidak dibuat. `Seed__SampleData=false`.
- `Cors:Origins` untuk domain frontend Next.js, `Storage:RootPath` untuk lampiran.

## Test
`dotnet test` (Docker harus jalan; test memakai container SQL Server asli karena butuh rowversion, transaksi, dan filtered index).
Cakupan: login/refresh, RBAC (401/403), validasi hierarki, import CSV/XLSX, stok in/out/disposal, over-issuance, submit duplikat, concurrency, status history.

## Keputusan & asumsi (spesifikasi ambigu)
1. **MD_Model.AssetId nullable** (agar master model bisa dibuat tanpa aset). Ubah jadi required jika ERD memang mewajibkan.
2. **Refresh token stateless** (JWT bertanda tangan, single-use) + daftar revokasi **in-memory** untuk logout. Tidak ada tabel baru. Revokasi hilang saat restart dan tidak dibagi antar-instance; untuk multi-instance gunakan cache terdistribusi.
3. **Status TR_Good** boleh berpindah dua arah (InProgress <-> Completed); setiap perubahan tercatat di history. Header/item tidak punya endpoint edit (read-only).
4. **Header TR_Good** (AssetId, Cat/Brand/Group/Type) diturunkan dari aset item pertama, bukan dari client.
5. **Stock opname tidak mengubah stok**; selisih dicatat sebagai temuan (`Finding`). `SystemQuantity` diambil server saat item ditambahkan. `PUT items` mengganti seluruh daftar item. Status: Draft, InProgress, Completed (Completed = read-only).
6. **PUT /api/assets tidak mengubah Quantity**; stok hanya berubah lewat Goods In/Out. Kirim `rowVersion` untuk optimistic concurrency.
7. **Nomor dokumen**: `GI-yyyyMMdd-0001`, `GO-...`, `SO-...`; penomoran diserialkan dengan `sp_getapplock` di dalam transaksi.
8. **Disposal** mengubah `ConditionId` aset ke kondisi dengan kode `DISPOSAL` (di-seed).
9. **Permission**: Main Admin melewati seluruh policy; Admin mendapat izin CRUD yang didukung hanya pada halaman terpilih; Member selalu mendapat izin baca dan hanya izin create pada halaman terpilih. Izin halaman goods-in dan goods-out dibedakan pada API. Manajemen akun (`user.manage`) hanya untuk Main Admin.
10. **Import**: baris valid di-insert (satu transaksi), baris invalid dilaporkan; kolom opsional `category_code`, `group_code`, `type_code`, `brand_code`; maks 5000 baris / 5 MB. `Quantity` selalu 0.
11. Index unik tambahan pada `CFG_Role.RoleName` dan `CFG_Permission.PermissionName`; CHECK constraint `Quantity >= 0` (aset) dan `Quantity > 0` (detail).
12. Kolom `CFG_ModelRole.model_id` memakai snake_case persis seperti ERD, sedangkan `CFG_ModelPermission.ModelId` PascalCase.

## Endpoint
Semua ada di Swagger (enum dan TransactionType didokumentasikan). Ringkas: `/api/auth/*` (termasuk `POST /api/auth/password-reset` untuk mengirim permintaan reset tanpa autentikasi), `/api/password-reset-requests` (Admin: daftar, set password atau tolak), `/api/employees`, `/api/users` (termasuk `GET` dan `POST /api/users/{id}/permissions` untuk permission efektif per user), `/api/roles`, `/api/permissions`, `/api/assets` (+ `bulk-business-unit`, `import`, `export`, `import-template`), master (`/api/categories`, `asset-groups`, `asset-types`, `brands`, `asset-models`, `business-units`, `locations`, `asset-statuses`, `asset-conditions`), `/api/goods-in`, `/api/goods-out`, `/api/stock-audits`, `/api/dashboard/summary`, `/api/reports/*`.
