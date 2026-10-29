namespace Inventory.Domain.Entities;

public class TrGoodDetail
{
    public int GoodDetailId { get; set; }
    public int GoodId { get; set; }
    public int AssetId { get; set; }
    public DateTime CreatedDt { get; set; }
    public int Quantity { get; set; }
    public string? SerialNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? AssetInformation { get; set; }
    public string? Condition { get; set; }
    public string? Remarks { get; set; }
    public string? Specifications { get; set; }
    public string? RequestJustification { get; set; }
    public string? RequiredDuration { get; set; }
    public string? IntendedPurpose { get; set; }
    public TrGood Good { get; set; } = null!;
    public MdAsset Asset { get; set; } = null!;
}
