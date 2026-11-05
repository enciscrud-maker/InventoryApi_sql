namespace Inventory.Domain;

public enum AssetType { In, Out }
public enum GoodType { In, Out }
public enum GoodStatus { InProgress, Completed }
public enum StockOpnameType { In, Out }

/// <summary>Goods In: Return, NewUnit, AfterService. Goods Out: Borrowing, Service, Disposal, OffLease, Handover.</summary>
public enum TransactionType { Return, NewUnit, AfterService, Borrowing, Service, Disposal, OffLease, Handover }

public static class TransactionRules
{
    public static GoodType DirectionOf(TransactionType t) =>
        t is TransactionType.Return or TransactionType.NewUnit or TransactionType.AfterService ? GoodType.In : GoodType.Out;
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string Guard = "api";
}

public static class ConditionCodes
{
    public const string Disposal = "DISPOSAL";
}

public static class StockOpnameStatuses
{
    public const string Draft = "Draft";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public static readonly string[] All = { Draft, InProgress, Completed };
}

public static class Permissions
{
    public const string MasterRead = "master.read";
    public const string MasterCreate = "master.create";
    public const string MasterUpdate = "master.update";
    public const string MasterDelete = "master.delete";
    public const string AssetRead = "asset.read";
    public const string AssetCreate = "asset.create";
    public const string AssetUpdate = "asset.update";
    public const string AssetImport = "asset.import";
    public const string AssetExport = "asset.export";
    public const string GoodRead = "good.read";
    public const string GoodCreate = "good.create";
    public const string GoodStatus = "good.status";
    public const string AuditRead = "audit.read";
    public const string AuditCreate = "audit.create";
    public const string AuditUpdate = "audit.update";
    public const string AuditApprove = "audit.approve";
    public const string DashboardRead = "dashboard.read";
    public const string ReportRead = "report.read";
    public const string UserManage = "user.manage";

    public static readonly string[] All =
    {
        MasterRead, MasterCreate, MasterUpdate, MasterDelete, AssetRead, AssetCreate, AssetUpdate, AssetImport, AssetExport,
        GoodRead, GoodCreate, GoodStatus, AuditRead, AuditCreate, AuditUpdate, AuditApprove, DashboardRead, ReportRead, UserManage
    };

    /// <summary>Member = everything except delete, approval and user/role management.</summary>
    public static readonly string[] MemberDefaults = All.Where(p => p != MasterDelete && p != AuditApprove && p != UserManage).ToArray();
}
