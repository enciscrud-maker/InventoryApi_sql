namespace Inventory.Application.Dtos;

// ---------- Auth ----------
public record LoginRequest(string Identifier, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);
public record UserProfileDto(int UserId, int EmployeeId, string EmployeeName, string EmployeeEmail, string EmployeeNik,
    string? ProfilePhotoUrl, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserProfileDto User);

// ---------- Employees / Users / Roles ----------
public record EmployeeRequest(string EmployeeName, string? EmployeePhone, string EmployeeNik, string? Position, string? Department, string? BusinessUnit);
public record EmployeeDto(int EmployeeId, string EmployeeName, string? EmployeePhone, string EmployeeNik, string? Position, string? Department, string? BusinessUnit, bool HasUser);
public record UserCreateRequest(int EmployeeId, string EmployeeEmail, string Password, string? ProfilePhotoUrl, List<int>? RoleIds);
public record UserUpdateRequest(string EmployeeEmail, string? Password, string? ProfilePhotoUrl);
public record UserStatusRequest(bool IsActive);
public record UserRolesRequest(List<int> RoleIds);
public record UserDto(int UserId, int EmployeeId, string EmployeeName, string EmployeeEmail, string EmployeeNik, bool IsActive,
    string? ProfilePhotoUrl, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<string> Roles);
public record RoleDto(int RoleId, string RoleName, string GuardName, IReadOnlyList<string> Permissions);
public record PermissionDto(int PermissionId, string PermissionName, string GuardName);
public class UserQuery : PageQuery { public bool? IsActive { get; set; } }

// ---------- Master data ----------
public record CategoryRequest(string CatName, string CatCode, string CatPrefix, bool IsActive = true);
public record CategoryDto(int CatId, string CatName, string CatCode, string CatPrefix, bool IsActive);
public record GroupRequest(int CatId, string GroupName, string GroupCode, string GroupPrefix, bool IsActive = true);
public record GroupDto(int GroupId, int CatId, string GroupName, string GroupCode, string GroupPrefix, bool IsActive);
public record TypeRequest(int GroupId, int CatId, string TypeName, string TypeCode, string TypePrefix, bool IsActive = true);
public record TypeDto(int TypeId, int GroupId, int CatId, string TypeName, string TypeCode, string TypePrefix, bool IsActive);
public record BrandRequest(int? TypeId, string BrandName, string BrandCode, string BrandPrefix, bool IsActive = true);
public record BrandDto(int BrandId, int? TypeId, string BrandName, string BrandCode, string BrandPrefix, bool IsActive);
public record ModelRequest(int? AssetId, int CatId, int BrandId, int GroupId, int TypeId, string ModelName, string ModelCode, bool IsActive = true);
public record ModelDto(int ModelId, int? AssetId, int CatId, int BrandId, int GroupId, int TypeId, string ModelName, string ModelCode, bool IsActive);
public record BusinessUnitRequest(string BusUnitCode, string BusUnitPrefix, bool IsActive = true);
public record BusinessUnitDto(int BusUnitId, string BusUnitCode, string BusUnitPrefix, bool IsActive);
public record LocationRequest(int BusUnitId, string LocationName);
public record LocationDto(int LocationId, int BusUnitId, string LocationName);
public record AssetStatusRequest(string StatusCode, string StatusName, bool IsActive = true);
public record AssetStatusDto(int StatusId, string StatusCode, string StatusName, bool IsActive);
public record ConditionRequest(string ConditionCode, string ConditionName, bool IsActive = true);
public record ConditionDto(int ConditionId, string ConditionCode, string ConditionName, bool IsActive);

// ---------- Assets ----------
public record AssetRequest(int CatId, int GroupId, int TypeId, int BrandId, string AssetName, AssetType AssetType, int StatusId, string AssetPrefix,
    int? Quantity, string? UnitOfMeasure, string? SerialNumber, string? AssetTag, string? PurchaseOrderNumber, string? SapMaterialNumber,
    decimal? AssetValue, string? Description, int? BusUnitId, int? ConditionId, string? BusinessUnit, string? RowVersion);
public record AssetDto(int AssetId, int CatId, string CatName, int GroupId, string GroupName, int TypeId, string TypeName, int BrandId, string BrandName,
    string AssetName, AssetType AssetType, int StatusId, string AssetStatus, string AssetPrefix, DateTime CreatedDt, int Quantity, string? UnitOfMeasure,
    string? SerialNumber, string? AssetTag, string? PurchaseOrderNumber, string? SapMaterialNumber, decimal? AssetValue, string? Description,
    int? BusUnitId, int? ConditionId, string? ConditionName, string? BusinessUnit, DateTime? UpdatedDt, string RowVersion);
public class AssetQuery : PageQuery
{
    public int? CatId { get; set; }
    public int? GroupId { get; set; }
    public int? TypeId { get; set; }
    public int? BrandId { get; set; }
    public int? BusUnitId { get; set; }
    public string? BusinessUnit { get; set; }
    public int? ConditionId { get; set; }
    public AssetType? AssetType { get; set; }
    public int? StatusId { get; set; }
}
public record BulkBusinessUnitRequest(List<int> AssetIds, int BusUnitId, string? BusinessUnit);
public record BulkBusinessUnitResult(int UpdatedCount);
public record ImportError(int RowNumber, List<string> Messages);
public record ImportResult(int TotalRows, int SuccessCount, int FailedCount, List<ImportError> Errors);

