namespace Inventory.Application.Services;

public interface IHierarchyValidator
{
    Task ValidateAsync(int catId, int groupId, int typeId, int? brandId, CancellationToken ct);
    Task EnsureGroupAsync(int catId, CancellationToken ct);
    Task EnsureTypeAsync(int catId, int groupId, CancellationToken ct);
    Task EnsureBrandAsync(int? typeId, CancellationToken ct);
}

/// <summary>Rule 13: Category -> Group -> Type -> Brand must be consistent.</summary>
public class HierarchyValidator(IAppDbContext db) : IHierarchyValidator
{
    public async Task ValidateAsync(int catId, int groupId, int typeId, int? brandId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var cat = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.CatId == catId, ct);
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.GroupId == groupId, ct);
        var type = await db.Types.AsNoTracking().FirstOrDefaultAsync(x => x.TypeId == typeId, ct);
        MdBrand? brand = brandId.HasValue ? await db.Brands.AsNoTracking().FirstOrDefaultAsync(x => x.BrandId == brandId.Value, ct) : null;

        if (cat is null) errors["catId"] = new[] { "Category does not exist." };
        if (group is null) errors["groupId"] = new[] { "Group does not exist." };
        else if (cat is not null && group.CatId != catId) errors["groupId"] = new[] { "Group does not belong to the selected Category." };
        if (type is null) errors["typeId"] = new[] { "Type does not exist." };
        else
        {
            if (group is not null && type.GroupId != groupId) errors["typeId"] = new[] { "Type does not belong to the selected Group." };
            else if (type.CatId != catId) errors["typeId"] = new[] { "Type does not belong to the selected Category." };
        }
        if (brandId.HasValue)
        {
            if (brand is null) errors["brandId"] = new[] { "Brand does not exist." };
            else if (type is not null && brand.TypeId.HasValue && brand.TypeId != typeId) errors["brandId"] = new[] { "Brand does not match the selected Type." };
        }
        if (errors.Count > 0) throw new ValidationFailedException(errors);
    }

    public async Task EnsureGroupAsync(int catId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(x => x.CatId == catId, ct))
            throw new ValidationFailedException("catId", "Category does not exist.");
    }

    public async Task EnsureTypeAsync(int catId, int groupId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(x => x.CatId == catId, ct))
            throw new ValidationFailedException("catId", "Category does not exist.");
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.GroupId == groupId, ct)
            ?? throw new ValidationFailedException("groupId", "Group does not exist.");
        if (group.CatId != catId) throw new ValidationFailedException("groupId", "Group does not belong to the selected Category.");
    }

    public async Task EnsureBrandAsync(int? typeId, CancellationToken ct)
    {
        if (typeId.HasValue && !await db.Types.AnyAsync(x => x.TypeId == typeId.Value, ct))
            throw new ValidationFailedException("typeId", "Type does not exist.");
    }
}
