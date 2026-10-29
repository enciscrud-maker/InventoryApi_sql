using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CfgRolePermissionConfiguration : IEntityTypeConfiguration<CfgRolePermission>
{
    public void Configure(EntityTypeBuilder<CfgRolePermission> b)
    {
        b.ToTable("CFG_RolePermission");
        b.HasKey(x => new { x.PermissionId, x.RoleId });
        b.Property(x => x.PermissionId).HasColumnName("PermissionId").ValueGeneratedNever();
        b.Property(x => x.RoleId).HasColumnName("RoleId").ValueGeneratedNever();
        b.Fk(x => x.Permission, x => x.PermissionId);
        b.Fk(x => x.Role, x => x.RoleId);
    }
}
