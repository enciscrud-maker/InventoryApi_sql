using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdAssetConditionConfiguration : IEntityTypeConfiguration<MdAssetCondition>
{
    public void Configure(EntityTypeBuilder<MdAssetCondition> b)
    {
        b.ToTable("MD_AssetCondition");
        b.HasKey(x => x.ConditionId);
        b.Property(x => x.ConditionId).HasColumnName("ConditionId").ValueGeneratedOnAdd();
        b.Property(x => x.ConditionCode).HasColumnName("ConditionCode").HasMaxLength(30).IsRequired();
        b.Property(x => x.ConditionName).HasColumnName("ConditionName").HasMaxLength(100).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.HasIndex(x => x.ConditionCode).IsUnique();
    }
}
