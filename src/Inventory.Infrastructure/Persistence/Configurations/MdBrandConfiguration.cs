using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdBrandConfiguration : IEntityTypeConfiguration<MdBrand>
{
    public void Configure(EntityTypeBuilder<MdBrand> b)
    {
        b.ToTable("MD_Brand");
        b.HasKey(x => x.BrandId);
        b.Property(x => x.BrandId).HasColumnName("BrandId").ValueGeneratedOnAdd();
        b.Property(x => x.TypeId).HasColumnName("TypeId");
        b.Property(x => x.BrandName).HasColumnName("BrandName").HasMaxLength(100).IsRequired();
        b.Property(x => x.BrandCode).HasColumnName("BrandCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.BrandPrefix).HasColumnName("BrandPrefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.Fk(x => x.Type, x => x.TypeId, false);
        b.HasIndex(x => x.BrandCode).IsUnique();
    }
}
