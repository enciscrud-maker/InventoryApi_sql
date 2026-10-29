namespace Inventory.Api.Controllers;

[ApiController, Route("api/assets"), Authorize, Produces("application/json")]
public class AssetsController(AssetService assets, AssetImportService import) : ControllerBase
{
    private const long MaxImportBytes = 5 * 1024 * 1024;

    /// <summary>Paged list. Filters: catId, groupId, typeId, brandId, locationId, businessUnit, conditionId, assetType, assetStatus. Sort: name, prefix, createdDt, quantity, status.</summary>
    [HttpGet, Authorize(Policy = Permissions.AssetRead)]
    public async Task<ActionResult<PagedResult<AssetDto>>> List([FromQuery] AssetQuery q, CancellationToken ct) => Ok(await assets.ListAsync(q, ct));

    [HttpGet("{id:int}"), Authorize(Policy = Permissions.AssetRead)]
    public async Task<ActionResult<AssetDto>> Get(int id, CancellationToken ct) => Ok(await assets.GetAsync(id, ct));

    /// <summary>Creates an asset. Stock afterwards only changes through Goods In/Out.</summary>
    [HttpPost, Authorize(Policy = Permissions.AssetCreate)]
    public async Task<ActionResult<AssetDto>> Create(AssetRequest r, CancellationToken ct) => StatusCode(201, await assets.CreateAsync(r, ct));

    /// <summary>Updates an asset. Pass <c>rowVersion</c> from the last read for optimistic concurrency (409 on conflict). Quantity is ignored.</summary>
    [HttpPut("{id:int}"), Authorize(Policy = Permissions.AssetUpdate)]
    public async Task<ActionResult<AssetDto>> Update(int id, AssetRequest r, CancellationToken ct) => Ok(await assets.UpdateAsync(id, r, ct));

    [HttpPost("bulk-location"), Authorize(Policy = Permissions.AssetUpdate)]
    public async Task<ActionResult<BulkLocationResult>> BulkLocation(BulkLocationRequest r, CancellationToken ct) => Ok(await assets.BulkLocationAsync(r, ct));

    /// <summary>Imports assets from .xlsx or .csv (multipart field <c>file</c>). Valid rows are inserted in one transaction; invalid rows are reported.</summary>
    [HttpPost("import"), Authorize(Policy = Permissions.AssetImport), Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<ImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new BadRequestException("A non-empty file is required.");
        if (file.Length > MaxImportBytes) throw new BadRequestException("The file exceeds the 5 MB limit.");
        await using var stream = file.OpenReadStream();
        return Ok(await import.ImportAsync(stream, file.FileName, ct));
    }

    [HttpGet("export"), Authorize(Policy = Permissions.AssetExport)]
    public async Task<IActionResult> Export([FromQuery] AssetQuery q, CancellationToken ct) =>
        File(await assets.ExportAsync(q, ct), ExcelHelper.ContentType, $"assets-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");

    [HttpGet("import-template"), Authorize(Policy = Permissions.AssetRead)]
    public IActionResult ImportTemplate() => File(import.BuildTemplate(), ExcelHelper.ContentType, "asset-import-template.xlsx");
}
