namespace Inventory.Domain.Entities;

public class MdLocation
{
    public int LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public int BusUnitId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public MdBusinessUnit BusinessUnit { get; set; } = null!;
}