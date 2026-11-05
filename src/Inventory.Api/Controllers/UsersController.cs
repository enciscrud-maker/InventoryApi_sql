namespace Inventory.Api.Controllers;

/// <summary>Employees, users, roles and permissions. Admin only.</summary>
[ApiController, Authorize(Policy = Permissions.UserManage), Produces("application/json")]
public class EmployeesController(EmployeeService svc) : ControllerBase
{
    [HttpGet("api/employees")]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> List([FromQuery] PageQuery q, CancellationToken ct) => Ok(await svc.ListAsync(q, ct));

    [HttpGet("api/employees/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    [HttpPost("api/employees")]
    public async Task<ActionResult<EmployeeDto>> Create(EmployeeRequest r, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(r, ct));

    [HttpPut("api/employees/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, EmployeeRequest r, CancellationToken ct) => Ok(await svc.UpdateAsync(id, r, ct));
}

[ApiController, Authorize(Policy = Permissions.UserManage), Produces("application/json")]
public class UsersController(UserService svc) : ControllerBase
{
    [HttpGet("api/users")]
    public async Task<ActionResult<PagedResult<UserDto>>> List([FromQuery] UserQuery q, CancellationToken ct) => Ok(await svc.ListAsync(q, ct));

    [HttpGet("api/users/{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    [HttpPost("api/users")]
    public async Task<ActionResult<UserDto>> Create(UserCreateRequest r, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(r, ct));

    [HttpPut("api/users/{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UserUpdateRequest r, CancellationToken ct) => Ok(await svc.UpdateAsync(id, r, ct));

    /// <summary>Activate / deactivate a user.</summary>
    [HttpPatch("api/users/{id:int}/status")]
    public async Task<ActionResult<UserDto>> SetStatus(int id, UserStatusRequest r, CancellationToken ct) => Ok(await svc.SetStatusAsync(id, r.IsActive, ct));

    /// <summary>Replaces the user's roles (CFG_ModelRole).</summary>
    [HttpPut("api/users/{id:int}/roles")]
    public async Task<ActionResult<UserDto>> SetRoles(int id, UserRolesRequest r, CancellationToken ct) => Ok(await svc.SetRolesAsync(id, r.RoleIds, ct));

    /// <summary>Returns the permission catalog with the user's effective and directly assigned permissions.</summary>
    [HttpGet("api/users/{id:int}/permissions")]
    public async Task<ActionResult<UserPermissionsDto>> GetPermissions(int id, CancellationToken ct) => Ok(await svc.GetPermissionsAsync(id, ct));

    /// <summary>Replaces the user's access with exactly the supplied permissions, removing role assignments.</summary>
    [HttpPost("api/users/{id:int}/permissions")]
    public async Task<ActionResult<UserPermissionsDto>> SetPermissions(int id, UserPermissionsRequest r, CancellationToken ct) =>
        Ok(await svc.SetPermissionsAsync(id, r.PermissionIds, ct));

    [HttpGet("api/roles")]
    public async Task<ActionResult<List<RoleDto>>> ListRoles(CancellationToken ct) => Ok(await svc.ListRolesAsync(ct));

    [HttpGet("api/permissions")]
    public async Task<ActionResult<List<PermissionDto>>> ListPermissions(CancellationToken ct) => Ok(await svc.ListPermissionsAsync(ct));
}
