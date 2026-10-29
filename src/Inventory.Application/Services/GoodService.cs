namespace Inventory.Application.Services;

public class GoodService(IAppDbContext db, ICurrentUser current, IFileStorage storage)
{
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx" };

    public static readonly Expression<Func<TrGood, GoodListItemDto>> ListProjection = g => new GoodListItemDto(
        g.GoodId, g.RequestDate, g.GoodNumber, g.Details.Sum(d => d.Quantity), g.TransactionType, g.PersonName, g.DestinationLocation, g.GoodStatus);

    public IQueryable<TrGood> Filtered(GoodType? type, GoodQuery q)
    {
        var x = db.Goods.AsNoTracking().AsQueryable();
        if (type.HasValue) x = x.Where(g => g.GoodType == type.Value);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            x = x.Where(g => g.GoodNumber.Contains(s) || g.GoodName.Contains(s) || g.PersonName.Contains(s) || g.DestinationLocation.Contains(s));
        }
        if (q.Status.HasValue) x = x.Where(g => g.GoodStatus == q.Status.Value);
        if (q.TransactionType.HasValue) x = x.Where(g => g.TransactionType == q.TransactionType.Value);
        if (q.From.HasValue) { var f = Time.ToUtc(q.From.Value); x = x.Where(g => g.RequestDate >= f); }
        if (q.To.HasValue) { var t = Time.ToUtc(q.To.Value); x = x.Where(g => g.RequestDate <= t); }
        return x;
    }

    public async Task<PagedResult<GoodListItemDto>> ListAsync(GoodType? type, GoodQuery q, CancellationToken ct)
    {
        var x = Filtered(type, q);
        var desc = q.SortBy is null || q.Desc; // newest first by default
        IQueryable<TrGood> sorted = q.SortBy?.ToLowerInvariant() switch
        {
            "documentnumber" => x.OrderByDir(g => g.GoodNumber, desc),
            "status" => x.OrderByDir(g => g.GoodStatus, desc),
            "transactiontype" => x.OrderByDir(g => g.TransactionType, desc),
            _ => x.OrderByDir(g => g.RequestDate, desc).ThenByDescending(g => g.GoodId)
        };
        return await sorted.Select(ListProjection).ToPagedAsync(q, ct);
    }

    public async Task<GoodDetailDto> GetAsync(GoodType? type, int id, CancellationToken ct)
    {
        var g = await db.Goods.AsNoTracking().AsSplitQuery()
            .Include(x => x.Details).ThenInclude(d => d.Asset)
            .Include(x => x.Attachments).Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.GoodId == id && (type == null || x.GoodType == type), ct)
            ?? throw new NotFoundException($"Transaction {id}");
        return ToDto(g);
    }

    public async Task<GoodDetailDto> CreateAsync(GoodType type, GoodCreateRequest r, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new AppException(401, "Unauthorized", "Authenticated user is required.");
        if (TransactionRules.DirectionOf(r.TransactionType) != type)
            throw new ValidationFailedException("transactionType", $"Transaction type '{r.TransactionType}' is not valid for Goods {type}.");
        if (type == GoodType.Out)
        {
            var bad = r.Items.Select((i, idx) => (i, idx)).Where(x =>
                string.IsNullOrWhiteSpace(x.i.RequestJustification) || string.IsNullOrWhiteSpace(x.i.RequiredDuration) || string.IsNullOrWhiteSpace(x.i.IntendedPurpose)).ToList();
            if (bad.Count > 0)
                throw new ValidationFailedException(bad.ToDictionary(x => $"items[{x.idx}]", x => new[] { "requestJustification, requiredDuration and intendedPurpose are required for Goods Out items." }));
        }

        // Rule 12: reject duplicate submissions (idempotency key).
        if (await db.Goods.AnyAsync(g => g.RequestId == r.RequestId, ct))
            throw new ConflictException($"Duplicate submission: requestId '{r.RequestId}' has already been processed.");

        var assetIds = r.Items.Select(i => i.AssetId).Distinct().ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var assets = await db.Assets.Where(a => assetIds.Contains(a.AssetId)).ToDictionaryAsync(a => a.AssetId, ct);
        var errors = new Dictionary<string, string[]>();
        var missing = assetIds.Except(assets.Keys).ToList();
        if (missing.Count > 0) errors["items"] = new[] { $"Assets not found: {string.Join(", ", missing)}." };

        var totals = r.Items.GroupBy(i => i.AssetId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        foreach (var (assetId, qty) in totals)
        {
            if (!assets.TryGetValue(assetId, out var a)) continue;
            if (type == GoodType.Out && qty > a.Quantity)
                errors[$"items.assetId[{assetId}]"] = new[] { $"Requested quantity {qty} exceeds available stock {a.Quantity} for asset '{a.AssetPrefix}'." };
        }
        if (errors.Count > 0) throw new ValidationFailedException(errors);

        int? disposalConditionId = null;
        if (r.TransactionType == TransactionType.Disposal)
        {
            disposalConditionId = await db.Conditions.Where(c => c.ConditionCode == ConditionCodes.Disposal).Select(c => (int?)c.ConditionId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Asset condition '{ConditionCodes.Disposal}' is not seeded.");
        }

        var now = DateTime.UtcNow;
        // Rule 8: stock applied exactly once, at creation time.
        foreach (var (assetId, qty) in totals)
        {
            var a = assets[assetId];
            a.Quantity += type == GoodType.In ? qty : -qty;
            a.UpdatedDt = now;
            if (disposalConditionId.HasValue) a.ConditionId = disposalConditionId;
        }

        // Serialize document numbering for the duration of the transaction (assets above were loaded earlier,
        // so a concurrent issue on the same asset surfaces as a RowVersion conflict -> 409).
        await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource = N'TR_Good_Number', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000", ct);
        var prefix = type == GoodType.In ? "GI" : "GO";
        var stem = $"{prefix}-{now:yyyyMMdd}-";
        var last = await db.Goods.Where(g => g.GoodNumber.StartsWith(stem)).OrderByDescending(g => g.GoodNumber).Select(g => g.GoodNumber).FirstOrDefaultAsync(ct);
        var seq = last is null ? 1 : int.Parse(last[stem.Length..]) + 1;

        var first = assets[r.Items[0].AssetId]; // header classification is derived from the first item's master data, never from the client
        var good = new TrGood
        {
            UserId = userId, AssetId = first.AssetId, CatId = first.CatId, BrandId = first.BrandId, GroupId = first.GroupId, TypeId = first.TypeId,
            GoodName = r.GoodName.Trim(), GoodType = type, GoodStatus = GoodStatus.InProgress, GoodPrefix = prefix, CreatedDt = now,
            GoodNumber = $"{stem}{seq:D4}", RequestId = r.RequestId.Trim(), TransactionType = r.TransactionType, BusinessUnit = r.BusinessUnit.Trim(),
            RequestDate = Time.ToUtc(r.RequestDate), FormIssueDate = Time.ToUtc(r.FormIssueDate),
            PersonName = r.PersonName.Trim(), PersonNik = r.PersonNik.Trim(), PersonDepartment = r.PersonDepartment.Trim(),
            DestinationLocation = r.DestinationLocation.Trim(), RequestNumber = r.RequestNumber?.Trim(), ApproverName = r.ApproverName.Trim(),
            InventoryNotes = r.InventoryNotes?.Trim(), InventoryAdjustmentApplied = true, InventoryAdjustedAt = now,
            Details = r.Items.Select(i => new TrGoodDetail
            {
                AssetId = i.AssetId, CreatedDt = now, Quantity = i.Quantity, SerialNumber = i.SerialNumber, ReferenceNumber = i.ReferenceNumber,
                AssetInformation = i.AssetInformation, Condition = i.Condition, Remarks = i.Remarks, Specifications = i.Specifications,
                RequestJustification = type == GoodType.Out ? i.RequestJustification : null,
                RequiredDuration = type == GoodType.Out ? i.RequiredDuration : null,
                IntendedPurpose = type == GoodType.Out ? i.IntendedPurpose : null
            }).ToList()
        };
        db.Goods.Add(good);
        await db.SaveChangesAsync(ct);   // header + details + stock + marker in ONE transaction (rule 10)
        await tx.CommitAsync(ct);
        return await GetAsync(type, good.GoodId, ct);
    }

    /// <summary>Rules 3, 4, 9: status + exactly one history row in one transaction; no stock re-application.</summary>
    public async Task<GoodDetailDto> UpdateStatusAsync(GoodType type, int id, GoodStatusRequest r, CancellationToken ct)
    {
        var actorId = current.UserId ?? throw new AppException(401, "Unauthorized", "Authenticated user is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var good = await db.Goods.FirstOrDefaultAsync(g => g.GoodId == id && g.GoodType == type, ct) ?? throw new NotFoundException($"Transaction {id}");
        if (good.GoodStatus == r.Status) return await GetAsync(type, id, ct); // unchanged -> no history row

        var actor = await db.Users.Include(u => u.Employee).FirstAsync(u => u.UserId == actorId, ct);
        db.GoodStatusHistories.Add(new TrGoodStatusHistory
        {
            GoodId = good.GoodId, PreviousStatus = good.GoodStatus, NewStatus = r.Status, ActorUserId = actor.UserId,
            ActorNameSnapshot = actor.Employee.EmployeeName, ActorNikSnapshot = actor.Employee.EmployeeNik,
            ChangedAt = DateTime.UtcNow, Notes = r.Notes?.Trim()
        });
        good.GoodStatus = r.Status;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(type, id, ct);
    }

    public async Task<GoodAttachmentDto> AddAttachmentAsync(GoodType type, int id, string fileName, string? contentType, long size, Stream content, CancellationToken ct)
    {
        if (!await db.Goods.AnyAsync(g => g.GoodId == id && g.GoodType == type, ct)) throw new NotFoundException($"Transaction {id}");
        var original = Path.GetFileName(fileName);
        var ext = Path.GetExtension(original).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new ValidationFailedException("file", $"File type '{ext}' is not allowed.");
        if (size <= 0 || size > MaxAttachmentBytes) throw new ValidationFailedException("file", "File must be between 1 byte and 10 MB.");

        var stored = $"{Guid.NewGuid():N}{ext}";
        var path = await storage.SaveAsync($"goods/{id}", stored, content, ct);
        var att = new TrGoodAttachment
        {
            GoodId = id, OriginalFileName = original, StoredFileName = stored, ContentType = contentType ?? "application/octet-stream",
            FileSize = size, StoragePath = path, UploadedAt = DateTime.UtcNow
        };
        db.GoodAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        return new GoodAttachmentDto(att.GoodAttachmentId, att.OriginalFileName, att.ContentType, att.FileSize, att.StoragePath, att.UploadedAt);
    }

    private static GoodDetailDto ToDto(TrGood g) => new(
        g.GoodId, g.GoodNumber, g.GoodName, g.GoodType, g.TransactionType, g.GoodStatus, g.GoodPrefix, g.CreatedDt, g.RequestId, g.BusinessUnit,
        g.RequestDate, g.FormIssueDate, g.PersonName, g.PersonNik, g.PersonDepartment, g.DestinationLocation, g.RequestNumber, g.ApproverName,
        g.InventoryNotes, g.InventoryAdjustmentApplied, g.InventoryAdjustedAt, g.UserId, g.Details.Sum(d => d.Quantity),
        g.Details.OrderBy(d => d.GoodDetailId).Select(d => new GoodItemDto(d.GoodDetailId, d.AssetId, d.Asset.AssetName, d.Asset.AssetPrefix, d.Quantity, d.SerialNumber,
            d.ReferenceNumber, d.AssetInformation, d.Condition, d.Remarks, d.Specifications, d.RequestJustification, d.RequiredDuration, d.IntendedPurpose)).ToList(),
        g.Attachments.OrderBy(a => a.GoodAttachmentId).Select(a => new GoodAttachmentDto(a.GoodAttachmentId, a.OriginalFileName, a.ContentType, a.FileSize, a.StoragePath, a.UploadedAt)).ToList(),
        g.StatusHistory.OrderBy(h => h.GoodStatusHistoryId).Select(h => new GoodStatusHistoryDto(h.GoodStatusHistoryId, h.PreviousStatus, h.NewStatus, h.ActorUserId,
            h.ActorNameSnapshot, h.ActorNikSnapshot, h.ChangedAt, h.Notes)).ToList(),
        Convert.ToBase64String(g.RowVersion));
}
