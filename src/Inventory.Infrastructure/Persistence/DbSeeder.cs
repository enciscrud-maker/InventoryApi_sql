using Inventory.Application.Abstractions;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Inventory.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task InitializeAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration cfg, CancellationToken ct = default)
    {
        // Prefer migrations; fall back to EnsureCreated when none have been generated yet (see README).
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);

        await SeedSecurityAsync(db, ct);
        await SeedLookupsAsync(db, ct);
        await SeedAdminAsync(db, hasher, cfg, ct);
        if (cfg.GetValue<bool>("Seed:SampleData")) await SeedSampleAsync(db, ct);
    }

    private static async Task SeedSecurityAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var name in new[] { RoleNames.Admin, RoleNames.Member })
            if (!await db.Roles.AnyAsync(r => r.RoleName == name, ct))
                db.Roles.Add(new CfgRole { RoleName = name, GuardName = RoleNames.Guard, CreatedAt = now });
        foreach (var p in Permissions.All)
            if (!await db.Permissions.AnyAsync(x => x.PermissionName == p, ct))
                db.Permissions.Add(new CfgPermission { PermissionName = p, GuardName = RoleNames.Guard, CreatedAt = now });
        await db.SaveChangesAsync(ct);

        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleName, ct);
        var perms = await db.Permissions.ToDictionaryAsync(p => p.PermissionName, ct);
        var existing = (await db.RolePermissions.ToListAsync(ct)).Select(x => (x.RoleId, x.PermissionId)).ToHashSet();
        void Grant(string role, IEnumerable<string> names)
        {
            foreach (var n in names)
                if (!existing.Contains((roles[role].RoleId, perms[n].PermissionId)))
                    db.RolePermissions.Add(new CfgRolePermission { RoleId = roles[role].RoleId, PermissionId = perms[n].PermissionId });
        }
        Grant(RoleNames.Admin, Permissions.All);
        Grant(RoleNames.Member, Permissions.MemberDefaults);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedLookupsAsync(AppDbContext db, CancellationToken ct)
    {
        var conditions = new[] { ("NEW", "New"), ("GOOD", "Good"), ("FAIR", "Fair"), ("DAMAGED", "Damaged"), (ConditionCodes.Disposal, "Disposal") };
        foreach (var (code, name) in conditions)
            if (!await db.Conditions.AnyAsync(c => c.ConditionCode == code, ct))
                db.Conditions.Add(new MdAssetCondition { ConditionCode = code, ConditionName = name, IsActive = true });
        if (!await db.AssetStatuses.AnyAsync(s => s.StatusCode == "ACTIVE", ct))
            db.AssetStatuses.Add(new MdAssetStatus { StatusCode = "ACTIVE", StatusName = "Active", IsActive = true });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration cfg, CancellationToken ct)
    {
        var password = cfg["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;            // never ship a default admin password to production
        var email = cfg["Seed:AdminEmail"] ?? "admin@inventory.local";
        var nik = cfg["Seed:AdminNik"] ?? "0000000001";
        if (await db.Users.AnyAsync(u => u.EmployeeEmail == email, ct)) return;

        var emp = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeNik == nik, ct)
            ?? db.Employees.Add(new MdEmployee { EmployeeName = "System Administrator", EmployeeNik = nik, Department = "IT", Position = "Administrator" }).Entity;
        await db.SaveChangesAsync(ct);
        var user = new MdUser
        {
            EmployeeId = emp.EmployeeId, EmployeeName = emp.EmployeeName, EmployeeEmail = email,
            EmployeePassword = hasher.Hash(password), CreatedAt = DateTime.UtcNow, IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        var adminRole = await db.Roles.FirstAsync(r => r.RoleName == RoleNames.Admin, ct);
        db.ModelRoles.Add(new CfgModelRole { RoleId = adminRole.RoleId, ModelId = user.UserId });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSampleAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;
        var hq = new MdBusinessUnit { BusUnitCode = "HQ-JKT", BusUnitPrefix = "HQ", IsActive = true };
        var wh = new MdBusinessUnit { BusUnitCode = "WH-01", BusUnitPrefix = "WH", IsActive = true };
        db.BusinessUnits.AddRange(hq, wh);
        await db.SaveChangesAsync(ct);
        db.Locations.AddRange(
            new MdLocation { BusUnitId = hq.BusUnitId, LocationName = "Head Office Jakarta" },
            new MdLocation { BusUnitId = wh.BusUnitId, LocationName = "Main Warehouse" });

        var cat = new MdCategory { CatName = "Electronics", CatCode = "ELC", CatPrefix = "EL", IsActive = true };
        db.Categories.Add(cat); await db.SaveChangesAsync(ct);
        var grp = new MdGroup { CatId = cat.CatId, GroupName = "Computer", GroupCode = "CMP", GroupPrefix = "CP", IsActive = true };
        db.Groups.Add(grp); await db.SaveChangesAsync(ct);
        var typ = new MdType { CatId = cat.CatId, GroupId = grp.GroupId, TypeName = "Laptop", TypeCode = "LPT", TypePrefix = "LP", IsActive = true };
        db.Types.Add(typ); await db.SaveChangesAsync(ct);
        var lenovo = new MdBrand { TypeId = typ.TypeId, BrandName = "Lenovo", BrandCode = "LNV", BrandPrefix = "LN", IsActive = true };
        var dell = new MdBrand { TypeId = typ.TypeId, BrandName = "Dell", BrandCode = "DLL", BrandPrefix = "DL", IsActive = true };
        db.Brands.AddRange(lenovo, dell); await db.SaveChangesAsync(ct);

        var good = await db.Conditions.FirstAsync(c => c.ConditionCode == "GOOD", ct);
        var active = await db.AssetStatuses.FirstAsync(s => s.StatusCode == "ACTIVE", ct);
        var asset = new MdAsset
        {
            CatId = cat.CatId, GroupId = grp.GroupId, TypeId = typ.TypeId, BrandId = lenovo.BrandId, AssetName = "Lenovo ThinkPad T14",
            AssetType = AssetType.In, StatusId = active.StatusId, AssetPrefix = "LPT-0001", CreatedDt = DateTime.UtcNow, Quantity = 10, UnitOfMeasure = "unit",
            BusUnitId = wh.BusUnitId, ConditionId = good.ConditionId, BusinessUnit = "Logistics", AssetValue = 18_500_000m
        };
        var asset2 = new MdAsset
        {
            CatId = cat.CatId, GroupId = grp.GroupId, TypeId = typ.TypeId, BrandId = dell.BrandId, AssetName = "Dell Latitude 5440",
            AssetType = AssetType.In, StatusId = active.StatusId, AssetPrefix = "LPT-0002", CreatedDt = DateTime.UtcNow, Quantity = 5, UnitOfMeasure = "unit",
            BusUnitId = hq.BusUnitId, ConditionId = good.ConditionId, BusinessUnit = "Corporate", AssetValue = 16_000_000m
        };
        db.Assets.AddRange(asset, asset2); await db.SaveChangesAsync(ct);
        db.Models.AddRange(
            new MdModel { AssetId = asset.AssetId, CatId = cat.CatId, GroupId = grp.GroupId, TypeId = typ.TypeId, BrandId = lenovo.BrandId, ModelName = "ThinkPad T14 Gen 4", ModelCode = "LNV-T14G4", IsActive = true },
            new MdModel { AssetId = asset2.AssetId, CatId = cat.CatId, GroupId = grp.GroupId, TypeId = typ.TypeId, BrandId = dell.BrandId, ModelName = "Latitude 5440", ModelCode = "DLL-L5440", IsActive = true });
        await db.SaveChangesAsync(ct);
    }
}
