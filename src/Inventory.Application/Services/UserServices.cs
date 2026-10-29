namespace Inventory.Application.Services;

public class EmployeeService(IAppDbContext db)
{
    public async Task<PagedResult<EmployeeDto>> ListAsync(PageQuery p, CancellationToken ct)
    {
        var q = db.Employees.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim();
            q = q.Where(e => e.EmployeeName.Contains(s) || e.EmployeeNik.Contains(s) || (e.Department != null && e.Department.Contains(s)));
        }
        q = p.SortBy?.ToLowerInvariant() switch
        {
            "name" => q.OrderByDir(e => e.EmployeeName, p.Desc),
            "nik" => q.OrderByDir(e => e.EmployeeNik, p.Desc),
            _ => q.OrderByDir(e => e.EmployeeId, p.Desc)
        };
        var total = await q.CountAsync(ct);
        var items = await q.Skip((p.SafePage - 1) * p.SafeSize).Take(p.SafeSize).Include(e => e.User).ToListAsync(ct);
        return new PagedResult<EmployeeDto>(items.Select(ToDto).ToList(), p.SafePage, p.SafeSize, total);
    }

    public async Task<EmployeeDto> GetAsync(int id, CancellationToken ct) =>
        ToDto(await db.Employees.AsNoTracking().Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == id, ct) ?? throw new NotFoundException($"Employee {id}"));

    public async Task<EmployeeDto> CreateAsync(EmployeeRequest r, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(e => e.EmployeeNik == r.EmployeeNik, ct)) throw new ConflictException($"EmployeeNik '{r.EmployeeNik}' already exists.");
        var e = new MdEmployee();
        Apply(e, r);
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, EmployeeRequest r, CancellationToken ct)
    {
        var e = await db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.EmployeeId == id, ct) ?? throw new NotFoundException($"Employee {id}");
        if (await db.Employees.AnyAsync(x => x.EmployeeNik == r.EmployeeNik && x.EmployeeId != id, ct)) throw new ConflictException($"EmployeeNik '{r.EmployeeNik}' already exists.");
        Apply(e, r);
        if (e.User is not null) { e.User.EmployeeName = e.EmployeeName; e.User.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    private static void Apply(MdEmployee e, EmployeeRequest r)
    {
        e.EmployeeName = r.EmployeeName.Trim(); e.EmployeePhone = r.EmployeePhone?.Trim(); e.EmployeeNik = r.EmployeeNik.Trim();
        e.Position = r.Position?.Trim(); e.Department = r.Department?.Trim(); e.BusinessUnit = r.BusinessUnit?.Trim();
    }

    private static EmployeeDto ToDto(MdEmployee e) =>
        new(e.EmployeeId, e.EmployeeName, e.EmployeePhone, e.EmployeeNik, e.Position, e.Department, e.BusinessUnit, e.User is not null);
}

