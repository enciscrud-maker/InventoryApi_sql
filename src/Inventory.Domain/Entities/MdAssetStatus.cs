namespace Inventory.Domain.Entities;

public class MdAssetStatus
{
    public int StatusId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<MdAsset> Assets { get; set; } = new List<MdAsset>();
}