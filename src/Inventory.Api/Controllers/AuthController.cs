namespace Inventory.Api.Controllers;

[ApiController, Route("api/auth"), Produces("application/json")]
public class AuthController(AuthService auth, Inventory.Application.Abstractions.ICurrentUser current) : ControllerBase
{
    /// <summary>Login with EmployeeEmail or EmployeeNik (field <c>identifier</c>) and password.</summary>
    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) => Ok(await auth.LoginAsync(request, ct));

    /// <summary>Exchanges a valid refresh token for a new access/refresh token pair (refresh tokens are single use).</summary>
    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) => Ok(await auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>Revokes the current access token (and the supplied refresh token, if any).</summary>
    [HttpPost("logout"), Authorize]
    public IActionResult Logout(LogoutRequest? request)
    {
        var jti = User.FindFirst("jti")?.Value;
        DateTime? exp = long.TryParse(User.FindFirst("exp")?.Value, out var e) ? DateTimeOffset.FromUnixTimeSeconds(e).UtcDateTime : null;
        auth.Logout(jti, exp, request?.RefreshToken);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetProfileAsync(current.UserId ?? throw new AppException(401, "Unauthorized"), ct));
}
