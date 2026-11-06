namespace Inventory.Application.Services;

public class DashboardService(IAppDbContext db)
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var totalAssets = await db.Assets.CountAsync(ct);
        var totalQty = await db.Assets.SumAsync(a => (int?)a.Quantity, ct) ?? 0;
        var outOfStock = await db.Assets.CountAsync(a => a.Quantity == 0, ct);
        var inProgress = await db.Goods.CountAsync(g => g.GoodStatus == GoodStatus.InProgress, ct);
        var in30 = await db.Goods.CountAsync(g => g.GoodType == GoodType.In && g.CreatedDt >= since, ct);
        var out30 = await db.Goods.CountAsync(g => g.GoodType == GoodType.Out && g.CreatedDt >= since, ct);
        var qIn = await db.GoodDetails.Where(d => d.Good.GoodType == GoodType.In && d.Good.CreatedDt >= since).SumAsync(d => (int?)d.Quantity, ct) ?? 0;
        var qOut = await db.GoodDetails.Where(d => d.Good.GoodType == GoodType.Out && d.Good.CreatedDt >= since).SumAsync(d => (int?)d.Quantity, ct) ?? 0;
        var openAudits = await db.StockOpnames.CountAsync(s => s.StockOpnameStatus != StockOpnameStatuses.Completed, ct);
        var byCat = await db.Assets.GroupBy(a => a.Category.CatName).OrderBy(g => g.Key)
            .Select(g => new CategorySummaryDto(g.Key, g.Count(), g.Sum(a => a.Quantity))).ToListAsync(ct);
        var recent = await db.Goods.AsNoTracking().OrderByDescending(g => g.CreatedDt).Take(5).Select(GoodService.ListProjection).ToListAsync(ct);
        return new DashboardSummaryDto(totalAssets, totalQty, outOfStock, inProgress, in30, out30, qIn, qOut, openAudits, byCat, recent);
    }
}

public class ReportService(AssetService assets, GoodService goods, AuditService audits)
{
    public Task<PagedResult<AssetDto>> AssetsAsync(AssetQuery q, CancellationToken ct) => assets.ListAsync(q, ct);

    public Task<PagedResult<GoodListItemDto>> TransactionsAsync(ReportQuery q, CancellationToken ct) => goods.ListAsync(q.GoodType, ToGoodQuery(q), ct);

    public Task<PagedResult<AuditListItemDto>> AuditsAsync(ReportQuery q, CancellationToken ct) => audits.ListAsync(ToAuditQuery(q), ct);

    public async Task<(byte[] Content, string FileName)> ExportAsync(ReportQuery q, CancellationToken ct)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        switch ((q.Type ?? "assets").ToLowerInvariant())
        {
            case "assets":
                return (await assets.ExportAsync(q, ct), $"assets-{stamp}.xlsx");
            case "transactions":
            {
                var items = await goods.Filtered(q.GoodType, ToGoodQuery(q)).OrderByDescending(g => g.RequestDate).Select(GoodService.ListProjection).ToListAsync(ct);
                var bytes = ExcelHelper.Build("Transactions", new[] { "Date (UTC)", "DocumentNumber", "TotalAssets", "TransactionType", "SourceOrRecipient", "Destination", "Status" },
                    items.Select(i => new object?[] { i.Date, i.DocumentNumber, i.TotalAssets, i.TransactionType, i.SourceOrRecipient, i.Destination, i.Status }));
                return (bytes, $"transactions-{stamp}.xlsx");
            }
            case "audits":
            {
                var items = await audits.Filtered(ToAuditQuery(q)).OrderByDescending(a => a.AuditDate).Select(a => new AuditListItemDto(a.StockOpnameId, a.StockOpnameNumber, a.StockOpnameName,
                    a.AuditDate, a.StockOpnameType, a.StockOpnameStatus, a.BusinessUnit, a.BusUnitId, a.Details.Count(), a.Details.Count(d => d.PhysicalQuantity != d.SystemQuantity),
                    a.Details.Any() && a.Details.All(d => d.IsConfirmed && d.Finding == "Match"))).ToListAsync(ct);
                var bytes = ExcelHelper.Build("Audits", new[] { "StockOpnameNumber", "Name", "AuditDate (UTC)", "Type", "Status", "BusinessUnit", "Items", "Discrepancies" },
                    items.Select(i => new object?[] { i.StockOpnameNumber, i.StockOpnameName, i.AuditDate, i.StockOpnameType, i.StockOpnameStatus, i.BusinessUnit, i.ItemCount, i.DiscrepancyCount }));
                return (bytes, $"audits-{stamp}.xlsx");
            }
            default:
                throw new ValidationFailedException("type", "type must be one of: assets, transactions, audits.");
        }
    }

    private static GoodQuery ToGoodQuery(ReportQuery q) => new()
    {
        Page = q.Page, PageSize = q.PageSize, Search = q.Search, SortBy = q.SortBy, SortDir = q.SortDir,
        Status = q.Status, TransactionType = q.TransactionType, From = q.From, To = q.To
    };

    private static AuditQuery ToAuditQuery(ReportQuery q) => new()
    {
        Page = q.Page, PageSize = q.PageSize, Search = q.Search, SortBy = q.SortBy, SortDir = q.SortDir,
        Status = q.AuditStatus, From = q.From, To = q.To, BusUnitId = q.BusUnitId, BusinessUnit = q.BusinessUnit
    };
}
