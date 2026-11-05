namespace Inventory.Api.Controllers;

/// <summary>Master data CRUD. Admin + Member: read/create/update. Admin only: delete (rejected with 409 while referenced).</summary>
[ApiController, Authorize, Produces("application/json")]
public abstract class MasterControllerBase<TReq, TDto>(IMasterService<TReq, TDto> svc) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.MasterLookupRead)]
    public async Task<ActionResult<PagedResult<TDto>>> List([FromQuery] PageQuery q, CancellationToken ct) => Ok(await svc.ListAsync(q, ct));

    [HttpGet("{id:int}"), Authorize(Policy = Permissions.MasterLookupRead)]
    public async Task<ActionResult<TDto>> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    [HttpPost, Authorize(Policy = Permissions.MasterCreate)]
    public async Task<ActionResult<TDto>> Create(TReq request, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(request, ct));

    [HttpPut("{id:int}"), Authorize(Policy = Permissions.MasterUpdate)]
    public async Task<ActionResult<TDto>> Update(int id, TReq request, CancellationToken ct) => Ok(await svc.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}"), Authorize(Policy = Permissions.MasterDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[Route("api/categories")] public class CategoriesController(IMasterService<CategoryRequest, CategoryDto> s) : MasterControllerBase<CategoryRequest, CategoryDto>(s);
[Route("api/asset-groups")] public class AssetGroupsController(IMasterService<GroupRequest, GroupDto> s) : MasterControllerBase<GroupRequest, GroupDto>(s);
[Route("api/asset-types")] public class AssetTypesController(IMasterService<TypeRequest, TypeDto> s) : MasterControllerBase<TypeRequest, TypeDto>(s);
[Route("api/brands")] public class BrandsController(IMasterService<BrandRequest, BrandDto> s) : MasterControllerBase<BrandRequest, BrandDto>(s);
[Route("api/asset-models")] public class AssetModelsController(IMasterService<ModelRequest, ModelDto> s) : MasterControllerBase<ModelRequest, ModelDto>(s);
[Route("api/business-units")] public class BusinessUnitsController(IMasterService<BusinessUnitRequest, BusinessUnitDto> s) : MasterControllerBase<BusinessUnitRequest, BusinessUnitDto>(s);
[Route("api/locations")] public class LocationsController(IMasterService<LocationRequest, LocationDto> s) : MasterControllerBase<LocationRequest, LocationDto>(s);
[Route("api/asset-statuses")] public class AssetStatusesController(IMasterService<AssetStatusRequest, AssetStatusDto> s) : MasterControllerBase<AssetStatusRequest, AssetStatusDto>(s);
[Route("api/asset-conditions")] public class AssetConditionsController(IMasterService<ConditionRequest, ConditionDto> s) : MasterControllerBase<ConditionRequest, ConditionDto>(s);
