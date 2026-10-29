namespace Inventory.Domain.Entities;

public class MdUser
{
    public int UserId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string EmployeePassword { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ProfilePhotoUrl { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public MdEmployee Employee { get; set; } = null!;
}
