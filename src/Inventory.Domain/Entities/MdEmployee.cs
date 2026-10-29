namespace Inventory.Domain.Entities;

public class MdEmployee
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePhone { get; set; }
    public string EmployeeNik { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? BusinessUnit { get; set; }
    public MdUser? User { get; set; }
}
