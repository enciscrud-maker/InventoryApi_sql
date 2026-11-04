namespace Inventory.Domain.Entities;

public class MdLocation
{
    public int LocationId { get; set; }
    public int BusUnitId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public MdBusinessUnit BusinessUnit { get; set; } = null!;
}