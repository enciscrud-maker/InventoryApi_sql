using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdModelConfiguration : IEntityTypeConfiguration<MdModel>
{
    public void Configure(EntityTypeBuilder<MdModel> b)
    {
        b.ToTable("MD_Model");
        b.HasKey(x => x.ModelId);
        b.Property(x => x.ModelId).HasColumnName("ModelId").ValueGeneratedOnAdd();
        b.Property(x => x.AssetId).HasColumnName("AssetId");
        b.Property(x => x.CatId).HasColumnName("CatId");
        b.Property(x => x.BrandId).HasColumnName("BrandId");
        b.Property(x => x.GroupId).HasColumnName("GroupId");
        b.Property(x => x.TypeId).HasColumnName("TypeId");
        b.Property(x => x.ModelName).HasColumnName("ModelName").HasMaxLength(150).IsRequired();
        b.Property(x => x.ModelCode).HasColumnName("ModelCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.Fk(x => x.Asset, x => x.AssetId, false);
        b.Fk(x => x.Category, x => x.CatId);
        b.Fk(x => x.Brand, x => x.BrandId);
        b.Fk(x => x.Group, x => x.GroupId);
        b.Fk(x => x.Type, x => x.TypeId);
        b.HasIndex(x => x.ModelCode).IsUnique();
    }
}
