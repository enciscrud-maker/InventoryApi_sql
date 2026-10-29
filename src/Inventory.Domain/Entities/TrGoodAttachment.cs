namespace Inventory.Domain.Entities;

public class TrGoodAttachment
{
    public int GoodAttachmentId { get; set; }
    public int GoodId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public TrGood Good { get; set; } = null!;
}
