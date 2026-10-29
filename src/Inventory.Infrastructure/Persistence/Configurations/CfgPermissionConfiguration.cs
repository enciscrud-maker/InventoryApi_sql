using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CfgPermissionConfiguration : IEntityTypeConfiguration<CfgPermission>
{
    public void Configure(EntityTypeBuilder<CfgPermission> b)
    {
        b.ToTable("CFG_Permission");
        b.HasKey(x => x.PermissionId);
        b.Property(x => x.PermissionId).HasColumnName("PermissionId").ValueGeneratedOnAdd();
        b.Property(x => x.PermissionName).HasColumnName("PermissionName").HasMaxLength(100).IsRequired();
        b.Property(x => x.GuardName).HasColumnName("GuardName").HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        b.HasIndex(x => x.PermissionName).IsUnique();
    }
}
