namespace Inventory.Domain.Entities;

public enum PasswordResetRequestStatus
{
    Pending,
    Processed,
    Rejected
}

public class TrPasswordResetRequest
{
    public int PasswordResetRequestId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Nik { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public PasswordResetRequestStatus Status { get; set; } = PasswordResetRequestStatus.Pending;
    public int? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
    public MdUser User { get; set; } = null!;
    public MdUser? ProcessedBy { get; set; }
}
