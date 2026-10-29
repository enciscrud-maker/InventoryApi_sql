using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CfgRoleConfiguration : IEntityTypeConfiguration<CfgRole>
{
    public void Configure(EntityTypeBuilder<CfgRole> b)
    {
        b.ToTable("CFG_Role");
        b.HasKey(x => x.RoleId);
        b.Property(x => x.RoleId).HasColumnName("RoleId").ValueGeneratedOnAdd();
        b.Property(x => x.RoleName).HasColumnName("RoleName").HasMaxLength(100).IsRequired();
        b.Property(x => x.GuardName).HasColumnName("GuardName").HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        b.HasIndex(x => x.RoleName).IsUnique();
    }
}
