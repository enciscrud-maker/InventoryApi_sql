namespace Inventory.Domain.Entities;

public class MdAsset
{
    public int AssetId { get; set; }
    public int CatId { get; set; }
    public int BrandId { get; set; }
    public int GroupId { get; set; }
    public int TypeId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public int StatusId { get; set; }
    public string AssetPrefix { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }
    public int Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? SerialNumber { get; set; }
    public string? AssetTag { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? SapMaterialNumber { get; set; }
    public decimal? AssetValue { get; set; }
    public string? Description { get; set; }
    public int? BusUnitId { get; set; }
    public int? LocationId { get; set; }
    public int? ModelId { get; set; }
    public int? ConditionId { get; set; }
    public string? BusinessUnit { get; set; }
    public DateTime? UpdatedDt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public MdCategory Category { get; set; } = null!;
    public MdBrand Brand { get; set; } = null!;
    public MdGroup Group { get; set; } = null!;
    public MdType Type { get; set; } = null!;
    public MdBusinessUnit? BusUnit { get; set; }
    public MdLocation? Location { get; set; }
    public MdModel? Model { get; set; }
    public MdAssetCondition? Condition { get; set; }
    public MdAssetStatus Status { get; set; } = null!;
}
