namespace Inventory.Domain.Entities;

public class MdType
{
    public int TypeId { get; set; }
    public int GroupId { get; set; }
    public int CatId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
    public string TypePrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public MdGroup Group { get; set; } = null!;
    public MdCategory Category { get; set; } = null!;
}
