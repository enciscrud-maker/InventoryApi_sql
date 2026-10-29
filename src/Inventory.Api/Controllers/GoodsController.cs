namespace Inventory.Api.Controllers;

/// <summary>
/// Goods In / Goods Out (TR_Good). The creator is always taken from the JWT. Creating a document applies the stock adjustment exactly once.
/// Documents are read-only after submit: only the status endpoint changes them.
/// </summary>
[ApiController, Authorize, Produces("application/json")]
public abstract class GoodsControllerBase(GoodService svc, GoodType type) : ControllerBase
{
    /// <summary>List: Date, DocumentNumber, TotalAssets (sum of item quantity), TransactionType, SourceOrRecipient, Destination, Status.</summary>
    [HttpGet, Authorize(Policy = Permissions.GoodRead)]
    public async Task<ActionResult<PagedResult<GoodListItemDto>>> List([FromQuery] GoodQuery q, CancellationToken ct) => Ok(await svc.ListAsync(type, q, ct));

    [HttpGet("{id:int}"), Authorize(Policy = Permissions.GoodRead)]
    public async Task<ActionResult<GoodDetailDto>> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(type, id, ct));

    /// <summary>Submits a document. 409 when requestId was already processed or the stock row changed concurrently; 400 when Goods Out exceeds available stock.</summary>
    [HttpPost, Authorize(Policy = Permissions.GoodCreate)]
    public async Task<ActionResult<GoodDetailDto>> Create(GoodCreateRequest r, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(type, r, ct));

    /// <summary>Changes GoodStatus (InProgress | Completed) and appends one immutable history row. Never re-applies stock.</summary>
    [HttpPatch("{id:int}/status"), Authorize(Policy = Permissions.GoodStatus)]
    public async Task<ActionResult<GoodDetailDto>> UpdateStatus(int id, GoodStatusRequest r, CancellationToken ct) => Ok(await svc.UpdateStatusAsync(type, id, r, ct));

    /// <summary>Uploads an attachment (multipart field <c>file</c>; pdf, png, jpg, doc(x), xls(x); max 10 MB).</summary>
    [HttpPost("{id:int}/attachments"), Authorize(Policy = Permissions.GoodCreate), Consumes("multipart/form-data")]
    [RequestSizeLimit(GoodService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<ActionResult<GoodAttachmentDto>> AddAttachment(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null) throw new BadRequestException("A file is required.");
        await using var stream = file.OpenReadStream();
        return StatusCode(201, await svc.AddAttachmentAsync(type, id, file.FileName, file.ContentType, file.Length, stream, ct));
    }
}

[Route("api/goods-in")] public class GoodsInController(GoodService svc) : GoodsControllerBase(svc, GoodType.In);
[Route("api/goods-out")] public class GoodsOutController(GoodService svc) : GoodsControllerBase(svc, GoodType.Out);
