using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdEmployeeConfiguration : IEntityTypeConfiguration<MdEmployee>
{
    public void Configure(EntityTypeBuilder<MdEmployee> b)
    {
        b.ToTable("MD_Employee");
        b.HasKey(x => x.EmployeeId);
        b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").ValueGeneratedOnAdd();
        b.Property(x => x.EmployeeName).HasColumnName("EmployeeName").HasMaxLength(200).IsRequired();
        b.Property(x => x.EmployeePhone).HasColumnName("EmployeePhone").HasMaxLength(30);
        b.Property(x => x.EmployeeNik).HasColumnName("EmployeeNik").HasMaxLength(50).IsRequired();
        b.Property(x => x.Position).HasColumnName("Position").HasMaxLength(100);
        b.Property(x => x.Department).HasColumnName("Department").HasMaxLength(100);
        b.Property(x => x.BusinessUnit).HasColumnName("BusinessUnit").HasMaxLength(100);
        b.HasIndex(x => x.EmployeeNik).IsUnique();
    }
}
