namespace Inventory.Domain.Entities;

public class MdBusinessUnit
{
    public int BusUnitId { get; set; }
    public string BusUnitCode { get; set; } = string.Empty;
    public string BusUnitPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<MdLocation> Locations { get; set; } = new List<MdLocation>();
}
