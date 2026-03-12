namespace AccessControlPro.Domain.Enums;

/// <summary>
/// All granular permissions in the system.
/// Stored as comma-separated string on AppUser.Permissions.
/// </summary>
public static class AppPermission
{
    // App Access
    public const string AccessMainApp = "AccessMainApp";
    public const string AccessPOS = "AccessPOS";
    public const string AccessAdmin = "AccessAdmin";

    // Players
    public const string PlayersView = "Players.View";
    public const string PlayersAdd = "Players.Add";
    public const string PlayersEdit = "Players.Edit";
    public const string PlayersDelete = "Players.Delete";
    public const string PlayersAssignCard = "Players.AssignCard";
    public const string PlayersRemoveCard = "Players.RemoveCard";
    public const string PlayersFreeze = "Players.Freeze";
    public const string PlayersRenew = "Players.Renew";
    public const string PlayersReports = "Players.Reports";

    // Devices
    public const string DevicesView = "Devices.View";
    public const string DevicesAdd = "Devices.Add";
    public const string DevicesEdit = "Devices.Edit";
    public const string DevicesDelete = "Devices.Delete";
    public const string DevicesConnect = "Devices.Connect";

    // Doors
    public const string DoorsView = "Doors.View";
    public const string DoorsOpenClose = "Doors.OpenClose";
    public const string DoorsSettings = "Doors.Settings";

    // Events
    public const string EventsView = "Events.View";

    // Finance
    public const string FinanceView = "Finance.View";
    public const string FinanceManage = "Finance.Manage";

    // Cash Flow
    public const string CashFlowView = "CashFlow.View";
    public const string CashFlowManage = "CashFlow.Manage";

    // Logs
    public const string LogsView = "Logs.View";

    // Deleted Records
    public const string DeletedRecordsView = "DeletedRecords.View";

    // Monitor
    public const string MonitorView = "Monitor.View";

    // Admin Panel
    public const string AdminManageUsers = "Admin.ManageUsers";
    public const string AdminManageSettings = "Admin.ManageSettings";

    /// <summary>
    /// All permissions in the system, for Admin default.
    /// </summary>
    public static readonly string[] All =
    [
        AccessMainApp, AccessPOS, AccessAdmin,
        PlayersView, PlayersAdd, PlayersEdit, PlayersDelete,
        PlayersAssignCard, PlayersRemoveCard, PlayersFreeze, PlayersRenew, PlayersReports,
        DevicesView, DevicesAdd, DevicesEdit, DevicesDelete, DevicesConnect,
        DoorsView, DoorsOpenClose, DoorsSettings,
        EventsView,
        FinanceView, FinanceManage,
        CashFlowView, CashFlowManage,
        LogsView,
        DeletedRecordsView,
        MonitorView,
        AdminManageUsers, AdminManageSettings
    ];

    /// <summary>
    /// Default permissions for a new regular user.
    /// </summary>
    public static readonly string[] DefaultUser =
    [
        AccessMainApp,
        PlayersView,
        DevicesView,
        DoorsView,
        EventsView
    ];

    /// <summary>
    /// Permission groups for UI display.
    /// </summary>
    public static readonly (string GroupKey, string[] Permissions)[] Groups =
    [
        ("AppAccess", [AccessMainApp, AccessPOS, AccessAdmin]),
        ("Players", [PlayersView, PlayersAdd, PlayersEdit, PlayersDelete, PlayersAssignCard, PlayersRemoveCard, PlayersFreeze, PlayersRenew, PlayersReports]),
        ("Devices", [DevicesView, DevicesAdd, DevicesEdit, DevicesDelete, DevicesConnect]),
        ("Doors", [DoorsView, DoorsOpenClose, DoorsSettings]),
        ("Events", [EventsView]),
        ("Finance", [FinanceView, FinanceManage]),
        ("CashFlow", [CashFlowView, CashFlowManage]),
        ("Logs", [LogsView]),
        ("DeletedRecords", [DeletedRecordsView]),
        ("Monitor", [MonitorView]),
        ("Admin", [AdminManageUsers, AdminManageSettings])
    ];
}
