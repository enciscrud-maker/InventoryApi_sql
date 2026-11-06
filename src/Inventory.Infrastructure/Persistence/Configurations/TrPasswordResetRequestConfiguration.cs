using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrPasswordResetRequestConfiguration : IEntityTypeConfiguration<TrPasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<TrPasswordResetRequest> b)
    {
        b.ToTable("TR_PasswordResetRequest");
        b.HasKey(x => x.PasswordResetRequestId);
        b.Property(x => x.PasswordResetRequestId).HasColumnName("PasswordResetRequestId").ValueGeneratedOnAdd();
        b.Property(x => x.RequestId).HasColumnName("RequestId").HasMaxLength(40).IsRequired();
        b.Property(x => x.UserId).HasColumnName("UserId").IsRequired(false);
        b.Property(x => x.MemberName).HasColumnName("MemberName").HasMaxLength(200).IsRequired();
        b.Property(x => x.Nik).HasColumnName("Nik").HasMaxLength(50).IsRequired();
        b.Property(x => x.Email).HasColumnName("Email").HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(30).IsRequired();
        b.Property(x => x.RequestDate).HasColumnName("RequestDate");
        b.Property(x => x.Status).HasColumnName("Status").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ProcessedByUserId).HasColumnName("ProcessedByUserId");
        b.Property(x => x.ProcessedAt).HasColumnName("ProcessedAt");
        b.Property(x => x.RejectionReason).HasColumnName("RejectionReason").HasMaxLength(500);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ProcessedBy).WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => new { x.Status, x.RequestDate });
    }
}
