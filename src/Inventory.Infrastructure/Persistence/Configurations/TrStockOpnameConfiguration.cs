using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrStockOpnameConfiguration : IEntityTypeConfiguration<TrStockOpname>
{
    public void Configure(EntityTypeBuilder<TrStockOpname> b)
    {
        b.ToTable("TR_StockOpname");
        b.HasKey(x => x.StockOpnameId);
        b.Property(x => x.StockOpnameId).HasColumnName("StockOpnameId").ValueGeneratedOnAdd();
        b.Property(x => x.UserId).HasColumnName("UserId");
        b.Property(x => x.AssetId).HasColumnName("AssetId");
        b.Property(x => x.CategoryId).HasColumnName("CategoryId");
        b.Property(x => x.BrandId).HasColumnName("BrandId");
        b.Property(x => x.GroupId).HasColumnName("GroupId");
        b.Property(x => x.TypeId).HasColumnName("TypeId");
        b.Property(x => x.StockOpnameName).HasColumnName("StockOpnameName").HasMaxLength(200).IsRequired();
        b.Property(x => x.StockOpnameType).HasColumnName("StockOpnameType").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.StockOpnameStatus).HasColumnName("StockOpnameStatus").HasMaxLength(30).IsRequired();
        b.Property(x => x.StockOpnamePrefix).HasColumnName("StockOpnamePrefix").HasMaxLength(10).IsRequired();
        b.Property(x => x.CreatedDt).HasColumnName("CreatedDt");
        b.Property(x => x.StockOpnameNumber).HasColumnName("StockOpnameNumber").HasMaxLength(50).IsRequired();
        b.Property(x => x.AuditDate).HasColumnName("AuditDate");
        b.Property(x => x.BusinessUnit).HasColumnName("BusinessUnit").HasMaxLength(100);
        b.Property(x => x.LocationId).HasColumnName("LocationId");
        b.Property(x => x.UpdatedDt).HasColumnName("UpdatedDt");
        b.Fk(x => x.User, x => x.UserId);
        b.Fk(x => x.Asset, x => x.AssetId);
        b.Fk(x => x.Category, x => x.CategoryId);
        b.Fk(x => x.Brand, x => x.BrandId);
        b.Fk(x => x.Group, x => x.GroupId);
        b.Fk(x => x.Type, x => x.TypeId);
        b.Fk(x => x.Location, x => x.LocationId, false);
        b.HasIndex(x => x.StockOpnameNumber).IsUnique();
        b.HasMany(x => x.Details).WithOne(d => d.StockOpname).HasForeignKey(d => d.StockOpnameId).OnDelete(DeleteBehavior.Restrict);
    }
}
