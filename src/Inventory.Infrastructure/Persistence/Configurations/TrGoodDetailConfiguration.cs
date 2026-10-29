using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrGoodDetailConfiguration : IEntityTypeConfiguration<TrGoodDetail>
{
    public void Configure(EntityTypeBuilder<TrGoodDetail> b)
    {
        b.ToTable("TR_GoodDetail");
        b.HasKey(x => x.GoodDetailId);
        b.Property(x => x.GoodDetailId).HasColumnName("GoodDetailId").ValueGeneratedOnAdd();
        b.Property(x => x.GoodId).HasColumnName("GoodId");
        b.Property(x => x.AssetId).HasColumnName("AssetId");
        b.Property(x => x.CreatedDt).HasColumnName("CreatedDt");
        b.Property(x => x.Quantity).HasColumnName("Quantity");
        b.Property(x => x.SerialNumber).HasColumnName("SerialNumber").HasMaxLength(100);
        b.Property(x => x.ReferenceNumber).HasColumnName("ReferenceNumber").HasMaxLength(100);
        b.Property(x => x.AssetInformation).HasColumnName("AssetInformation").HasMaxLength(500);
        b.Property(x => x.Condition).HasColumnName("Condition").HasMaxLength(100);
        b.Property(x => x.Remarks).HasColumnName("Remarks").HasMaxLength(500);
        b.Property(x => x.Specifications).HasColumnName("Specifications").HasMaxLength(500);
        b.Property(x => x.RequestJustification).HasColumnName("RequestJustification").HasMaxLength(500);
        b.Property(x => x.RequiredDuration).HasColumnName("RequiredDuration").HasMaxLength(100);
        b.Property(x => x.IntendedPurpose).HasColumnName("IntendedPurpose").HasMaxLength(500);
        b.Fk(x => x.Asset, x => x.AssetId);
        b.ToTable(t => t.HasCheckConstraint("CK_TR_GoodDetail_Quantity", "[Quantity] > 0"));
    }
}
