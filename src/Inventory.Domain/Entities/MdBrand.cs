namespace Inventory.Domain.Entities;

public class MdBrand
{
    public int BrandId { get; set; }
    public int? TypeId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public string BrandPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public MdType? Type { get; set; }
}
