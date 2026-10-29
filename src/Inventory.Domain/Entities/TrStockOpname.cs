namespace Inventory.Domain.Entities;

public class TrStockOpname
{
    public int StockOpnameId { get; set; }
    public int UserId { get; set; }
    public int AssetId { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public int GroupId { get; set; }
    public int TypeId { get; set; }
    public string StockOpnameName { get; set; } = string.Empty;
    public StockOpnameType StockOpnameType { get; set; }
    public string StockOpnameStatus { get; set; } = string.Empty;
    public string StockOpnamePrefix { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }
    public string StockOpnameNumber { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; }
    public string? BusinessUnit { get; set; }
    public int? LocationId { get; set; }
    public DateTime? UpdatedDt { get; set; }
    public MdUser User { get; set; } = null!;
    public MdAsset Asset { get; set; } = null!;
    public MdCategory Category { get; set; } = null!;
    public MdBrand Brand { get; set; } = null!;
    public MdGroup Group { get; set; } = null!;
    public MdType Type { get; set; } = null!;
    public MdLocation? Location { get; set; }
    public ICollection<TrStockOpnameDetail> Details { get; set; } = new List<TrStockOpnameDetail>();
}
