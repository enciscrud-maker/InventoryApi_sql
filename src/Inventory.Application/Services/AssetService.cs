namespace Inventory.Application.Services;

public class AssetService(IAppDbContext db, IHierarchyValidator hierarchy)
{
    public IQueryable<MdAsset> Filtered(AssetQuery q)
    {
        IQueryable<MdAsset> x = db.Assets.AsNoTracking()
            .Include(a => a.Category).Include(a => a.Group).Include(a => a.Type).Include(a => a.Brand)
            .Include(a => a.BusUnit).Include(a => a.Condition).Include(a => a.Status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            x = x.Where(a => a.AssetName.Contains(s) || a.AssetPrefix.Contains(s) || a.AssetCode.Contains(s));
        }
        if (q.CatId.HasValue) x = x.Where(a => a.CatId == q.CatId.Value);
        if (q.GroupId.HasValue) x = x.Where(a => a.GroupId == q.GroupId.Value);
        if (q.TypeId.HasValue) x = x.Where(a => a.TypeId == q.TypeId.Value);
        if (q.BrandId.HasValue) x = x.Where(a => a.BrandId == q.BrandId.Value);
        if (q.BusUnitId.HasValue) x = x.Where(a => a.BusUnitId == q.BusUnitId.Value);
        if (q.ConditionId.HasValue) x = x.Where(a => a.ConditionId == q.ConditionId.Value);
        if (!string.IsNullOrWhiteSpace(q.BusinessUnit)) { var bu = q.BusinessUnit.Trim(); x = x.Where(a => a.BusinessUnit == bu); }
        if (q.AssetType.HasValue) x = x.Where(a => a.AssetType == q.AssetType.Value);
        if (q.StatusId.HasValue) x = x.Where(a => a.StatusId == q.StatusId.Value);
        return x;
    }

    private static IQueryable<MdAsset> Sort(IQueryable<MdAsset> x, string? sortBy, bool desc) => sortBy?.ToLowerInvariant() switch
    {
        "name" or "assetname" => x.OrderByDir(a => a.AssetName, desc),
        "prefix" or "assetprefix" => x.OrderByDir(a => a.AssetPrefix, desc),
        "code" or "assetcode" => x.OrderByDir(a => a.AssetCode, desc),
        "createddt" or "created" => x.OrderByDir(a => a.CreatedDt, desc),
        "quantity" => x.OrderByDir(a => a.Quantity, desc),
        "status" => x.OrderByDir(a => a.Status.StatusName, desc),
        _ => x.OrderByDir(a => a.AssetId, desc)
    };

    public async Task<PagedResult<AssetDto>> ListAsync(AssetQuery q, CancellationToken ct)
    {
        var x = Sort(Filtered(q), q.SortBy, q.Desc);
        var total = await x.CountAsync(ct);
        var items = await x.Skip((q.SafePage - 1) * q.SafeSize).Take(q.SafeSize).ToListAsync(ct);
        return new PagedResult<AssetDto>(items.Select(ToDto).ToList(), q.SafePage, q.SafeSize, total);
    }

    public async Task<AssetDto> GetAsync(int id, CancellationToken ct)
    {
        var a = await Filtered(new AssetQuery()).FirstOrDefaultAsync(x => x.AssetId == id, ct) ?? throw new NotFoundException($"Asset {id}");
        return ToDto(a);
    }

    public async Task<AssetDto> CreateAsync(AssetRequest r, CancellationToken ct)
    {
        var status = await ValidateAsync(r, null, ct);
        var a = new MdAsset { CreatedDt = DateTime.UtcNow, Quantity = r.Quantity ?? 0 };
        Apply(a, r, status);
        db.Assets.Add(a);
        await db.SaveChangesAsync(ct);
        return await GetAsync(a.AssetId, ct);
    }

