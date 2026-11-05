using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inventory.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Infrastructure.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "InventoryApi";
    public string Audience { get; set; } = "InventoryClient";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _o = options.Value;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    private SigningCredentials Credentials => new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256);

    public TokenResult CreateAccessToken(int userId, string name, string email, string nik, IEnumerable<string> roles,
        IEnumerable<string> permissions, IEnumerable<string> pagePermissions, bool isMainAdmin)
    {
        var jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()), new("jti", jti), new("token_use", "access"),
            new("name", name), new("email", email), new("nik", nik)
        };
        if (isMainAdmin) claims.Add(new Claim("is_main_admin", "true"));
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Distinct().Select(p => new Claim("permission", p)));
        claims.AddRange(pagePermissions.Distinct().Select(p => new Claim("page_permission", p)));
        var expires = DateTime.UtcNow.AddMinutes(_o.AccessTokenMinutes);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, DateTime.UtcNow, expires, Credentials);
        return new TokenResult(_handler.WriteToken(token), expires, jti);
    }

    public TokenResult CreateRefreshToken(int userId)
    {
        var jti = Guid.NewGuid().ToString("N");
        var claims = new[] { new Claim("sub", userId.ToString()), new Claim("jti", jti), new Claim("token_use", "refresh") };
        var expires = DateTime.UtcNow.AddDays(_o.RefreshTokenDays);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, DateTime.UtcNow, expires, Credentials);
        return new TokenResult(_handler.WriteToken(token), expires, jti);
    }

    public RefreshTokenInfo? ValidateRefreshToken(string token)
    {
        try
        {
            var principal = _handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = _o.Issuer, ValidateAudience = true, ValidAudience = _o.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validated);
            if (principal.FindFirst("token_use")?.Value != "refresh") return null;
            var sub = principal.FindFirst("sub")?.Value; var jti = principal.FindFirst("jti")?.Value;
            return int.TryParse(sub, out var uid) && jti is not null ? new RefreshTokenInfo(uid, jti, validated.ValidTo) : null;
        }
        catch (Exception) { return null; }
    }
}

public class MemoryTokenRevocation(IMemoryCache cache) : ITokenRevocation
{
    public void Revoke(string jti, DateTime expiresAtUtc)
    {
        var ttl = expiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        cache.Set($"revoked:{jti}", true, ttl);
    }
    public bool IsRevoked(string jti) => cache.TryGetValue($"revoked:{jti}", out _);
}