// ---------- Goods ----------
public record GoodItemRequest(int AssetId, int Quantity, string? SerialNumber, string? ReferenceNumber, string? AssetInformation, string? Condition,
    string? Remarks, string? Specifications, string? RequestJustification, string? RequiredDuration, string? IntendedPurpose);
public record GoodCreateRequest(string RequestId, string GoodName, TransactionType TransactionType, string BusinessUnit, DateTime RequestDate, DateTime FormIssueDate,
    string PersonName, string PersonNik, string PersonDepartment, string DestinationBusinessUnit, string? RequestNumber, string ApproverName, string? InventoryNotes,
    List<GoodItemRequest> Items);
public record GoodStatusRequest(GoodStatus Status, string? Notes);
public record GoodListItemDto(int GoodId, DateTime Date, string DocumentNumber, int TotalAssets, TransactionType TransactionType,
    string SourceOrRecipient, string Destination, GoodStatus Status);
public record GoodItemDto(int GoodDetailId, int AssetId, string AssetName, string AssetPrefix, int Quantity, string? SerialNumber, string? ReferenceNumber,
    string? AssetInformation, string? Condition, string? Remarks, string? Specifications, string? RequestJustification, string? RequiredDuration, string? IntendedPurpose);
public record GoodAttachmentDto(int GoodAttachmentId, string OriginalFileName, string ContentType, long FileSize, string StoragePath, DateTime UploadedAt);
public record GoodStatusHistoryDto(int GoodStatusHistoryId, GoodStatus PreviousStatus, GoodStatus NewStatus, int ActorUserId, string ActorName, string ActorNik, DateTime ChangedAt, string? Notes);
public record GoodDetailDto(int GoodId, string DocumentNumber, string GoodName, GoodType GoodType, TransactionType TransactionType, GoodStatus Status, string GoodPrefix,
    DateTime CreatedDt, string RequestId, string BusinessUnit, DateTime RequestDate, DateTime FormIssueDate, string PersonName, string PersonNik, string PersonDepartment,
    string DestinationBusinessUnit, string? RequestNumber, string ApproverName, string? InventoryNotes, bool InventoryAdjustmentApplied, DateTime? InventoryAdjustedAt,
    int CreatedByUserId, int TotalAssets, IReadOnlyList<GoodItemDto> Items, IReadOnlyList<GoodAttachmentDto> Attachments, IReadOnlyList<GoodStatusHistoryDto> StatusHistory, string RowVersion);
public class GoodQuery : PageQuery
{
    public GoodStatus? Status { get; set; }
    public TransactionType? TransactionType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

// ---------- Stock audit ----------
public record AuditCreateRequest(string StockOpnameName, StockOpnameType StockOpnameType, int AssetId, DateTime AuditDate, string? BusinessUnit, int? BusUnitId, string? StockOpnameStatus);
public record AuditUpdateRequest(string StockOpnameName, DateTime AuditDate, string? BusinessUnit, int? BusUnitId, string StockOpnameStatus);
public record AuditItemRequest(int? StockOpnameDetailId, int AssetId, int PhysicalQuantity, int? ActualBusUnitId, string? Finding, bool IsConfirmed, string? Notes);
public record AuditItemsRequest(List<AuditItemRequest> Items);
public record AuditItemDto(int StockOpnameDetailId, int AssetId, string AssetName, string AssetPrefix, int SystemQuantity, int PhysicalQuantity, int Variance,
    int? ActualBusUnitId, string? Finding, bool IsConfirmed, string? Notes, string? EvidencePath);
public record AuditListItemDto(int StockOpnameId, string StockOpnameNumber, string StockOpnameName, DateTime AuditDate, StockOpnameType StockOpnameType, string StockOpnameStatus,
    string? BusinessUnit, int? BusUnitId, int ItemCount, int DiscrepancyCount);
public record AuditDetailDto(int StockOpnameId, string StockOpnameNumber, string StockOpnameName, DateTime AuditDate, StockOpnameType StockOpnameType, string StockOpnameStatus,
    string StockOpnamePrefix, int AssetId, int AuditorUserId, string? BusinessUnit, int? BusUnitId, DateTime CreatedDt, DateTime? UpdatedDt, IReadOnlyList<AuditItemDto> Items);
public class AuditQuery : PageQuery
{
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? BusUnitId { get; set; }
    public string? BusinessUnit { get; set; }
}

// ---------- Dashboard & reports ----------
public record CategorySummaryDto(string Category, int AssetCount, int TotalQuantity);
public record DashboardSummaryDto(int TotalAssets, int TotalQuantity, int OutOfStockAssets, int GoodsInProgress, int GoodsInLast30Days, int GoodsOutLast30Days,
    int QuantityInLast30Days, int QuantityOutLast30Days, int OpenAudits, IReadOnlyList<CategorySummaryDto> AssetsByCategory, IReadOnlyList<GoodListItemDto> RecentTransactions);
public class ReportQuery : AssetQuery
{
    public string? Type { get; set; }          // assets | transactions | audits (export only)
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public GoodType? GoodType { get; set; }
    public GoodStatus? Status { get; set; }
    public TransactionType? TransactionType { get; set; }
    public string? AuditStatus { get; set; }
}
