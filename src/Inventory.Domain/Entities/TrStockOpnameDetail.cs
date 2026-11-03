namespace Inventory.Domain.Entities;

public class TrStockOpnameDetail
{
    public int StockOpnameDetailId { get; set; }
    public int StockOpnameId { get; set; }
    public int AssetId { get; set; }
    public int SystemQuantity { get; set; }
    public int PhysicalQuantity { get; set; }
    public int? ActualBusUnitId { get; set; }
    public string? Finding { get; set; }
    public bool IsConfirmed { get; set; }
    public string? Notes { get; set; }
    public string? EvidencePath { get; set; }
    public TrStockOpname StockOpname { get; set; } = null!;
    public MdAsset Asset { get; set; } = null!;
    public MdBusinessUnit? ActualBusUnit { get; set; }
}
