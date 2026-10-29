namespace Inventory.Domain.Entities;

public class MdAssetCondition
{
    public int ConditionId { get; set; }
    public string ConditionCode { get; set; } = string.Empty;
    public string ConditionName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
