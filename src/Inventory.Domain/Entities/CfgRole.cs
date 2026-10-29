namespace Inventory.Domain.Entities;

public class CfgRole
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
