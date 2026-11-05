using System.Linq.Expressions;
using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inventory.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<MdCategory> Categories => Set<MdCategory>();
    public DbSet<MdGroup> Groups => Set<MdGroup>();
    public DbSet<MdType> Types => Set<MdType>();
    public DbSet<MdBrand> Brands => Set<MdBrand>();
    public DbSet<MdModel> Models => Set<MdModel>();
    public DbSet<MdAsset> Assets => Set<MdAsset>();
    public DbSet<MdBusinessUnit> BusinessUnits => Set<MdBusinessUnit>();
    public DbSet<MdLocation> Locations => Set<MdLocation>();
    public DbSet<MdAssetStatus> AssetStatuses => Set<MdAssetStatus>();
    public DbSet<MdAssetCondition> Conditions => Set<MdAssetCondition>();
    public DbSet<MdEmployee> Employees => Set<MdEmployee>();
    public DbSet<MdUser> Users => Set<MdUser>();
    public DbSet<CfgRole> Roles => Set<CfgRole>();
    public DbSet<CfgPermission> Permissions => Set<CfgPermission>();
    public DbSet<CfgRolePermission> RolePermissions => Set<CfgRolePermission>();
    public DbSet<CfgModelRole> ModelRoles => Set<CfgModelRole>();
    public DbSet<CfgModelPermission> ModelPermissions => Set<CfgModelPermission>();
    public DbSet<TrGood> Goods => Set<TrGood>();
    public DbSet<TrGoodDetail> GoodDetails => Set<TrGoodDetail>();
    public DbSet<TrGoodAttachment> GoodAttachments => Set<TrGoodAttachment>();
    public DbSet<TrGoodStatusHistory> GoodStatusHistories => Set<TrGoodStatusHistory>();
    public DbSet<TrStockOpname> StockOpnames => Set<TrStockOpname>();
    public DbSet<TrStockOpnameDetail> StockOpnameDetails => Set<TrStockOpnameDetail>();
    public DbSet<TrPasswordResetRequest> PasswordResetRequests => Set<TrPasswordResetRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        // Rule: every FK is Restrict (no cascades). Safety net on top of the explicit configuration.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // Rule 16: all timestamps are UTC, also when materialized.
        cb.Properties<DateTime>().HaveConversion<UtcConverter>();
        cb.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }
}

public class UtcConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

public static class ConfigExtensions
{
    /// <summary>Many-to-one FK with OnDelete(Restrict).</summary>
    public static void Fk<TE, TP>(this EntityTypeBuilder<TE> b, Expression<Func<TE, TP?>> nav, Expression<Func<TE, object?>> fk, bool required = true)
        where TE : class where TP : class
    {
        b.HasOne(nav).WithMany().HasForeignKey(fk).IsRequired(required).OnDelete(DeleteBehavior.Restrict);
    }
}
