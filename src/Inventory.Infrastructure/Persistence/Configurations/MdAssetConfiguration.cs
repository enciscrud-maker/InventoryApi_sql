using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdAssetConfiguration : IEntityTypeConfiguration<MdAsset>
{
    public void Configure(EntityTypeBuilder<MdAsset> b)
    {
        b.ToTable("MD_Asset");
        b.HasKey(x => x.AssetId);
        b.Property(x => x.AssetId).HasColumnName("AssetId").ValueGeneratedOnAdd();
        b.Property(x => x.CatId).HasColumnName("CatId");
        b.Property(x => x.BrandId).HasColumnName("BrandId");
        b.Property(x => x.GroupId).HasColumnName("GroupId");
        b.Property(x => x.TypeId).HasColumnName("TypeId");
        b.Property(x => x.AssetName).HasColumnName("AssetName").HasMaxLength(200).IsRequired();
        b.Property(x => x.AssetType).HasColumnName("AssetType").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.StatusId).HasColumnName("StatusId");
        b.Property(x => x.AssetStatus).HasColumnName("AssetStatus").HasMaxLength(50).IsRequired();
        b.Property(x => x.AssetPrefix).HasColumnName("AssetPrefix").HasMaxLength(50).IsRequired();
        b.Property(x => x.AssetCode).HasColumnName("AssetCode").HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedDt).HasColumnName("CreatedDt");
        b.Property(x => x.Quantity).HasColumnName("Quantity");
        b.Property(x => x.UnitOfMeasure).HasColumnName("UnitOfMeasure").HasMaxLength(30);
        b.Property(x => x.SerialNumber).HasColumnName("SerialNumber").HasMaxLength(100);
        b.Property(x => x.AssetTag).HasColumnName("AssetTag").HasMaxLength(100);
        b.Property(x => x.PurchaseOrderNumber).HasColumnName("PurchaseOrderNumber").HasMaxLength(100);
        b.Property(x => x.SapMaterialNumber).HasColumnName("SapMaterialNumber").HasMaxLength(100);
        b.Property(x => x.AssetValue).HasColumnName("AssetValue").HasPrecision(18, 2);
        b.Property(x => x.Description).HasColumnName("Description").HasMaxLength(1000);
        b.Property(x => x.BusUnitId).HasColumnName("BusUnitId");
        b.Property(x => x.LocationId).HasColumnName("LocationId");
        b.Property(x => x.ModelId).HasColumnName("ModelId");
        b.Property(x => x.ConditionId).HasColumnName("ConditionId");
        b.Property(x => x.BusinessUnit).HasColumnName("BusinessUnit").HasMaxLength(100);
        b.Property(x => x.UpdatedDt).HasColumnName("UpdatedDt");
        b.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        b.Fk(x => x.Category, x => x.CatId);
        b.Fk(x => x.Brand, x => x.BrandId);
        b.Fk(x => x.Group, x => x.GroupId);
        b.Fk(x => x.Type, x => x.TypeId);
        b.Fk(x => x.BusUnit, x => x.BusUnitId, false);
        b.Fk(x => x.Location, x => x.LocationId, false);
        b.Fk(x => x.Model, x => x.ModelId, false);
        b.Fk(x => x.Condition, x => x.ConditionId, false);
        b.HasOne(x => x.Status)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.StatusId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AssetPrefix);
        b.HasIndex(x => x.AssetCode).IsUnique();
        b.HasIndex(x => x.SerialNumber).IsUnique().HasFilter("[SerialNumber] IS NOT NULL");
        b.HasIndex(x => x.AssetTag).IsUnique().HasFilter("[AssetTag] IS NOT NULL AND [AssetTag] <> N''");
        b.HasIndex(x => x.SapMaterialNumber).IsUnique().HasFilter("[SapMaterialNumber] IS NOT NULL AND [SapMaterialNumber] <> N''");
        b.ToTable(t => t.HasCheckConstraint("CK_MD_Asset_LocationRequiresBusinessUnit", "[LocationId] IS NULL OR [BusUnitId] IS NOT NULL"));
        b.ToTable(t => t.HasCheckConstraint("CK_MD_Asset_Quantity", "[Quantity] >= 0"));
    }
}
