namespace Inventory.Domain.Entities;

public class CfgRolePermission
{
    public int PermissionId { get; set; }
    public int RoleId { get; set; }
    public CfgPermission Permission { get; set; } = null!;
    public CfgRole Role { get; set; } = null!;
}
