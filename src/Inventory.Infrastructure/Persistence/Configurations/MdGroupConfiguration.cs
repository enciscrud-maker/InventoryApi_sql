using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdGroupConfiguration : IEntityTypeConfiguration<MdGroup>
{
    public void Configure(EntityTypeBuilder<MdGroup> b)
    {
        b.ToTable("MD_Group");
        b.HasKey(x => x.GroupId);
        b.Property(x => x.GroupId).HasColumnName("GroupId").ValueGeneratedOnAdd();
        b.Property(x => x.CatId).HasColumnName("CatId");
        b.Property(x => x.GroupName).HasColumnName("GroupName").HasMaxLength(100).IsRequired();
        b.Property(x => x.GroupCode).HasColumnName("GroupCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.GroupPrefix).HasColumnName("GroupPrefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.Fk(x => x.Category, x => x.CatId);
        b.HasIndex(x => x.GroupCode).IsUnique();
    }
}
