using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdAssetStatusConfiguration : IEntityTypeConfiguration<MdAssetStatus>
{
    public void Configure(EntityTypeBuilder<MdAssetStatus> b)
    {
        b.ToTable("MD_AssetStatus");
        b.HasKey(x => x.StatusId);
        b.Property(x => x.StatusId).HasColumnName("StatusId").ValueGeneratedOnAdd();
        b.Property(x => x.StatusCode).HasColumnName("StatusCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.StatusName).HasColumnName("StatusName").HasMaxLength(100).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.HasIndex(x => x.StatusCode).IsUnique();
    }
}