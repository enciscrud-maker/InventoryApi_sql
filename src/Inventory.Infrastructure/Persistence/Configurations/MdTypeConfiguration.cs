using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdTypeConfiguration : IEntityTypeConfiguration<MdType>
{
    public void Configure(EntityTypeBuilder<MdType> b)
    {
        b.ToTable("MD_Type");
        b.HasKey(x => x.TypeId);
        b.Property(x => x.TypeId).HasColumnName("TypeId").ValueGeneratedOnAdd();
        b.Property(x => x.GroupId).HasColumnName("GroupId");
        b.Property(x => x.CatId).HasColumnName("CatId");
        b.Property(x => x.TypeName).HasColumnName("TypeName").HasMaxLength(100).IsRequired();
        b.Property(x => x.TypeCode).HasColumnName("TypeCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.TypePrefix).HasColumnName("TypePrefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.Fk(x => x.Group, x => x.GroupId);
        b.Fk(x => x.Category, x => x.CatId);
        b.HasIndex(x => x.TypeCode).IsUnique();
    }
}
