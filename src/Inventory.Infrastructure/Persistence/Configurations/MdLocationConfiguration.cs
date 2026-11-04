using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdLocationConfiguration : IEntityTypeConfiguration<MdLocation>
{
    public void Configure(EntityTypeBuilder<MdLocation> b)
    {
        b.ToTable("MD_Location");
        b.HasKey(x => x.LocationId);
        b.Property(x => x.LocationId).HasColumnName("LocationId").ValueGeneratedOnAdd();
        b.Property(x => x.BusUnitId).HasColumnName("BusUnitId");
        b.Property(x => x.LocationName).HasColumnName("LocationName").HasMaxLength(150).IsRequired();
        b.HasOne(x => x.BusinessUnit).WithMany(x => x.Locations).HasForeignKey(x => x.BusUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}