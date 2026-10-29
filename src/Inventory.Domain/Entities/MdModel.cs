namespace Inventory.Domain.Entities;

public class MdModel
{
    public int ModelId { get; set; }
    public int? AssetId { get; set; }
    public int CatId { get; set; }
    public int BrandId { get; set; }
    public int GroupId { get; set; }
    public int TypeId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public MdAsset? Asset { get; set; }
    public MdCategory Category { get; set; } = null!;
    public MdBrand Brand { get; set; } = null!;
    public MdGroup Group { get; set; } = null!;
    public MdType Type { get; set; } = null!;
}
