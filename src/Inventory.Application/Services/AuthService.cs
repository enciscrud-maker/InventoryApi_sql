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
        var (roles, perms) = await LoadAuthzAsync(userId, ct);
        return ToProfile(user, roles, perms);
    }

    private async Task<AuthResponse> IssueAsync(MdUser user, CancellationToken ct)
    {
        var (roles, perms) = await LoadAuthzAsync(user.UserId, ct);
        var access = tokens.CreateAccessToken(user.UserId, user.Employee.EmployeeName, user.EmployeeEmail, user.Employee.EmployeeNik, roles, perms);
        var refresh = tokens.CreateRefreshToken(user.UserId);
        return new AuthResponse(access.Token, refresh.Token, access.ExpiresAt, ToProfile(user, roles, perms));
    }

    public async Task<(List<string> Roles, List<string> Permissions)> LoadAuthzAsync(int userId, CancellationToken ct)
    {
        var roleIds = db.ModelRoles.Where(m => m.ModelId == userId).Select(m => m.RoleId);
        var roles = await db.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleName).ToListAsync(ct);
        var permIds = db.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)).Select(rp => rp.PermissionId)
            .Union(db.ModelPermissions.Where(mp => mp.ModelId == userId).Select(mp => mp.PermissionId));
        var perms = await db.Permissions.Where(p => permIds.Contains(p.PermissionId)).Select(p => p.PermissionName).ToListAsync(ct);
        return (roles, perms);
    }

    private static UserProfileDto ToProfile(MdUser u, IReadOnlyList<string> roles, IReadOnlyList<string> perms) =>
        new(u.UserId, u.EmployeeId, u.Employee.EmployeeName, u.EmployeeEmail, u.Employee.EmployeeNik, u.ProfilePhotoUrl, roles, perms);
}
