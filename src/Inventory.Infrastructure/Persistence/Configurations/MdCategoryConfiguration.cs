using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdCategoryConfiguration : IEntityTypeConfiguration<MdCategory>
{
    public void Configure(EntityTypeBuilder<MdCategory> b)
    {
        b.ToTable("MD_Category");
        b.HasKey(x => x.CatId);
        b.Property(x => x.CatId).HasColumnName("CatId").ValueGeneratedOnAdd();
        b.Property(x => x.CatName).HasColumnName("CatName").HasMaxLength(100).IsRequired();
        b.Property(x => x.CatCode).HasColumnName("CatCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.CatPrefix).HasColumnName("CatPrefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.HasIndex(x => x.CatCode).IsUnique();
    }
}
