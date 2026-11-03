namespace Inventory.Application.Services;

public class AuditService(IAppDbContext db, ICurrentUser current, IFileStorage storage)
{
    private static readonly string[] EvidenceExtensions = { ".png", ".jpg", ".jpeg", ".pdf" };

    public IQueryable<TrStockOpname> Filtered(AuditQuery q)
    {
        var x = db.StockOpnames.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search)) { var s = q.Search.Trim(); x = x.Where(a => a.StockOpnameNumber.Contains(s) || a.StockOpnameName.Contains(s)); }
        if (!string.IsNullOrWhiteSpace(q.Status)) { var st = q.Status.Trim(); x = x.Where(a => a.StockOpnameStatus == st); }
        if (q.BusUnitId.HasValue) x = x.Where(a => a.BusUnitId == q.BusUnitId.Value);
        if (!string.IsNullOrWhiteSpace(q.BusinessUnit)) { var bu = q.BusinessUnit.Trim(); x = x.Where(a => a.BusinessUnit == bu); }
        if (q.From.HasValue) { var f = Time.ToUtc(q.From.Value); x = x.Where(a => a.AuditDate >= f); }
        if (q.To.HasValue) { var t = Time.ToUtc(q.To.Value); x = x.Where(a => a.AuditDate <= t); }
        return x;
    }

    public async Task<PagedResult<AuditListItemDto>> ListAsync(AuditQuery q, CancellationToken ct)
    {
        var x = Filtered(q);
        var desc = q.SortBy is null || q.Desc;
        IQueryable<TrStockOpname> sorted = q.SortBy?.ToLowerInvariant() switch
        {
            "number" => x.OrderByDir(a => a.StockOpnameNumber, desc),
            "status" => x.OrderByDir(a => a.StockOpnameStatus, desc),
            _ => x.OrderByDir(a => a.AuditDate, desc).ThenByDescending(a => a.StockOpnameId)
        };
        return await sorted.Select(a => new AuditListItemDto(a.StockOpnameId, a.StockOpnameNumber, a.StockOpnameName, a.AuditDate, a.StockOpnameType, a.StockOpnameStatus,
            a.BusinessUnit, a.BusUnitId, a.Details.Count(), a.Details.Count(d => d.PhysicalQuantity != d.SystemQuantity))).ToPagedAsync(q, ct);
    }

    public async Task<AuditDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var a = await db.StockOpnames.AsNoTracking().Include(x => x.Details).ThenInclude(d => d.Asset)
            .FirstOrDefaultAsync(x => x.StockOpnameId == id, ct) ?? throw new NotFoundException($"Stock audit {id}");
        return ToDto(a);
    }

    public async Task<AuditDetailDto> CreateAsync(AuditCreateRequest r, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new AppException(401, "Unauthorized", "Authenticated user is required.");
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == r.AssetId, ct) ?? throw new ValidationFailedException("assetId", "Asset does not exist.");
        var status = NormalizeStatus(r.StockOpnameStatus) ?? StockOpnameStatuses.Draft;
        await EnsureBusinessUnitAsync(r.BusUnitId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = N'TR_StockOpname_Number', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000", ct);
        var now = DateTime.UtcNow;
        var stem = $"SO-{now:yyyyMMdd}-";
        var last = await db.StockOpnames.Where(a => a.StockOpnameNumber.StartsWith(stem)).OrderByDescending(a => a.StockOpnameNumber).Select(a => a.StockOpnameNumber).FirstOrDefaultAsync(ct);
        var seq = last is null ? 1 : int.Parse(last[stem.Length..]) + 1;

        var audit = new TrStockOpname
        {
            UserId = userId, AssetId = asset.AssetId, CategoryId = asset.CatId, BrandId = asset.BrandId, GroupId = asset.GroupId, TypeId = asset.TypeId,
            StockOpnameName = r.StockOpnameName.Trim(), StockOpnameType = r.StockOpnameType, StockOpnameStatus = status, StockOpnamePrefix = "SO",
            CreatedDt = now, StockOpnameNumber = $"{stem}{seq:D4}", AuditDate = Time.ToUtc(r.AuditDate), BusinessUnit = r.BusinessUnit?.Trim(), BusUnitId = r.BusUnitId
        };
        db.StockOpnames.Add(audit);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(audit.StockOpnameId, ct);
    }

    public async Task<AuditDetailDto> UpdateAsync(int id, AuditUpdateRequest r, CancellationToken ct)
    {
        var a = await db.StockOpnames.FirstOrDefaultAsync(x => x.StockOpnameId == id, ct) ?? throw new NotFoundException($"Stock audit {id}");
        var status = NormalizeStatus(r.StockOpnameStatus) ?? throw new ValidationFailedException("stockOpnameStatus", $"Status must be one of: {string.Join(", ", StockOpnameStatuses.All)}.");
        if (a.StockOpnameStatus == StockOpnameStatuses.Completed && status == StockOpnameStatuses.Completed)
            throw new ConflictException("A completed audit is read-only.");
        await EnsureBusinessUnitAsync(r.BusUnitId, ct);
        a.StockOpnameName = r.StockOpnameName.Trim(); a.AuditDate = Time.ToUtc(r.AuditDate); a.BusinessUnit = r.BusinessUnit?.Trim();
        a.BusUnitId = r.BusUnitId; a.StockOpnameStatus = status; a.UpdatedDt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>PUT semantics: the submitted list replaces the audit's items. SystemQuantity is always taken from the server.</summary>
    public async Task<AuditDetailDto> ReplaceItemsAsync(int id, AuditItemsRequest r, CancellationToken ct)
    {
        var audit = await db.StockOpnames.Include(x => x.Details).FirstOrDefaultAsync(x => x.StockOpnameId == id, ct) ?? throw new NotFoundException($"Stock audit {id}");
        if (audit.StockOpnameStatus == StockOpnameStatuses.Completed) throw new ConflictException("A completed audit is read-only.");

        var assetIds = r.Items.Select(i => i.AssetId).Distinct().ToList();
        var assets = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.AssetId)).ToDictionaryAsync(a => a.AssetId, ct);
        var missing = assetIds.Except(assets.Keys).ToList();
        if (missing.Count > 0) throw new ValidationFailedException("items", $"Assets not found: {string.Join(", ", missing)}.");
        var busUnitIds = r.Items.Where(i => i.ActualBusUnitId.HasValue).Select(i => i.ActualBusUnitId!.Value).Distinct().ToList();
        if (busUnitIds.Count > 0 && await db.BusinessUnits.CountAsync(b => busUnitIds.Contains(b.BusUnitId), ct) != busUnitIds.Count)
            throw new ValidationFailedException("items", "One or more actual business units do not exist.");

        var keepIds = r.Items.Where(i => i.StockOpnameDetailId.HasValue).Select(i => i.StockOpnameDetailId!.Value).ToHashSet();
        var unknown = keepIds.Except(audit.Details.Select(d => d.StockOpnameDetailId)).ToList();
        if (unknown.Count > 0) throw new ValidationFailedException("items", $"Item ids not part of this audit: {string.Join(", ", unknown)}.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.StockOpnameDetails.RemoveRange(audit.Details.Where(d => !keepIds.Contains(d.StockOpnameDetailId)).ToList());
        foreach (var i in r.Items)
        {
            TrStockOpnameDetail d;
            if (i.StockOpnameDetailId.HasValue)
            {
                d = audit.Details.First(x => x.StockOpnameDetailId == i.StockOpnameDetailId.Value);
                if (d.AssetId != i.AssetId) throw new ValidationFailedException("items", $"Item {d.StockOpnameDetailId}: the asset of an existing item cannot be changed.");
            }
            else
            {
                d = new TrStockOpnameDetail { StockOpnameId = id, AssetId = i.AssetId, SystemQuantity = assets[i.AssetId].Quantity };
                db.StockOpnameDetails.Add(d);
            }
            d.PhysicalQuantity = i.PhysicalQuantity; d.ActualBusUnitId = i.ActualBusUnitId; d.IsConfirmed = i.IsConfirmed; d.Notes = i.Notes;
            var variance = d.PhysicalQuantity - d.SystemQuantity;
            d.Finding = string.IsNullOrWhiteSpace(i.Finding) ? (variance == 0 ? "Match" : variance < 0 ? "Shortage" : "Overage") : i.Finding.Trim();
        }
        audit.UpdatedDt = DateTime.UtcNow;
        if (audit.StockOpnameStatus == StockOpnameStatuses.Draft) audit.StockOpnameStatus = StockOpnameStatuses.InProgress;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AuditItemDto> AddEvidenceAsync(int id, int itemId, string fileName, long size, Stream content, CancellationToken ct)
    {
        var audit = await db.StockOpnames.AsNoTracking().FirstOrDefaultAsync(a => a.StockOpnameId == id, ct) ?? throw new NotFoundException($"Stock audit {id}");
        if (audit.StockOpnameStatus == StockOpnameStatuses.Completed) throw new ConflictException("A completed audit is read-only.");
        var item = await db.StockOpnameDetails.Include(d => d.Asset).FirstOrDefaultAsync(d => d.StockOpnameDetailId == itemId && d.StockOpnameId == id, ct)
            ?? throw new NotFoundException($"Audit item {itemId}");
        var ext = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        if (!EvidenceExtensions.Contains(ext)) throw new ValidationFailedException("file", $"File type '{ext}' is not allowed (png, jpg, pdf).");
        if (size <= 0 || size > GoodService.MaxAttachmentBytes) throw new ValidationFailedException("file", "File must be between 1 byte and 10 MB.");
        item.EvidencePath = await storage.SaveAsync($"audits/{id}", $"{itemId}-{Guid.NewGuid():N}{ext}", content, ct);
        await db.SaveChangesAsync(ct);
        return ToItem(item);
    }

    private async Task EnsureBusinessUnitAsync(int? busUnitId, CancellationToken ct)
    {
        if (busUnitId.HasValue && !await db.BusinessUnits.AnyAsync(b => b.BusUnitId == busUnitId.Value, ct))
            throw new ValidationFailedException("busUnitId", "Business unit does not exist.");
    }

    private static string? NormalizeStatus(string? s) => string.IsNullOrWhiteSpace(s) ? null : StockOpnameStatuses.All.FirstOrDefault(x => string.Equals(x, s.Trim(), StringComparison.OrdinalIgnoreCase));

    private static AuditItemDto ToItem(TrStockOpnameDetail d) => new(d.StockOpnameDetailId, d.AssetId, d.Asset.AssetName, d.Asset.AssetPrefix, d.SystemQuantity, d.PhysicalQuantity,
        d.PhysicalQuantity - d.SystemQuantity, d.ActualBusUnitId, d.Finding, d.IsConfirmed, d.Notes, d.EvidencePath);

    private static AuditDetailDto ToDto(TrStockOpname a) => new(a.StockOpnameId, a.StockOpnameNumber, a.StockOpnameName, a.AuditDate, a.StockOpnameType, a.StockOpnameStatus,
        a.StockOpnamePrefix, a.AssetId, a.UserId, a.BusinessUnit, a.BusUnitId, a.CreatedDt, a.UpdatedDt, a.Details.OrderBy(d => d.StockOpnameDetailId).Select(ToItem).ToList());
}
