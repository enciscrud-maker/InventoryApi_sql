namespace Inventory.Application.Services;

public class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, ITokenRevocation revocation)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var id = r.Identifier.Trim();
        var user = await db.Users.Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.EmployeeEmail == id || u.Employee.EmployeeNik == id, ct);
        if (user is null || !hasher.Verify(r.Password, user.EmployeePassword))
            throw new AppException(401, "Unauthorized", "Invalid credentials.");
        if (!user.IsActive) throw new AppException(403, "Forbidden", "Account is inactive.");
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var info = tokens.ValidateRefreshToken(refreshToken) ?? throw new AppException(401, "Unauthorized", "Invalid refresh token.");
        if (revocation.IsRevoked(info.Jti)) throw new AppException(401, "Unauthorized", "Refresh token has been revoked.");
        var user = await db.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.UserId == info.UserId, ct)
            ?? throw new AppException(401, "Unauthorized", "User no longer exists.");
        if (!user.IsActive) throw new AppException(403, "Forbidden", "Account is inactive.");
        revocation.Revoke(info.Jti, info.ExpiresAt); // rotation: a refresh token is single-use
        return await IssueAsync(user, ct);
    }

    public void Logout(string? accessJti, DateTime? accessExpiresAt, string? refreshToken)
    {
        if (accessJti is not null) revocation.Revoke(accessJti, accessExpiresAt ?? DateTime.UtcNow.AddHours(1));
        if (!string.IsNullOrWhiteSpace(refreshToken) && tokens.ValidateRefreshToken(refreshToken) is { } info)
            revocation.Revoke(info.Jti, info.ExpiresAt);
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Employee).FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException("User");
        var (roles, perms, pages, isMainAdmin) = await LoadAuthzAsync(userId, ct);
        return ToProfile(user, roles, perms, pages, isMainAdmin);
    }

    private async Task<AuthResponse> IssueAsync(MdUser user, CancellationToken ct)
    {
        var (roles, perms, pages, isMainAdmin) = await LoadAuthzAsync(user.UserId, ct);
        var access = tokens.CreateAccessToken(user.UserId, user.Employee.EmployeeName, user.EmployeeEmail,
            user.Employee.EmployeeNik, roles, perms, pages, isMainAdmin);
        var refresh = tokens.CreateRefreshToken(user.UserId);
        return new AuthResponse(access.Token, refresh.Token, access.ExpiresAt, ToProfile(user, roles, perms, pages, isMainAdmin));
    }

    public async Task<(List<string> Roles, List<string> Permissions, List<string> PagePermissions, bool IsMainAdmin)> LoadAuthzAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new AppException(401, "Unauthorized", "User no longer exists.");
        var isMainAdmin = user.IsMainAdmin || await db.Employees.AnyAsync(
            e => e.EmployeeId == user.EmployeeId && e.EmployeeNik == RoleNames.MainAdminNik, ct);
        var roleIds = db.ModelRoles.Where(m => m.ModelId == userId).Select(m => m.RoleId);
        var roles = await db.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleName).ToListAsync(ct);
        var pagePermissionNames = await db.ModelPermissions
            .Where(mp => mp.ModelId == userId)
            .Join(db.Permissions, mp => mp.PermissionId, p => p.PermissionId, (_, p) => p.PermissionName)
            .ToListAsync(ct);
        var pages = isMainAdmin
            ? Permissions.PagePermissions.Keys.ToList()
            : Permissions.PageMarkers
                .Where(marker => pagePermissionNames.Contains(marker.Value, StringComparer.Ordinal))
                .Select(marker => marker.Key)
                .ToList();
        if (isMainAdmin) return (roles, Permissions.All.ToList(), pages, true);

        var rolePermissionIds = db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(db.Roles, rp => rp.RoleId, r => r.RoleId, (rp, r) => new { rp.PermissionId, r.RoleName })
            .Where(x => x.RoleName != RoleNames.Admin)
            .Select(x => x.PermissionId);
        var directPermissionIds = db.ModelPermissions
            .Where(mp => mp.ModelId == userId)
            .Select(mp => mp.PermissionId);
        var permissionNames = await db.Permissions
            .Where(p => rolePermissionIds.Contains(p.PermissionId) || directPermissionIds.Contains(p.PermissionId))
            .Select(p => p.PermissionName)
            .Distinct()
            .ToListAsync(ct);

        var isMember = roles.Contains(RoleNames.Member);
        var perms = isMember
            ? Permissions.MemberDefaults
                .Concat(permissionNames.Where(p => Permissions.MemberCreatable.Contains(p, StringComparer.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : permissionNames;
        perms.Remove(Permissions.UserManage);
        return (roles, perms, pages, false);
    }

    private static UserProfileDto ToProfile(MdUser u, IReadOnlyList<string> roles, IReadOnlyList<string> perms, IReadOnlyList<string> pages, bool isMainAdmin) =>
        new(u.UserId, u.EmployeeId, u.Employee.EmployeeName, u.EmployeeEmail, u.Employee.EmployeeNik,
            u.ProfilePhotoUrl, roles, perms, pages, isMainAdmin);
}
