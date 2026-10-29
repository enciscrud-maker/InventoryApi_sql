using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrStockOpnameDetailConfiguration : IEntityTypeConfiguration<TrStockOpnameDetail>
{
    public void Configure(EntityTypeBuilder<TrStockOpnameDetail> b)
    {
        b.ToTable("TR_StockOpnameDetail");
        b.HasKey(x => x.StockOpnameDetailId);
        b.Property(x => x.StockOpnameDetailId).HasColumnName("StockOpnameDetailId").ValueGeneratedOnAdd();
        b.Property(x => x.StockOpnameId).HasColumnName("StockOpnameId");
        b.Property(x => x.AssetId).HasColumnName("AssetId");
        b.Property(x => x.SystemQuantity).HasColumnName("SystemQuantity");
        b.Property(x => x.PhysicalQuantity).HasColumnName("PhysicalQuantity");
        b.Property(x => x.ActualLocationId).HasColumnName("ActualLocationId");
        b.Property(x => x.Finding).HasColumnName("Finding").HasMaxLength(500);
        b.Property(x => x.IsConfirmed).HasColumnName("IsConfirmed");
        b.Property(x => x.Notes).HasColumnName("Notes").HasMaxLength(500);
        b.Property(x => x.EvidencePath).HasColumnName("EvidencePath").HasMaxLength(500);
        b.Fk(x => x.Asset, x => x.AssetId);
        b.Fk(x => x.ActualLocation, x => x.ActualLocationId, false);
    }
}
