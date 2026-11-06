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
    public const string MainAdminNik = "0000000001";
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
    public const string MasterLookupRead = "master.lookup.read";
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
    public const string GoodInRead = "good.in.read";
    public const string GoodInCreate = "good.in.create";
    public const string GoodInStatus = "good.in.status";
    public const string GoodOutRead = "good.out.read";
    public const string GoodOutCreate = "good.out.create";
    public const string GoodOutStatus = "good.out.status";
    public const string AuditRead = "audit.read";
    public const string AuditCreate = "audit.create";
    public const string AuditUpdate = "audit.update";
    public const string AuditApprove = "audit.approve";
    public const string AuditDelete = "audit.delete";
    public const string DashboardRead = "dashboard.read";
    public const string ReportRead = "report.read";
    public const string SettingsRead = "settings.read";
    public const string UserManage = "user.manage";

    public static readonly string[] All =
    {
        MasterRead, MasterLookupRead, MasterCreate, MasterUpdate, MasterDelete, AssetRead, AssetCreate, AssetUpdate, AssetImport, AssetExport,
        GoodRead, GoodCreate, GoodStatus, GoodInRead, GoodInCreate, GoodInStatus, GoodOutRead, GoodOutCreate, GoodOutStatus,
        AuditRead, AuditCreate, AuditUpdate, AuditApprove, AuditDelete, DashboardRead, ReportRead, SettingsRead, UserManage
    };

    public static readonly string[] MemberDefaults =
    {
        MasterRead, MasterLookupRead, AssetRead, GoodInRead, GoodOutRead, AuditRead, DashboardRead, ReportRead, SettingsRead
    };

    public static readonly string[] MemberCreatable =
    {
        MasterCreate, AssetCreate, GoodInCreate, GoodOutCreate, AuditCreate
    };

    public static readonly IReadOnlyDictionary<string, string[]> PagePermissions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["asset-inventory"] = new[] { MasterLookupRead, AssetRead, AssetCreate, AssetUpdate, AssetImport, AssetExport },
            ["goods-in"] = new[] { MasterLookupRead, AssetRead, GoodInRead, GoodInCreate, GoodInStatus },
            ["goods-out"] = new[] { MasterLookupRead, AssetRead, GoodOutRead, GoodOutCreate, GoodOutStatus },
            ["stock-opname"] = new[] { MasterLookupRead, AssetRead, AuditRead, AuditCreate, AuditUpdate, AuditApprove, AuditDelete },
            ["master-data"] = new[] { MasterLookupRead, MasterRead, MasterCreate, MasterUpdate, MasterDelete },
            ["reports"] = new[] { ReportRead },
            ["dashboard"] = new[] { MasterLookupRead, AssetRead, GoodInRead, GoodOutRead, AuditRead, DashboardRead },
            ["settings"] = new[] { SettingsRead },
        };

    public static readonly IReadOnlyDictionary<string, string> PageMarkers =
        PagePermissions.Keys.ToDictionary(page => page, page => $"page.{page}", StringComparer.Ordinal);

    public static readonly string[] AllPageMarkers = PageMarkers.Values.ToArray();
}
