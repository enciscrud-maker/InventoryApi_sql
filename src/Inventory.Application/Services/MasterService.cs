namespace Inventory.Application.Services;

public interface IMasterService<TReq, TDto>
{
    Task<PagedResult<TDto>> ListAsync(PageQuery query, CancellationToken ct);
    Task<TDto> GetAsync(int id, CancellationToken ct);
    Task<TDto> CreateAsync(TReq request, CancellationToken ct);
    Task<TDto> UpdateAsync(int id, TReq request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public abstract class MasterService<TEntity, TReq, TDto>(IAppDbContext db, IHierarchyValidator hierarchy) : IMasterService<TReq, TDto>
    where TEntity : class
{
    protected IAppDbContext Db => db;
    protected IHierarchyValidator Hierarchy => hierarchy;

    protected abstract DbSet<TEntity> Set { get; }
    protected abstract string Label { get; }
    protected abstract IQueryable<TEntity> ApplySearch(IQueryable<TEntity> q, string s);
    protected abstract IQueryable<TEntity> ApplySort(IQueryable<TEntity> q, string? sortBy, bool desc);
    protected abstract TDto ToDto(TEntity e);
    protected abstract TEntity Create();
    protected abstract Task ApplyAsync(TEntity e, TReq r, int? id, CancellationToken ct);
    protected abstract Task<bool> IsReferencedAsync(int id, CancellationToken ct);

    public async Task<PagedResult<TDto>> ListAsync(PageQuery p, CancellationToken ct)
    {
        IQueryable<TEntity> q = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(p.Search)) q = ApplySearch(q, p.Search.Trim());
        q = ApplySort(q, p.SortBy?.ToLowerInvariant(), p.Desc);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((p.SafePage - 1) * p.SafeSize).Take(p.SafeSize).ToListAsync(ct);
        return new PagedResult<TDto>(items.Select(ToDto).ToList(), p.SafePage, p.SafeSize, total);
    }

    public async Task<TDto> GetAsync(int id, CancellationToken ct) => ToDto(await FindAsync(id, ct));

    public async Task<TDto> CreateAsync(TReq r, CancellationToken ct)
    {
        var e = Create();
        await ApplyAsync(e, r, null, ct);
        Set.Add(e);
        await Db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<TDto> UpdateAsync(int id, TReq r, CancellationToken ct)
    {
        var e = await FindAsync(id, ct);
        await ApplyAsync(e, r, id, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var e = await FindAsync(id, ct);
        if (await IsReferencedAsync(id, ct))
            throw new ConflictException($"{Label} is still referenced by other data and cannot be deleted.");
        Set.Remove(e);
        await Db.SaveChangesAsync(ct);
    }

    private async Task<TEntity> FindAsync(int id, CancellationToken ct) =>
        await Set.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException($"{Label} {id}");

    protected static void Unique(bool exists, string field, string value)
    {
        if (exists) throw new ConflictException($"{field} '{value}' already exists.");
    }
}

public class CategoryService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdCategory, CategoryRequest, CategoryDto>(db, h)
{
    protected override DbSet<MdCategory> Set => Db.Categories;
    protected override string Label => "Category";
    protected override IQueryable<MdCategory> ApplySearch(IQueryable<MdCategory> q, string s) => q.Where(x => x.CatName.Contains(s) || x.CatCode.Contains(s));
    protected override IQueryable<MdCategory> ApplySort(IQueryable<MdCategory> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.CatName, d), "code" => q.OrderByDir(x => x.CatCode, d), _ => q.OrderByDir(x => x.CatId, d) };
    protected override CategoryDto ToDto(MdCategory e) => new(e.CatId, e.CatName, e.CatCode, e.CatPrefix, e.IsActive);
    protected override MdCategory Create() => new();
    protected override async Task ApplyAsync(MdCategory e, CategoryRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        Unique(await Db.Categories.AnyAsync(x => x.CatCode == r.CatCode && x.CatId != self, ct), "CatCode", r.CatCode);
        e.CatName = r.CatName.Trim(); e.CatCode = r.CatCode.Trim(); e.CatPrefix = r.CatPrefix.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Groups.AnyAsync(x => x.CatId == id, ct) || await Db.Types.AnyAsync(x => x.CatId == id, ct)
        || await Db.Assets.AnyAsync(x => x.CatId == id, ct) || await Db.Models.AnyAsync(x => x.CatId == id, ct)
        || await Db.Goods.AnyAsync(x => x.CatId == id, ct) || await Db.StockOpnames.AnyAsync(x => x.CategoryId == id, ct);
}

public class GroupService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdGroup, GroupRequest, GroupDto>(db, h)
{
    protected override DbSet<MdGroup> Set => Db.Groups;
    protected override string Label => "Group";
    protected override IQueryable<MdGroup> ApplySearch(IQueryable<MdGroup> q, string s) => q.Where(x => x.GroupName.Contains(s) || x.GroupCode.Contains(s));
    protected override IQueryable<MdGroup> ApplySort(IQueryable<MdGroup> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.GroupName, d), "code" => q.OrderByDir(x => x.GroupCode, d), _ => q.OrderByDir(x => x.GroupId, d) };
    protected override GroupDto ToDto(MdGroup e) => new(e.GroupId, e.CatId, e.GroupName, e.GroupCode, e.GroupPrefix, e.IsActive);
    protected override MdGroup Create() => new();
    protected override async Task ApplyAsync(MdGroup e, GroupRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        await Hierarchy.EnsureGroupAsync(r.CatId, ct);
        Unique(await Db.Groups.AnyAsync(x => x.GroupCode == r.GroupCode && x.GroupId != self, ct), "GroupCode", r.GroupCode);
        if (id.HasValue && e.CatId != r.CatId && (await Db.Types.AnyAsync(x => x.GroupId == id.Value, ct) || await Db.Assets.AnyAsync(x => x.GroupId == id.Value, ct)))
            throw new ConflictException("Category of a Group cannot be changed while Types or Assets reference it.");
        e.CatId = r.CatId; e.GroupName = r.GroupName.Trim(); e.GroupCode = r.GroupCode.Trim(); e.GroupPrefix = r.GroupPrefix.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Types.AnyAsync(x => x.GroupId == id, ct) || await Db.Assets.AnyAsync(x => x.GroupId == id, ct)
        || await Db.Models.AnyAsync(x => x.GroupId == id, ct) || await Db.Goods.AnyAsync(x => x.GroupId == id, ct)
        || await Db.StockOpnames.AnyAsync(x => x.GroupId == id, ct);
}

public class TypeService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdType, TypeRequest, TypeDto>(db, h)
{
    protected override DbSet<MdType> Set => Db.Types;
    protected override string Label => "Type";
    protected override IQueryable<MdType> ApplySearch(IQueryable<MdType> q, string s) => q.Where(x => x.TypeName.Contains(s) || x.TypeCode.Contains(s));
    protected override IQueryable<MdType> ApplySort(IQueryable<MdType> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.TypeName, d), "code" => q.OrderByDir(x => x.TypeCode, d), _ => q.OrderByDir(x => x.TypeId, d) };
    protected override TypeDto ToDto(MdType e) => new(e.TypeId, e.GroupId, e.CatId, e.TypeName, e.TypeCode, e.TypePrefix, e.IsActive);
    protected override MdType Create() => new();
    protected override async Task ApplyAsync(MdType e, TypeRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        await Hierarchy.EnsureTypeAsync(r.CatId, r.GroupId, ct);
        Unique(await Db.Types.AnyAsync(x => x.TypeCode == r.TypeCode && x.TypeId != self, ct), "TypeCode", r.TypeCode);
        if (id.HasValue && (e.CatId != r.CatId || e.GroupId != r.GroupId) &&
            (await Db.Brands.AnyAsync(x => x.TypeId == id.Value, ct) || await Db.Assets.AnyAsync(x => x.TypeId == id.Value, ct)))
            throw new ConflictException("Category/Group of a Type cannot be changed while Brands or Assets reference it.");
        e.GroupId = r.GroupId; e.CatId = r.CatId; e.TypeName = r.TypeName.Trim(); e.TypeCode = r.TypeCode.Trim(); e.TypePrefix = r.TypePrefix.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Brands.AnyAsync(x => x.TypeId == id, ct) || await Db.Assets.AnyAsync(x => x.TypeId == id, ct)
        || await Db.Models.AnyAsync(x => x.TypeId == id, ct) || await Db.Goods.AnyAsync(x => x.TypeId == id, ct)
        || await Db.StockOpnames.AnyAsync(x => x.TypeId == id, ct);
}

public class BrandService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdBrand, BrandRequest, BrandDto>(db, h)
{
    protected override DbSet<MdBrand> Set => Db.Brands;
    protected override string Label => "Brand";
    protected override IQueryable<MdBrand> ApplySearch(IQueryable<MdBrand> q, string s) => q.Where(x => x.BrandName.Contains(s) || x.BrandCode.Contains(s));
    protected override IQueryable<MdBrand> ApplySort(IQueryable<MdBrand> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.BrandName, d), "code" => q.OrderByDir(x => x.BrandCode, d), _ => q.OrderByDir(x => x.BrandId, d) };
    protected override BrandDto ToDto(MdBrand e) => new(e.BrandId, e.TypeId, e.BrandName, e.BrandCode, e.BrandPrefix, e.IsActive);
    protected override MdBrand Create() => new();
    protected override async Task ApplyAsync(MdBrand e, BrandRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        await Hierarchy.EnsureBrandAsync(r.TypeId, ct);
        Unique(await Db.Brands.AnyAsync(x => x.BrandCode == r.BrandCode && x.BrandId != self, ct), "BrandCode", r.BrandCode);
        e.TypeId = r.TypeId; e.BrandName = r.BrandName.Trim(); e.BrandCode = r.BrandCode.Trim(); e.BrandPrefix = r.BrandPrefix.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Assets.AnyAsync(x => x.BrandId == id, ct) || await Db.Models.AnyAsync(x => x.BrandId == id, ct)
        || await Db.Goods.AnyAsync(x => x.BrandId == id, ct) || await Db.StockOpnames.AnyAsync(x => x.BrandId == id, ct);
}

public class ModelService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdModel, ModelRequest, ModelDto>(db, h)
{
    protected override DbSet<MdModel> Set => Db.Models;
    protected override string Label => "Model";
    protected override IQueryable<MdModel> ApplySearch(IQueryable<MdModel> q, string s) => q.Where(x => x.ModelName.Contains(s) || x.ModelCode.Contains(s));
    protected override IQueryable<MdModel> ApplySort(IQueryable<MdModel> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.ModelName, d), "code" => q.OrderByDir(x => x.ModelCode, d), _ => q.OrderByDir(x => x.ModelId, d) };
    protected override ModelDto ToDto(MdModel e) => new(e.ModelId, e.AssetId, e.CatId, e.BrandId, e.GroupId, e.TypeId, e.ModelName, e.ModelCode, e.IsActive);
    protected override MdModel Create() => new();
    protected override async Task ApplyAsync(MdModel e, ModelRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        await Hierarchy.ValidateAsync(r.CatId, r.GroupId, r.TypeId, r.BrandId, ct);
        if (r.AssetId.HasValue && !await Db.Assets.AnyAsync(x => x.AssetId == r.AssetId.Value, ct))
            throw new ValidationFailedException("assetId", "Asset does not exist.");
        Unique(await Db.Models.AnyAsync(x => x.ModelCode == r.ModelCode && x.ModelId != self, ct), "ModelCode", r.ModelCode);
        e.AssetId = r.AssetId; e.CatId = r.CatId; e.BrandId = r.BrandId; e.GroupId = r.GroupId; e.TypeId = r.TypeId;
        e.ModelName = r.ModelName.Trim(); e.ModelCode = r.ModelCode.Trim(); e.IsActive = r.IsActive;
    }
    protected override Task<bool> IsReferencedAsync(int id, CancellationToken ct) => Task.FromResult(false); // nothing references MD_Model
}

public class BusinessUnitService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdBusinessUnit, BusinessUnitRequest, BusinessUnitDto>(db, h)
{
    protected override DbSet<MdBusinessUnit> Set => Db.BusinessUnits;
    protected override string Label => "Business unit";
    protected override IQueryable<MdBusinessUnit> ApplySearch(IQueryable<MdBusinessUnit> q, string s) =>
        q.Where(x => x.LocatioName.Contains(s) || x.BusUnitCode.Contains(s) || x.BusUnitPrefix.Contains(s));
    protected override IQueryable<MdBusinessUnit> ApplySort(IQueryable<MdBusinessUnit> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.LocatioName, d), "code" => q.OrderByDir(x => x.BusUnitCode, d), "prefix" => q.OrderByDir(x => x.BusUnitPrefix, d), _ => q.OrderByDir(x => x.BusUnitId, d) };
    protected override BusinessUnitDto ToDto(MdBusinessUnit e) => new(e.BusUnitId, e.BusUnitCode, e.BusUnitPrefix, e.LocatioName, e.IsActive);
    protected override MdBusinessUnit Create() => new();
    protected override async Task ApplyAsync(MdBusinessUnit e, BusinessUnitRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        Unique(await Db.BusinessUnits.AnyAsync(x => x.BusUnitCode == r.BusUnitCode && x.BusUnitId != self, ct), "BusUnitCode", r.BusUnitCode);
        e.BusUnitCode = r.BusUnitCode.Trim(); e.BusUnitPrefix = r.BusUnitPrefix.Trim(); e.LocatioName = r.LocatioName.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Assets.AnyAsync(x => x.BusUnitId == id, ct) || await Db.StockOpnames.AnyAsync(x => x.BusUnitId == id, ct)
        || await Db.StockOpnameDetails.AnyAsync(x => x.ActualBusUnitId == id, ct);
}

public class ConditionService(IAppDbContext db, IHierarchyValidator h) : MasterService<MdAssetCondition, ConditionRequest, ConditionDto>(db, h)
{
    protected override DbSet<MdAssetCondition> Set => Db.Conditions;
    protected override string Label => "Asset condition";
    protected override IQueryable<MdAssetCondition> ApplySearch(IQueryable<MdAssetCondition> q, string s) => q.Where(x => x.ConditionName.Contains(s) || x.ConditionCode.Contains(s));
    protected override IQueryable<MdAssetCondition> ApplySort(IQueryable<MdAssetCondition> q, string? s, bool d) => s switch
    { "name" => q.OrderByDir(x => x.ConditionName, d), "code" => q.OrderByDir(x => x.ConditionCode, d), _ => q.OrderByDir(x => x.ConditionId, d) };
    protected override ConditionDto ToDto(MdAssetCondition e) => new(e.ConditionId, e.ConditionCode, e.ConditionName, e.IsActive);
    protected override MdAssetCondition Create() => new();
    protected override async Task ApplyAsync(MdAssetCondition e, ConditionRequest r, int? id, CancellationToken ct)
    {
        var self = id ?? 0;
        Unique(await Db.Conditions.AnyAsync(x => x.ConditionCode == r.ConditionCode && x.ConditionId != self, ct), "ConditionCode", r.ConditionCode);
        e.ConditionCode = r.ConditionCode.Trim(); e.ConditionName = r.ConditionName.Trim(); e.IsActive = r.IsActive;
    }
    protected override async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await Db.Assets.AnyAsync(x => x.ConditionId == id, ct);
}
