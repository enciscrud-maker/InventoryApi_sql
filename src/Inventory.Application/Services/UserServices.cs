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
        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = map.GetValueOrDefault(user.UserId) ?? Array.Empty<string>();
            result.Add(ToDto(
                user,
                roles,
                await EffectivePermissionNamesAsync(user, roles, ct),
                await PagePermissionNamesAsync(user, ct)));
        }
        return new PagedResult<UserDto>(result, p.SafePage, p.SafeSize, total);
    }

    public async Task<UserDto> GetAsync(int id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Employee).FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new NotFoundException($"User {id}");
        var roles = await RoleNamesAsync(id, ct);
        return ToDto(u, roles, await EffectivePermissionNamesAsync(u, roles, ct), await PagePermissionNamesAsync(u, ct));
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

    public async Task<UserDto> CreateManagedAccountAsync(ManagedAccountRequest r, CancellationToken ct)
    {
        ValidateManagedAccount(r, creating: true);
        var roleId = await RoleIdAsync(r.Role, ct);
        var permissionIds = await PermissionIdsForPagesAsync(r.Role, r.PagePermissions, ct);
        if (await db.Employees.AnyAsync(e => e.EmployeeNik == r.EmployeeNik.Trim(), ct))
            throw new ConflictException($"EmployeeNik '{r.EmployeeNik}' already exists.");
        if (await db.Users.AnyAsync(u => u.EmployeeEmail == r.EmployeeEmail.Trim(), ct))
            throw new ConflictException($"EmployeeEmail '{r.EmployeeEmail}' already exists.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var employee = new MdEmployee
        {
            EmployeeName = r.EmployeeName.Trim(),
            EmployeeNik = r.EmployeeNik.Trim(),
            EmployeePhone = r.EmployeePhone?.Trim(),
            Position = r.Position?.Trim(),
            Department = r.Department?.Trim(),
            BusinessUnit = r.BusinessUnit?.Trim(),
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        var user = new MdUser
        {
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.EmployeeName,
            EmployeeEmail = r.EmployeeEmail.Trim(),
            EmployeePassword = hasher.Hash(r.Password!),
            CreatedAt = DateTime.UtcNow,
            IsActive = r.IsActive,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.ModelRoles.Add(new CfgModelRole { RoleId = roleId, ModelId = user.UserId });
        db.ModelPermissions.AddRange(permissionIds.Select(permissionId =>
            new CfgModelPermission { ModelId = user.UserId, PermissionId = permissionId }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(user.UserId, ct);
    }

    public async Task<UserDto> UpdateManagedAccountAsync(int id, ManagedAccountRequest r, CancellationToken ct)
    {
        ValidateManagedAccount(r, creating: false);
        var user = await db.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.UserId == id, ct)
            ?? throw new NotFoundException($"User {id}");
        if (await IsMainAdminAsync(user, ct))
            throw new BadRequestException("The Main Admin identity and access cannot be changed here.");
        var roleId = await RoleIdAsync(r.Role, ct);
        var permissionIds = await PermissionIdsForPagesAsync(r.Role, r.PagePermissions, ct);
        if (await db.Employees.AnyAsync(e => e.EmployeeNik == r.EmployeeNik.Trim() && e.EmployeeId != user.EmployeeId, ct))
            throw new ConflictException($"EmployeeNik '{r.EmployeeNik}' already exists.");
        if (await db.Users.AnyAsync(u => u.EmployeeEmail == r.EmployeeEmail.Trim() && u.UserId != id, ct))
            throw new ConflictException($"EmployeeEmail '{r.EmployeeEmail}' already exists.");
        if (!r.IsActive && current.UserId == id)
            throw new BadRequestException("You cannot deactivate your own account.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var employee = user.Employee;
        employee.EmployeeName = r.EmployeeName.Trim();
        employee.EmployeeNik = r.EmployeeNik.Trim();
        employee.EmployeePhone = r.EmployeePhone?.Trim();
        employee.Position = r.Position?.Trim();
        employee.Department = r.Department?.Trim();
        employee.BusinessUnit = r.BusinessUnit?.Trim();
        user.EmployeeName = employee.EmployeeName;
        user.EmployeeEmail = r.EmployeeEmail.Trim();
        user.IsActive = r.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(r.Password))
            user.EmployeePassword = hasher.Hash(r.Password);

        var existingRoles = await db.ModelRoles.Where(m => m.ModelId == id).ToListAsync(ct);
        db.ModelRoles.RemoveRange(existingRoles);
        db.ModelRoles.Add(new CfgModelRole { RoleId = roleId, ModelId = id });
        var existingPermissions = await db.ModelPermissions.Where(m => m.ModelId == id).ToListAsync(ct);
        db.ModelPermissions.RemoveRange(existingPermissions);
        db.ModelPermissions.AddRange(permissionIds.Select(permissionId =>
            new CfgModelPermission { ModelId = id, PermissionId = permissionId }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> SetStatusAsync(int id, bool isActive, CancellationToken ct)
    {
        if (!isActive && current.UserId == id) throw new BadRequestException("You cannot deactivate your own account.");
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserId == id, ct) ?? throw new NotFoundException($"User {id}");
        if (await IsMainAdminAsync(u, ct) && !isActive) throw new BadRequestException("The Main Admin account cannot be deactivated.");
        u.IsActive = isActive; u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> SetRolesAsync(int id, List<int> roleIds, CancellationToken ct)
    {
        var target = await db.Users.FirstOrDefaultAsync(x => x.UserId == id, ct)
            ?? throw new NotFoundException($"User {id}");
        if (await IsMainAdminAsync(target, ct)) throw new BadRequestException("The Main Admin role cannot be changed.");
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
        var existingPermissions = await db.ModelPermissions.Where(m => m.ModelId == id).ToListAsync(ct);
        db.ModelPermissions.RemoveRange(existingPermissions);
        db.ModelRoles.AddRange(roleIds.Select(rid => new CfgModelRole { RoleId = rid, ModelId = id }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserPermissionsDto> GetPermissionsAsync(int id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.UserId == id, ct)) throw new NotFoundException($"User {id}");

        var roleIds = await db.ModelRoles.Where(m => m.ModelId == id).Select(m => m.RoleId).ToListAsync(ct);
        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id, ct)
            ?? throw new NotFoundException($"User {id}");
        var roleNames = await db.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleName).ToListAsync(ct);
        var directPermissionIds = await db.ModelPermissions.Where(mp => mp.ModelId == id)
            .Select(mp => mp.PermissionId).ToListAsync(ct);
        var effectiveNames = await EffectivePermissionNamesAsync(target, roleNames, ct);
        var allowedNames = effectiveNames.ToHashSet(StringComparer.Ordinal);
        var directIds = directPermissionIds.ToHashSet();
        var permissions = await db.Permissions.AsNoTracking().OrderBy(p => p.PermissionName)
            .Select(p => new PermissionDto(p.PermissionId, p.PermissionName, p.GuardName)).ToListAsync(ct);

        return new UserPermissionsDto(id, permissions.Select(p => new UserPermissionDto(
            p.PermissionId, p.PermissionName, p.GuardName, allowedNames.Contains(p.PermissionName), directIds.Contains(p.PermissionId))).ToList());
    }

    public async Task<UserPermissionsDto> SetPermissionsAsync(int id, List<int> permissionIds, CancellationToken ct)
    {
        var target = await db.Users.FirstOrDefaultAsync(x => x.UserId == id, ct)
            ?? throw new NotFoundException($"User {id}");
        if (await IsMainAdminAsync(target, ct)) throw new BadRequestException("The Main Admin permissions cannot be changed.");

        var ids = permissionIds.Distinct().ToList();
        var names = await db.Permissions.Where(p => ids.Contains(p.PermissionId)).Select(p => p.PermissionName).ToListAsync(ct);
        if (names.Count != ids.Count) throw new ValidationFailedException("permissionIds", "One or more permissions do not exist.");
        if (names.Contains(Permissions.UserManage, StringComparer.Ordinal))
            throw new BadRequestException("User-management permission is reserved for the Main Admin.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existingPermissions = await db.ModelPermissions.Where(m => m.ModelId == id).ToListAsync(ct);
        db.ModelPermissions.RemoveRange(existingPermissions);
        db.ModelPermissions.AddRange(ids.Select(permissionId => new CfgModelPermission { ModelId = id, PermissionId = permissionId }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetPermissionsAsync(id, ct);
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

    private async Task<IReadOnlyList<string>> EffectivePermissionNamesAsync(MdUser user, IReadOnlyList<string> roles, CancellationToken ct)
    {
        if (await IsMainAdminAsync(user, ct)) return Permissions.All;
        var roleIds = db.ModelRoles.Where(m => m.ModelId == user.UserId).Select(m => m.RoleId);
        var rolePermissionIds = db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(db.Roles, rp => rp.RoleId, r => r.RoleId, (rp, r) => new { rp.PermissionId, r.RoleName })
            .Where(x => x.RoleName != RoleNames.Admin)
            .Select(x => x.PermissionId);
        var directPermissionIds = db.ModelPermissions.Where(mp => mp.ModelId == user.UserId).Select(mp => mp.PermissionId);
        var names = await db.Permissions
            .Where(p => rolePermissionIds.Contains(p.PermissionId) || directPermissionIds.Contains(p.PermissionId))
            .Select(p => p.PermissionName)
            .Distinct()
            .ToListAsync(ct);
        names = names.Where(Permissions.All.Contains).ToList();
        if (roles.Contains(RoleNames.Member))
            names = Permissions.MemberDefaults
                .Concat(names.Where(p => Permissions.MemberCreatable.Contains(p, StringComparer.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        names.Remove(Permissions.UserManage);
        return names;
    }

    private async Task<IReadOnlyList<string>> PagePermissionNamesAsync(MdUser user, CancellationToken ct)
    {
        if (await IsMainAdminAsync(user, ct)) return Permissions.PagePermissions.Keys.ToList();
        var markers = await db.ModelPermissions
            .Where(mp => mp.ModelId == user.UserId)
            .Join(db.Permissions, mp => mp.PermissionId, p => p.PermissionId, (_, p) => p.PermissionName)
            .ToListAsync(ct);
        return Permissions.PageMarkers
            .Where(marker => markers.Contains(marker.Value, StringComparer.Ordinal))
            .Select(marker => marker.Key)
            .ToList();
    }

    private async Task<int> RoleIdAsync(string role, CancellationToken ct)
    {
        if (role is not (RoleNames.Admin or RoleNames.Member))
            throw new ValidationFailedException("role", "Role must be Admin or Member.");
        var roleId = await db.Roles.Where(r => r.RoleName == role).Select(r => r.RoleId).FirstOrDefaultAsync(ct);
        return roleId > 0
            ? roleId
            : throw new InvalidOperationException($"Role '{role}' has not been seeded.");
    }

    private async Task<List<int>> PermissionIdsForPagesAsync(string role, IReadOnlyCollection<string>? pagePermissions, CancellationToken ct)
    {
        var pages = pagePermissions ?? Array.Empty<string>();
        var unknown = pages.Where(page => !Permissions.PagePermissions.ContainsKey(page)).Distinct(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
            throw new ValidationFailedException("pagePermissions", $"Unknown page permission(s): {string.Join(", ", unknown)}.");
        var selectedPages = pages.Distinct(StringComparer.Ordinal).ToList();
        var names = selectedPages
            .SelectMany(page => Permissions.PagePermissions[page]);
        if (role == RoleNames.Member) names = names.Where(Permissions.MemberCreatable.Contains);
        var permissionNames = names.Concat(selectedPages.Select(page => Permissions.PageMarkers[page]))
            .Distinct(StringComparer.Ordinal).ToList();
        return await db.Permissions.Where(p => permissionNames.Contains(p.PermissionName))
            .Select(p => p.PermissionId).ToListAsync(ct);
    }

    private async Task<bool> IsMainAdminAsync(MdUser user, CancellationToken ct)
    {
        if (user.IsMainAdmin) return true;
        if (user.Employee is not null)
            return user.Employee.EmployeeNik == RoleNames.MainAdminNik;
        return await db.Employees.AnyAsync(
            e => e.EmployeeId == user.EmployeeId && e.EmployeeNik == RoleNames.MainAdminNik, ct);
    }

    private static void ValidateManagedAccount(ManagedAccountRequest r, bool creating)
    {
        if (string.IsNullOrWhiteSpace(r.EmployeeName) || string.IsNullOrWhiteSpace(r.EmployeeNik) ||
            string.IsNullOrWhiteSpace(r.EmployeeEmail))
            throw new ValidationFailedException("account", "Name, NIK and email are required.");
        if (creating && (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8))
            throw new ValidationFailedException("password", "A password of at least 8 characters is required.");
        if (!string.IsNullOrWhiteSpace(r.Password) && r.Password.Length < 8)
            throw new ValidationFailedException("password", "A password of at least 8 characters is required.");
        if (r.EmployeeName.Length > 200 || r.EmployeeNik.Length > 50 || r.EmployeeEmail.Length > 200 ||
            (r.EmployeePhone?.Length ?? 0) > 30 || (r.Position?.Length ?? 0) > 100 ||
            (r.Department?.Length ?? 0) > 100 || (r.BusinessUnit?.Length ?? 0) > 100)
            throw new ValidationFailedException("account", "One or more account fields exceed the maximum length.");
        if (!System.Net.Mail.MailAddress.TryCreate(r.EmployeeEmail.Trim(), out _))
            throw new ValidationFailedException("employeeEmail", "A valid email address is required.");
        if (r.PagePermissions is null)
            throw new ValidationFailedException("pagePermissions", "Page permissions are required.");
    }

    // EmployeePassword is intentionally never part of any DTO.
    private static UserDto ToDto(MdUser u, IReadOnlyList<string> roles, IReadOnlyList<string> permissions,
        IReadOnlyList<string> pagePermissions) =>
        new(u.UserId, u.EmployeeId, u.EmployeeName, u.EmployeeEmail, u.Employee.EmployeeNik, u.IsActive,
            u.ProfilePhotoUrl, u.CreatedAt, u.UpdatedAt, roles, u.Employee.EmployeePhone, u.Employee.Position,
            u.Employee.Department, u.Employee.BusinessUnit,
            u.IsMainAdmin || u.Employee.EmployeeNik == RoleNames.MainAdminNik, permissions, pagePermissions);
}
