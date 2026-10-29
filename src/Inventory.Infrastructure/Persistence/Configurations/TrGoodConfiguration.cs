using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrGoodConfiguration : IEntityTypeConfiguration<TrGood>
{
    public void Configure(EntityTypeBuilder<TrGood> b)
    {
        b.ToTable("TR_Good");
        b.HasKey(x => x.GoodId);
        b.Property(x => x.GoodId).HasColumnName("GoodId").ValueGeneratedOnAdd();
        b.Property(x => x.UserId).HasColumnName("UserId");
        b.Property(x => x.AssetId).HasColumnName("AssetId");
        b.Property(x => x.CatId).HasColumnName("CatId");
        b.Property(x => x.BrandId).HasColumnName("BrandId");
        b.Property(x => x.GroupId).HasColumnName("GroupId");
        b.Property(x => x.TypeId).HasColumnName("TypeId");
        b.Property(x => x.GoodName).HasColumnName("GoodName").HasMaxLength(200).IsRequired();
        b.Property(x => x.GoodType).HasColumnName("GoodType").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.GoodStatus).HasColumnName("GoodStatus").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.GoodPrefix).HasColumnName("GoodPrefix").HasMaxLength(10).IsRequired();
        b.Property(x => x.CreatedDt).HasColumnName("CreatedDT");
        b.Property(x => x.GoodNumber).HasColumnName("GoodNumber").HasMaxLength(50).IsRequired();
        b.Property(x => x.RequestId).HasColumnName("RequestId").HasMaxLength(100).IsRequired();
        b.Property(x => x.TransactionType).HasColumnName("TransactionType").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.BusinessUnit).HasColumnName("BusinessUnit").HasMaxLength(100).IsRequired();
        b.Property(x => x.RequestDate).HasColumnName("RequestDate");
        b.Property(x => x.FormIssueDate).HasColumnName("FormIssueDate");
        b.Property(x => x.PersonName).HasColumnName("PersonName").HasMaxLength(200).IsRequired();
        b.Property(x => x.PersonNik).HasColumnName("PersonNik").HasMaxLength(50).IsRequired();
        b.Property(x => x.PersonDepartment).HasColumnName("PersonDepartment").HasMaxLength(100).IsRequired();
        b.Property(x => x.DestinationLocation).HasColumnName("DestinationLocation").HasMaxLength(200).IsRequired();
        b.Property(x => x.RequestNumber).HasColumnName("RequestNumber").HasMaxLength(100);
        b.Property(x => x.ApproverName).HasColumnName("ApproverName").HasMaxLength(200).IsRequired();
        b.Property(x => x.InventoryNotes).HasColumnName("InventoryNotes").HasMaxLength(1000);
        b.Property(x => x.InventoryAdjustmentApplied).HasColumnName("InventoryAdjustmentApplied");
        b.Property(x => x.InventoryAdjustedAt).HasColumnName("InventoryAdjustedAt");
        b.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        b.Fk(x => x.User, x => x.UserId);
        b.Fk(x => x.Asset, x => x.AssetId);
        b.Fk(x => x.Category, x => x.CatId);
        b.Fk(x => x.Brand, x => x.BrandId);
        b.Fk(x => x.Group, x => x.GroupId);
        b.Fk(x => x.Type, x => x.TypeId);
        b.HasIndex(x => x.GoodNumber).IsUnique();
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasMany(x => x.Details).WithOne(d => d.Good).HasForeignKey(d => d.GoodId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Attachments).WithOne(d => d.Good).HasForeignKey(d => d.GoodId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.StatusHistory).WithOne(d => d.Good).HasForeignKey(d => d.GoodId).OnDelete(DeleteBehavior.Restrict);
    }
}
