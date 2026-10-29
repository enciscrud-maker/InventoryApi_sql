namespace Inventory.Domain.Entities;

public class MdCategory
{
    public int CatId { get; set; }
    public string CatName { get; set; } = string.Empty;
    public string CatCode { get; set; } = string.Empty;
    public string CatPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
