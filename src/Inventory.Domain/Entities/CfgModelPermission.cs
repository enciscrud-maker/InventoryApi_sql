namespace Inventory.Domain.Entities;

public class CfgModelPermission
{
    public int PermissionId { get; set; }
    public int ModelId { get; set; }
    public CfgPermission Permission { get; set; } = null!;
    public MdUser User { get; set; } = null!;
}
