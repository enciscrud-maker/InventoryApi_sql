namespace Inventory.Domain.Entities;

public class TrGoodStatusHistory
{
    public int GoodStatusHistoryId { get; set; }
    public int GoodId { get; set; }
    public GoodStatus PreviousStatus { get; set; }
    public GoodStatus NewStatus { get; set; }
    public int ActorUserId { get; set; }
    public string ActorNameSnapshot { get; set; } = string.Empty;
    public string ActorNikSnapshot { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
    public TrGood Good { get; set; } = null!;
    public MdUser Actor { get; set; } = null!;
}