public class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser current)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserQuery p, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking().Include(u => u.Employee).AsQueryable();
        if (p.IsActive.HasValue) q = q.Where(u => u.IsActive == p.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim();
            q = q.Where(u => u.EmployeeName.Contains(s) || u.EmployeeEmail.Contains(s) || u.Employee.EmployeeNik.Contains(s));
        }
        q = p.SortBy?.ToLowerInvariant() switch
        {
            "name" => q.OrderByDir(u => u.EmployeeName, p.Desc),
            "email" => q.OrderByDir(u => u.EmployeeEmail, p.Desc),
            _ => q.OrderByDir(u => u.UserId, p.Desc)
        };
        var total = await q.CountAsync(ct);
        var users = await q.Skip((p.SafePage - 1) * p.SafeSize).Take(p.SafeSize).ToListAsync(ct);
        var ids = users.Select(u => u.UserId).ToList();
        var roleRows = await (from mr in db.ModelRoles join r in db.Roles on mr.RoleId equals r.RoleId where ids.Contains(mr.ModelId) select new { mr.ModelId, r.RoleName }).ToListAsync(ct);
        var map = roleRows.GroupBy(x => x.ModelId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).ToList());
        return new PagedResult<UserDto>(users.Select(u => ToDto(u, map.GetValueOrDefault(u.UserId) ?? Array.Empty<string>())).ToList(), p.SafePage, p.SafeSize, total);
    }

    public async Task<UserDto> GetAsync(int id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Employee).FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new NotFoundException($"User {id}");
        return ToDto(u, await RoleNamesAsync(id, ct));
    }

    public async Task<UserDto> CreateAsync(UserCreateRequest r, CancellationToken ct)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == r.EmployeeId, ct) ?? throw new ValidationFailedException("employeeId", "Employee does not exist.");
        if (await db.Users.AnyAsync(u => u.EmployeeId == r.EmployeeId, ct)) throw new ConflictException("This employee already has a user account.");
        if (await db.Users.AnyAsync(u => u.EmployeeEmail == r.EmployeeEmail, ct)) throw new ConflictException($"EmployeeEmail '{r.EmployeeEmail}' already exists.");

        List<int> roleIds;
        if (r.RoleIds is { Count: > 0 }) roleIds = r.RoleIds.Distinct().ToList();
        else roleIds = await db.Roles.Where(x => x.RoleName == RoleNames.Member).Select(x => x.RoleId).ToListAsync(ct);
        await EnsureRolesExistAsync(roleIds, ct);

        var user = new MdUser
        {
            EmployeeId = emp.EmployeeId, EmployeeName = emp.EmployeeName, EmployeeEmail = r.EmployeeEmail.Trim(),
            EmployeePassword = hasher.Hash(r.Password), CreatedAt = DateTime.UtcNow, IsActive = true, ProfilePhotoUrl = r.ProfilePhotoUrl
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.ModelRoles.AddRange(roleIds.Select(rid => new CfgModelRole { RoleId = rid, ModelId = user.UserId }));
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.UserId, ct);
    }

    public async Task<UserDto> UpdateAsync(int id, UserUpdateRequest r, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new NotFoundException($"User {id}");
        if (await db.Users.AnyAsync(x => x.EmployeeEmail == r.EmployeeEmail && x.UserId != id, ct)) throw new ConflictException($"EmployeeEmail '{r.EmployeeEmail}' already exists.");
        u.EmployeeEmail = r.EmployeeEmail.Trim();
        u.ProfilePhotoUrl = r.ProfilePhotoUrl;
        if (!string.IsNullOrEmpty(r.Password)) u.EmployeePassword = hasher.Hash(r.Password);
        u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> SetStatusAsync(int id, bool isActive, CancellationToken ct)
    {
        if (!isActive && current.UserId == id) throw new BadRequestException("You cannot deactivate your own account.");
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new NotFoundException($"User {id}");
        u.IsActive = isActive; u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> SetRolesAsync(int id, List<int> roleIds, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.UserId == id, ct)) throw new NotFoundException($"User {id}");
        roleIds = roleIds.Distinct().ToList();
        await EnsureRolesExistAsync(roleIds, ct);
        if (current.UserId == id)
        {
            var admin = await db.Roles.Where(r => r.RoleName == RoleNames.Admin).Select(r => r.RoleId).FirstOrDefaultAsync(ct);
            if (!roleIds.Contains(admin)) throw new BadRequestException("You cannot remove your own Admin role.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.ModelRoles.Where(m => m.ModelId == id).ToListAsync(ct);
        db.ModelRoles.RemoveRange(existing);
        db.ModelRoles.AddRange(roleIds.Select(rid => new CfgModelRole { RoleId = rid, ModelId = id }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<List<RoleDto>> ListRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToListAsync(ct);
        var rp = await (from x in db.RolePermissions join p in db.Permissions on x.PermissionId equals p.PermissionId select new { x.RoleId, p.PermissionName }).ToListAsync(ct);
        return roles.Select(r => new RoleDto(r.RoleId, r.RoleName, r.GuardName, rp.Where(x => x.RoleId == r.RoleId).Select(x => x.PermissionName).OrderBy(x => x).ToList())).ToList();
    }

    public async Task<List<PermissionDto>> ListPermissionsAsync(CancellationToken ct) =>
        await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionName).Select(p => new PermissionDto(p.PermissionId, p.PermissionName, p.GuardName)).ToListAsync(ct);

    private async Task EnsureRolesExistAsync(List<int> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0) throw new ValidationFailedException("roleIds", "At least one role is required.");
        var found = await db.Roles.CountAsync(r => roleIds.Contains(r.RoleId), ct);
        if (found != roleIds.Count) throw new ValidationFailedException("roleIds", "One or more roles do not exist.");
    }

    private async Task<IReadOnlyList<string>> RoleNamesAsync(int userId, CancellationToken ct) =>
        await (from mr in db.ModelRoles join r in db.Roles on mr.RoleId equals r.RoleId where mr.ModelId == userId select r.RoleName).ToListAsync(ct);

    // EmployeePassword is intentionally never part of any DTO.
    private static UserDto ToDto(MdUser u, IReadOnlyList<string> roles) =>
        new(u.UserId, u.EmployeeId, u.EmployeeName, u.EmployeeEmail, u.Employee.EmployeeNik, u.IsActive, u.ProfilePhotoUrl, u.CreatedAt, u.UpdatedAt, roles);
}
