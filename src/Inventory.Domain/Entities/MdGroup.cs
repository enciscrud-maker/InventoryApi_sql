namespace Inventory.Domain.Entities;

public class MdGroup
{
    public int GroupId { get; set; }
    public int CatId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string GroupPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public MdCategory Category { get; set; } = null!;
}
