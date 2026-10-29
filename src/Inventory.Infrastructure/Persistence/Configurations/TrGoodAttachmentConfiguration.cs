using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrGoodAttachmentConfiguration : IEntityTypeConfiguration<TrGoodAttachment>
{
    public void Configure(EntityTypeBuilder<TrGoodAttachment> b)
    {
        b.ToTable("TR_GoodAttachment");
        b.HasKey(x => x.GoodAttachmentId);
        b.Property(x => x.GoodAttachmentId).HasColumnName("GoodAttachmentId").ValueGeneratedOnAdd();
        b.Property(x => x.GoodId).HasColumnName("GoodId");
        b.Property(x => x.OriginalFileName).HasColumnName("OriginalFileName").HasMaxLength(255).IsRequired();
        b.Property(x => x.StoredFileName).HasColumnName("StoredFileName").HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasColumnName("ContentType").HasMaxLength(100).IsRequired();
        b.Property(x => x.FileSize).HasColumnName("FileSize");
        b.Property(x => x.StoragePath).HasColumnName("StoragePath").HasMaxLength(500).IsRequired();
        b.Property(x => x.UploadedAt).HasColumnName("UploadedAt");
    }
}
