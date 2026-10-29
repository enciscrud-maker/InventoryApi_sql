namespace Inventory.Domain.Entities;

public class CfgModelRole
{
    public int RoleId { get; set; }
    public int ModelId { get; set; }
    public CfgRole Role { get; set; } = null!;
    public MdUser User { get; set; } = null!;
}
