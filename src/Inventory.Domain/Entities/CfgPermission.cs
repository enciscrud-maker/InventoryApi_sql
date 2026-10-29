namespace Inventory.Domain.Entities;

public class CfgPermission
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