    /// <summary>Quantity is deliberately NOT updatable here: stock only moves through Goods In/Out.</summary>
    public async Task<AssetDto> UpdateAsync(int id, AssetRequest r, CancellationToken ct)
    {
        var a = await db.Assets.FirstOrDefaultAsync(x => x.AssetId == id, ct) ?? throw new NotFoundException($"Asset {id}");
        var status = await ValidateAsync(r, id, ct);
        if (!string.IsNullOrWhiteSpace(r.RowVersion))
        {
            try { db.Entry(a).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(r.RowVersion); }
            catch (FormatException) { throw new ValidationFailedException("rowVersion", "RowVersion is not valid base64."); }
        }
        Apply(a, r, status);
        a.UpdatedDt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); // DbUpdateConcurrencyException -> 409 via middleware
        return await GetAsync(id, ct);
    }

    public async Task<BulkBusinessUnitResult> BulkBusinessUnitAsync(BulkBusinessUnitRequest r, CancellationToken ct)
    {
        var ids = r.AssetIds.Distinct().ToList();
        var businessUnit = await db.BusinessUnits.AsNoTracking().FirstOrDefaultAsync(b => b.BusUnitId == r.BusUnitId, ct)
            ?? throw new ValidationFailedException("busUnitId", "Business unit does not exist.");
        var assets = await db.Assets.Where(a => ids.Contains(a.AssetId)).ToListAsync(ct);
        if (assets.Count != ids.Count)
        {
            var missing = ids.Except(assets.Select(a => a.AssetId));
            throw new ValidationFailedException("assetIds", $"Assets not found: {string.Join(", ", missing)}.");
        }
        var now = DateTime.UtcNow;
        foreach (var a in assets)
        {
            a.BusUnitId = businessUnit.BusUnitId;
            if (!string.IsNullOrWhiteSpace(r.BusinessUnit)) a.BusinessUnit = r.BusinessUnit.Trim();
            a.UpdatedDt = now;
        }
        await db.SaveChangesAsync(ct);
        return new BulkBusinessUnitResult(assets.Count);
    }

    public async Task<byte[]> ExportAsync(AssetQuery q, CancellationToken ct)
    {
        var items = await Sort(Filtered(q), q.SortBy, q.Desc).ToListAsync(ct);
        return ExcelHelper.Build("Assets", new[]
        {
            "AssetId", "AssetPrefix", "AssetCode", "AssetName", "Category", "Group", "Type", "Brand", "AssetType", "AssetStatus", "Quantity", "UnitOfMeasure",
            "SerialNumber", "AssetTag", "PurchaseOrderNumber", "SapMaterialNumber", "AssetValue", "Condition", "BusinessUnit", "CreatedDt (UTC)"
        }, items.Select(a => new object?[]
        {
            a.AssetId, a.AssetPrefix, a.AssetCode, a.AssetName, a.Category.CatName, a.Group.GroupName, a.Type.TypeName, a.Brand.BrandName, a.AssetType, a.Status.StatusName,
            a.Quantity, a.UnitOfMeasure, a.SerialNumber, a.AssetTag, a.PurchaseOrderNumber, a.SapMaterialNumber, a.AssetValue,
            a.Condition?.ConditionName, a.BusinessUnit, a.CreatedDt
        }));
    }

    private async Task<MdAssetStatus> ValidateAsync(AssetRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        await hierarchy.ValidateAsync(r.CatId, r.GroupId, r.TypeId, r.BrandId, ct);
        var prefix = r.AssetPrefix.Trim();
        var code = r.AssetCode.Trim();
        if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ValidationFailedException("assetCode", "AssetCode must start with AssetPrefix.");
        if (await db.Assets.AnyAsync(a => a.AssetCode == code && a.AssetId != self, ct))
            throw new ConflictException($"AssetCode '{code}' already exists.");
        if (!string.IsNullOrWhiteSpace(r.SerialNumber) && await db.Assets.AnyAsync(a => a.SerialNumber == r.SerialNumber && a.AssetId != self, ct))
            throw new ConflictException($"SerialNumber '{r.SerialNumber}' already exists.");
        if (r.BusUnitId.HasValue && !await db.BusinessUnits.AnyAsync(b => b.BusUnitId == r.BusUnitId.Value, ct))
            throw new ValidationFailedException("busUnitId", "Business unit does not exist.");
        if (r.LocationId.HasValue)
        {
            var location = await db.Locations.AsNoTracking()
                .FirstOrDefaultAsync(item => item.LocationId == r.LocationId.Value, ct)
                ?? throw new ValidationFailedException("locationId", "Location does not exist.");
            if (r.BusUnitId != location.BusUnitId)
                throw new ValidationFailedException("locationId", "Location must belong to the selected Business Unit.");
        }
        if (r.ModelId.HasValue && !await db.Models.AnyAsync(model => model.ModelId == r.ModelId.Value, ct))
            throw new ValidationFailedException("modelId", "Model does not exist.");
        var status = await db.AssetStatuses.FirstOrDefaultAsync(s => s.StatusId == r.StatusId, ct);
        if (status is null)
            throw new ValidationFailedException("statusId", "Asset status does not exist.");
        if (r.ConditionId.HasValue && !await db.Conditions.AnyAsync(c => c.ConditionId == r.ConditionId.Value, ct))
            throw new ValidationFailedException("conditionId", "Condition does not exist.");
        return status;
    }

    private static void Apply(MdAsset a, AssetRequest r, MdAssetStatus status)
    {
        a.CatId = r.CatId; a.GroupId = r.GroupId; a.TypeId = r.TypeId; a.BrandId = r.BrandId;
        a.AssetName = r.AssetName.Trim(); a.AssetType = r.AssetType; a.StatusId = status.StatusId;
        a.AssetStatus = status.StatusName; a.AssetPrefix = r.AssetPrefix.Trim(); a.AssetCode = r.AssetCode.Trim();
        a.UnitOfMeasure = r.UnitOfMeasure?.Trim();
        a.SerialNumber = string.IsNullOrWhiteSpace(r.SerialNumber) ? null : r.SerialNumber.Trim();
        a.AssetTag = r.AssetTag?.Trim(); a.PurchaseOrderNumber = r.PurchaseOrderNumber?.Trim(); a.SapMaterialNumber = r.SapMaterialNumber?.Trim();
        a.AssetValue = r.AssetValue; a.Description = r.Description?.Trim();
        a.BusUnitId = r.BusUnitId; a.LocationId = r.LocationId; a.ModelId = r.ModelId;
        a.ConditionId = r.ConditionId; a.BusinessUnit = r.BusinessUnit?.Trim();
    }

    public static AssetDto ToDto(MdAsset a) => new AssetDto(a.AssetId, a.CatId, a.Category.CatName, a.GroupId, a.Group.GroupName, a.TypeId, a.Type.TypeName,
        a.BrandId, a.Brand.BrandName, a.AssetName, a.AssetType, a.StatusId, a.Status.StatusName, a.AssetPrefix, a.CreatedDt, a.Quantity, a.UnitOfMeasure, a.SerialNumber,
        a.AssetTag, a.PurchaseOrderNumber, a.SapMaterialNumber, a.AssetValue, a.Description, a.BusUnitId,
        a.ConditionId, a.Condition?.ConditionName, a.BusinessUnit, a.UpdatedDt, Convert.ToBase64String(a.RowVersion))
    {
        AssetCode = a.AssetCode,
        LocationId = a.LocationId,
        LocationName = a.Location?.LocationName,
        ModelId = a.ModelId,
        ModelName = a.Model?.ModelName
    };
}
