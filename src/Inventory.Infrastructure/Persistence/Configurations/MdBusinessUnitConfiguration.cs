using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdBusinessUnitConfiguration : IEntityTypeConfiguration<MdBusinessUnit>
{
    public void Configure(EntityTypeBuilder<MdBusinessUnit> b)
    {
        b.ToTable("MD_BusinessUnit");
        b.HasKey(x => x.BusUnitId);
        b.Property(x => x.BusUnitId).HasColumnName("BusUnitId").ValueGeneratedOnAdd();
        b.Property(x => x.BusUnitCode).HasColumnName("BusUnitCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.BusUnitPrefix).HasColumnName("BusUnitPrefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.LocatioName).HasColumnName("LocatioName").HasMaxLength(150).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.HasIndex(x => x.BusUnitCode).IsUnique();
    }
}
