namespace Inventory.Domain.Entities;

public class TrGood
{
    public int GoodId { get; set; }
    public int UserId { get; set; }
    public int AssetId { get; set; }
    public int CatId { get; set; }
    public int BrandId { get; set; }
    public int GroupId { get; set; }
    public int TypeId { get; set; }
    public string GoodName { get; set; } = string.Empty;
    public GoodType GoodType { get; set; }
    public GoodStatus GoodStatus { get; set; } = GoodStatus.InProgress;
    public string GoodPrefix { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }
    public string GoodNumber { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public string BusinessUnit { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime FormIssueDate { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string PersonNik { get; set; } = string.Empty;
    public string PersonDepartment { get; set; } = string.Empty;
    public string DestinationLocation { get; set; } = string.Empty;
    public string? RequestNumber { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string? InventoryNotes { get; set; }
    public bool InventoryAdjustmentApplied { get; set; }
    public DateTime? InventoryAdjustedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public MdUser User { get; set; } = null!;
    public MdAsset Asset { get; set; } = null!;
    public MdCategory Category { get; set; } = null!;
    public MdBrand Brand { get; set; } = null!;
    public MdGroup Group { get; set; } = null!;
    public MdType Type { get; set; } = null!;
    public ICollection<TrGoodDetail> Details { get; set; } = new List<TrGoodDetail>();
    public ICollection<TrGoodAttachment> Attachments { get; set; } = new List<TrGoodAttachment>();
    public ICollection<TrGoodStatusHistory> StatusHistory { get; set; } = new List<TrGoodStatusHistory>();
}
