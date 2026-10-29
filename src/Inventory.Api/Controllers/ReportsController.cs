namespace Inventory.Api.Controllers;

[ApiController, Authorize, Produces("application/json")]
public class DashboardController(DashboardService svc) : ControllerBase
{
    [HttpGet("api/dashboard/summary"), Authorize(Policy = Permissions.DashboardRead)]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken ct) => Ok(await svc.GetSummaryAsync(ct));
}

[ApiController, Route("api/reports"), Authorize(Policy = Permissions.ReportRead), Produces("application/json")]
public class ReportsController(ReportService svc) : ControllerBase
{
    [HttpGet("assets")]
    public async Task<ActionResult<PagedResult<AssetDto>>> Assets([FromQuery] ReportQuery q, CancellationToken ct) => Ok(await svc.AssetsAsync(q, ct));

    /// <summary>Goods In + Out. Use goodType=In|Out, status, transactionType, from, to to filter.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<GoodListItemDto>>> Transactions([FromQuery] ReportQuery q, CancellationToken ct) => Ok(await svc.TransactionsAsync(q, ct));

    [HttpGet("audits")]
    public async Task<ActionResult<PagedResult<AuditListItemDto>>> Audits([FromQuery] ReportQuery q, CancellationToken ct) => Ok(await svc.AuditsAsync(q, ct));

    /// <summary>Excel export. type = assets | transactions | audits (same filters as the matching report).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ReportQuery q, CancellationToken ct)
    {
        var (content, name) = await svc.ExportAsync(q, ct);
        return File(content, ExcelHelper.ContentType, name);
    }
}
