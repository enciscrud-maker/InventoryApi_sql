using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CfgModelPermissionConfiguration : IEntityTypeConfiguration<CfgModelPermission>
{
    public void Configure(EntityTypeBuilder<CfgModelPermission> b)
    {
        b.ToTable("CFG_ModelPermission");
        b.HasKey(x => new { x.PermissionId, x.ModelId });
        b.Property(x => x.PermissionId).HasColumnName("PermissionId").ValueGeneratedNever();
        b.Property(x => x.ModelId).HasColumnName("ModelId").ValueGeneratedNever();
        b.Fk(x => x.Permission, x => x.PermissionId);
        b.Fk(x => x.User, x => x.ModelId);
    }
}
