namespace Inventory.Api.Controllers;

/// <summary>Stock opname (TR_StockOpname). Findings are recorded only; they never change MD_Asset.Quantity.</summary>
[ApiController, Route("api/stock-audits"), Authorize, Produces("application/json")]
public class AuditsController(AuditService svc) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.AuditRead)]
    public async Task<ActionResult<PagedResult<AuditListItemDto>>> List([FromQuery] AuditQuery q, CancellationToken ct) => Ok(await svc.ListAsync(q, ct));

    [HttpGet("{id:int}"), Authorize(Policy = Permissions.AuditRead)]
    public async Task<ActionResult<AuditDetailDto>> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    /// <summary>Deletes a Stock Opname session and its detail records, but never its referenced assets.</summary>
    [HttpDelete("{id:int}"), Authorize(Policy = Permissions.AuditDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }

    [HttpPost, Authorize(Policy = Permissions.AuditCreate)]
    public async Task<ActionResult<AuditDetailDto>> Create(AuditCreateRequest r, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(r, ct));

    [HttpPut("{id:int}"), Authorize(Policy = Permissions.AuditUpdate)]
    public async Task<ActionResult<AuditDetailDto>> Update(int id, AuditUpdateRequest r, CancellationToken ct) => Ok(await svc.UpdateAsync(id, r, ct));

    /// <summary>Replaces the audit items. SystemQuantity is set by the server from the current stock.</summary>
    [HttpPut("{id:int}/items"), Authorize(Policy = Permissions.AuditUpdate)]
    public async Task<ActionResult<AuditDetailDto>> ReplaceItems(int id, AuditItemsRequest r, CancellationToken ct) => Ok(await svc.ReplaceItemsAsync(id, r, ct));

    /// <summary>Uploads evidence (multipart field <c>file</c>; png, jpg, pdf; max 10 MB) for one item.</summary>
    [HttpPost("{id:int}/items/{itemId:int}/evidence"), Authorize(Policy = Permissions.AuditUpdate), Consumes("multipart/form-data")]
    [RequestSizeLimit(GoodService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<ActionResult<AuditItemDto>> AddEvidence(int id, int itemId, IFormFile file, CancellationToken ct)
    {
        if (file is null) throw new BadRequestException("A file is required.");
        await using var stream = file.OpenReadStream();
        return Ok(await svc.AddEvidenceAsync(id, itemId, file.FileName, file.Length, stream, ct));
    }

    [HttpGet("{id:int}/items/{itemId:int}/evidence/{evidenceId:int}"), Authorize(Policy = Permissions.AuditRead)]
    public async Task<IActionResult> GetEvidence(int id, int itemId, int evidenceId, CancellationToken ct)
    {
        if (evidenceId != 1) throw new NotFoundException($"Evidence {evidenceId}");
        var evidence = await svc.OpenEvidenceAsync(id, itemId, ct);
        return File(evidence.Content, evidence.ContentType);
    }

    [HttpDelete("{id:int}/items/{itemId:int}/evidence/{evidenceId:int}"), Authorize(Policy = Permissions.AuditUpdate)]
    public async Task<IActionResult> DeleteEvidence(int id, int itemId, int evidenceId, CancellationToken ct)
    {
        if (evidenceId != 1) throw new NotFoundException($"Evidence {evidenceId}");
        await svc.DeleteEvidenceAsync(id, itemId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/complete"), Authorize(Policy = Permissions.AuditApprove)]
    public async Task<ActionResult<AuditDetailDto>> Complete(int id, AuditCompleteRequest request, CancellationToken ct) =>
        Ok(await svc.CompleteAsync(id, request.UpdateLocation, ct));
}
