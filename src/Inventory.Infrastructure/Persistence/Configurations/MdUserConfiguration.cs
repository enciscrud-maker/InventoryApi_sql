using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MdUserConfiguration : IEntityTypeConfiguration<MdUser>
{
    public void Configure(EntityTypeBuilder<MdUser> b)
    {
        b.ToTable("MD_User");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasColumnName("UserId").ValueGeneratedOnAdd();
        b.Property(x => x.EmployeeId).HasColumnName("EmployeeId");
        b.Property(x => x.EmployeeName).HasColumnName("EmployeeName").HasMaxLength(200).IsRequired();
        b.Property(x => x.EmployeeEmail).HasColumnName("EmployeeEmail").HasMaxLength(200).IsRequired();
        b.Property(x => x.EmployeePassword).HasColumnName("EmployeePassword").HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
        b.Property(x => x.IsActive).HasColumnName("IsActive");
        b.Property(x => x.ProfilePhotoUrl).HasColumnName("ProfilePhotoUrl").HasMaxLength(500);
        b.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
        b.HasOne(x => x.Employee).WithOne(e => e.User).HasForeignKey<MdUser>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EmployeeEmail).IsUnique();
    }
}
