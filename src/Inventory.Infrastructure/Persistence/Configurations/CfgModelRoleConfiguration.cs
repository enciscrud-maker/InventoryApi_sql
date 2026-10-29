using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CfgModelRoleConfiguration : IEntityTypeConfiguration<CfgModelRole>
{
    public void Configure(EntityTypeBuilder<CfgModelRole> b)
    {
        b.ToTable("CFG_ModelRole");
        b.HasKey(x => new { x.RoleId, x.ModelId });
        b.Property(x => x.RoleId).HasColumnName("RoleId").ValueGeneratedNever();
        b.Property(x => x.ModelId).HasColumnName("model_id").ValueGeneratedNever();
        b.Fk(x => x.Role, x => x.RoleId);
        b.Fk(x => x.User, x => x.ModelId);
    }
}
