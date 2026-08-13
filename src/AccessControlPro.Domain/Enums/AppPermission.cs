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
    public const string PlayersReassignCard = "Players.ReassignCard";
    public const string PlayersSyncToDevice = "Players.SyncToDevice";
    public const string PlayersDailyPass = "Players.DailyPass";
    public const string PlayersBulkOperations = "Players.BulkOperations";
    public const string PlayersCreateMissingCards = "Players.CreateMissingCards";
    public const string PlayersPrintList = "Players.PrintList";

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

    // Data Migration
    public const string DataMigration = "DataMigration.Access";

    // QR Pass
    public const string QrPassManage = "QrPass.Manage";

    // Reminders
    public const string RemindersView = "Reminders.View";

    // General (app-wide actions)
    public const string DiagnosticsSend = "Diagnostics.Send";
    public const string AppChangeLanguage = "App.ChangeLanguage";

    // Dashboard
    public const string DashboardView = "Dashboard.View";

    // POS
    public const string POSSales = "POS.Sales";
    public const string POSManageShift = "POS.ManageShift";
    public const string POSApplyDiscount = "POS.ApplyDiscount";
    public const string POSPrintReceipt = "POS.PrintReceipt";
    public const string POSViewSummary = "POS.ViewSummary";
    public const string POSRefund = "POS.Refund";

    // Time Groups
    public const string TimeGroupsView = "TimeGroups.View";
    public const string TimeGroupsManage = "TimeGroups.Manage";

    // Subscription Plans
    public const string SubscriptionPlansView = "SubscriptionPlans.View";
    public const string SubscriptionPlansManage = "SubscriptionPlans.Manage";

    // Backup
    public const string BackupView = "Backup.View";
    public const string BackupManage = "Backup.Manage";

    // Cloud Access
    public const string AccessCloud = "AccessCloud";

    // Admin Panel
    public const string AdminManageUsers = "Admin.ManageUsers";
    public const string AdminManageSettings = "Admin.ManageSettings";

    /// <summary>
    /// All permissions in the system, for Admin default.
    /// </summary>
    public static readonly string[] All =
    [
        AccessMainApp, AccessPOS, AccessAdmin, AccessCloud,
        DashboardView,
        PlayersView, PlayersAdd, PlayersEdit, PlayersDelete,
        PlayersAssignCard, PlayersRemoveCard, PlayersReassignCard, PlayersFreeze, PlayersRenew, PlayersReports,
        PlayersSyncToDevice, PlayersDailyPass, PlayersBulkOperations, PlayersCreateMissingCards, PlayersPrintList,
        DevicesView, DevicesAdd, DevicesEdit, DevicesDelete, DevicesConnect,
        DoorsView, DoorsOpenClose, DoorsSettings,
        EventsView,
        FinanceView, FinanceManage,
        CashFlowView, CashFlowManage,
        LogsView,
        DeletedRecordsView,
        MonitorView,
        DataMigration, QrPassManage,
        RemindersView,
        DiagnosticsSend, AppChangeLanguage,
        POSSales, POSManageShift, POSApplyDiscount, POSPrintReceipt, POSViewSummary, POSRefund,
        TimeGroupsView, TimeGroupsManage,
        SubscriptionPlansView, SubscriptionPlansManage,
        BackupView, BackupManage,
        AdminManageUsers, AdminManageSettings
    ];

    /// <summary>
    /// Default permissions for a new regular user.
    /// </summary>
    public static readonly string[] DefaultUser =
    [
        AccessMainApp,
        DashboardView,
        PlayersView,
        DevicesView,
        DoorsView,
        EventsView,
        AppChangeLanguage
    ];

    /// <summary>
    /// Permission groups for UI display.
    /// </summary>
    public static readonly (string GroupKey, string[] Permissions)[] Groups =
    [
        ("AppAccess", [AccessMainApp, AccessPOS, AccessAdmin, AccessCloud]),
        ("Dashboard", [DashboardView]),
        ("Players", [PlayersView, PlayersAdd, PlayersEdit, PlayersDelete, PlayersAssignCard, PlayersRemoveCard, PlayersReassignCard, PlayersFreeze, PlayersRenew, PlayersReports, PlayersSyncToDevice, PlayersDailyPass, PlayersBulkOperations, PlayersCreateMissingCards, PlayersPrintList]),
        ("Devices", [DevicesView, DevicesAdd, DevicesEdit, DevicesDelete, DevicesConnect]),
        ("Doors", [DoorsView, DoorsOpenClose, DoorsSettings]),
        ("Events", [EventsView]),
        ("Finance", [FinanceView, FinanceManage]),
        ("CashFlow", [CashFlowView, CashFlowManage]),
        ("POS", [POSSales, POSManageShift, POSApplyDiscount, POSPrintReceipt, POSViewSummary, POSRefund]),
        ("TimeGroups", [TimeGroupsView, TimeGroupsManage]),
        ("SubscriptionPlans", [SubscriptionPlansView, SubscriptionPlansManage]),
        ("Logs", [LogsView]),
        ("DeletedRecords", [DeletedRecordsView]),
        ("Monitor", [MonitorView]),
        ("DataMigration", [DataMigration]),
        ("QrPass", [QrPassManage]),
        ("Reminders", [RemindersView]),
        ("General", [DiagnosticsSend, AppChangeLanguage]),
        ("Backup", [BackupView, BackupManage]),
        ("Admin", [AdminManageUsers, AdminManageSettings])
    ];
}
