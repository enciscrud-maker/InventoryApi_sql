namespace Inventory.Domain.Entities;

public class MdLocation
{
    public int LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? BusinessUnit { get; set; }
    public bool IsActive { get; set; } = true;
}
