using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Inventory.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<MdCategory> Categories { get; }
    DbSet<MdGroup> Groups { get; }
    DbSet<MdType> Types { get; }
    DbSet<MdBrand> Brands { get; }
    DbSet<MdModel> Models { get; }
    DbSet<MdAsset> Assets { get; }
    DbSet<MdBusinessUnit> BusinessUnits { get; }
    DbSet<MdLocation> Locations { get; }
    DbSet<MdAssetStatus> AssetStatuses { get; }
    DbSet<MdAssetCondition> Conditions { get; }
    DbSet<MdEmployee> Employees { get; }
    DbSet<MdUser> Users { get; }
    DbSet<CfgRole> Roles { get; }
    DbSet<CfgPermission> Permissions { get; }
    DbSet<CfgRolePermission> RolePermissions { get; }
    DbSet<CfgModelRole> ModelRoles { get; }
    DbSet<CfgModelPermission> ModelPermissions { get; }
    DbSet<TrGood> Goods { get; }
    DbSet<TrGoodDetail> GoodDetails { get; }
    DbSet<TrGoodAttachment> GoodAttachments { get; }
    DbSet<TrGoodStatusHistory> GoodStatusHistories { get; }
    DbSet<TrStockOpname> StockOpnames { get; }
    DbSet<TrStockOpnameDetail> StockOpnameDetails { get; }

    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    int? UserId { get; }
    bool HasPermission(string permission);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record TokenResult(string Token, DateTime ExpiresAt, string Jti);
public record RefreshTokenInfo(int UserId, string Jti, DateTime ExpiresAt);

public interface ITokenService
{
    TokenResult CreateAccessToken(int userId, string name, string email, string nik, IEnumerable<string> roles, IEnumerable<string> permissions);
    TokenResult CreateRefreshToken(int userId);
    RefreshTokenInfo? ValidateRefreshToken(string token);
}

/// <summary>Stateless JWTs + a revocation list (no extra DB table is allowed by the ERD).</summary>
public interface ITokenRevocation
{
    void Revoke(string jti, DateTime expiresAtUtc);
    bool IsRevoked(string jti);
}

public interface IFileStorage
{
    /// <summary>Saves a file and returns the relative storage path.</summary>
    Task<string> SaveAsync(string folder, string storedFileName, Stream content, CancellationToken ct);
}
