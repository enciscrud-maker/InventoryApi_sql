using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdLocationConfiguration : IEntityTypeConfiguration<MdLocation>
{
    public void Configure(EntityTypeBuilder<MdLocation> b)
    {
        b.ToTable("MD_Location");
        b.HasKey(x => x.LocationId);
        b.Property(x => x.LocationId).HasColumnName("LocationId").ValueGeneratedOnAdd();
        b.Property(x => x.LocationCode).HasColumnName("LocationCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.LocationName).HasColumnName("LocationName").HasMaxLength(150).IsRequired();
        b.Property(x => x.BusinessUnit).HasColumnName("BusinessUnit").HasMaxLength(100);
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.HasIndex(x => x.LocationCode).IsUnique();
    }
}
