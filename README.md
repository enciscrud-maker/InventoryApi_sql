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
9. **Permission** (claim JWT `permission`): Admin semua; Member semua kecuali `master.delete` dan `user.manage`. Seed ada di `DbSeeder`.
10. **Import**: baris valid di-insert (satu transaksi), baris invalid dilaporkan; kolom opsional `category_code`, `group_code`, `type_code`, `brand_code`; maks 5000 baris / 5 MB. `Quantity` selalu 0.
11. Index unik tambahan pada `CFG_Role.RoleName` dan `CFG_Permission.PermissionName`; CHECK constraint `Quantity >= 0` (aset) dan `Quantity > 0` (detail).
12. Kolom `CFG_ModelRole.model_id` memakai snake_case persis seperti ERD, sedangkan `CFG_ModelPermission.ModelId` PascalCase.

## Endpoint
Semua ada di Swagger (enum dan TransactionType didokumentasikan). Ringkas: `/api/auth/*`, `/api/employees`, `/api/users` (termasuk `GET` dan `POST /api/users/{id}/permissions` untuk permission efektif per user), `/api/roles`, `/api/permissions`, `/api/assets` (+ `bulk-business-unit`, `import`, `export`, `import-template`), master (`/api/categories`, `asset-groups`, `asset-types`, `brands`, `asset-models`, `business-units`, `locations`, `asset-statuses`, `asset-conditions`), `/api/goods-in`, `/api/goods-out`, `/api/stock-audits`, `/api/dashboard/summary`, `/api/reports/*`.
