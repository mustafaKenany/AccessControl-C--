using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Windows;

namespace AccessControlPro.WPF.Helpers;

public class LanguageManager : INotifyPropertyChanged
{
    private static readonly Lazy<LanguageManager> _instance = new(() => new LanguageManager());
    public static LanguageManager Instance => _instance.Value;

    private readonly ResourceManager _resourceManager;

    public event PropertyChangedEventHandler? PropertyChanged;

    private FlowDirection _flowDirection = FlowDirection.LeftToRight;
    private bool _isArabic;
    private CultureInfo _currentCulture = CultureInfo.InvariantCulture;

    private LanguageManager()
    {
        _resourceManager = new ResourceManager("AccessControlPro.WPF.Resources.Strings",
            typeof(LanguageManager).Assembly);
    }

    public FlowDirection FlowDirection
    {
        get => _flowDirection;
        private set
        {
            _flowDirection = value;
            OnPropertyChanged(nameof(FlowDirection));
        }
    }

    public bool IsArabic
    {
        get => _isArabic;
        private set
        {
            _isArabic = value;
            OnPropertyChanged(nameof(IsArabic));
            OnPropertyChanged(nameof(LanguageButtonText));
        }
    }

    public string LanguageButtonText => IsArabic ? "EN" : "عربي";

    // Localized string properties
    public string AppTitle => GetString("AppTitle");
    public string NavDashboard => GetString("NavDashboard");
    public string NavDevices => GetString("NavDevices");
    public string NavDoors => GetString("NavDoors");
    public string NavEmployees => GetString("NavEmployees");
    public string NavCards => GetString("NavCards");
    public string NavEvents => GetString("NavEvents");
    public string NavSettings => GetString("NavSettings");
    public string NavFinance => GetString("NavFinance");
    public string NavCashFlow => GetString("NavCashFlow");
    public string NavPOS => GetString("NavPOS");
    public string NavUsers => GetString("NavUsers");
    public string TotalDevices => GetString("TotalDevices");
    public string OnlineDoors => GetString("OnlineDoors");
    public string TodayEvents => GetString("TodayEvents");
    public string ActiveAlarms => GetString("ActiveAlarms");
    public string DoorStatus => GetString("DoorStatus");
    public string RecentEvents => GetString("RecentEvents");
    public string Refresh => GetString("Refresh");
    public string StatusOpen => GetString("StatusOpen");
    public string StatusClosed => GetString("StatusClosed");
    public string StatusAlarm => GetString("StatusAlarm");
    public string StatusFault => GetString("StatusFault");
    public string Time => GetString("Time");
    public string Door => GetString("Door");
    public string Card => GetString("Card");
    public string Event => GetString("Event");
    public string Language => GetString("Language");

    // Devices Page
    public string SearchNetwork => GetString("SearchNetwork");
    public string AddDevice => GetString("AddDevice");
    public string SearchPlaceholder => GetString("SearchPlaceholder");
    public string Online => GetString("Online");
    public string Offline => GetString("Offline");
    public string Total => GetString("Total");
    public string DeviceName => GetString("DeviceName");
    public string Model => GetString("Model");
    public string IPAddress => GetString("IPAddress");
    public string MACAddress => GetString("MACAddress");
    public string Port => GetString("Port");
    public string Doors => GetString("Doors");
    public string Status => GetString("Status");
    public string Actions => GetString("Actions");
    public string Edit => GetString("Edit");
    public string Delete => GetString("Delete");
    public string NoDevices => GetString("NoDevices");
    public string ConfirmDelete => GetString("ConfirmDelete");

    // Device Actions
    public string Connect => GetString("Connect");
    public string DeviceInfo => GetString("DeviceInfo");
    public string OpenDoor => GetString("OpenDoor");
    public string SyncTime => GetString("SyncTime");
    public string ConnectSuccess => GetString("ConnectSuccess");
    public string OpenDoorSuccess => GetString("OpenDoorSuccess");
    public string SyncTimeSuccess => GetString("SyncTimeSuccess");
    public string SelectDoor => GetString("SelectDoor");

    // Doors Page
    public string CloseDoor => GetString("CloseDoor");
    public string SetDelay => GetString("SetDelay");
    public string SetPassword => GetString("SetPassword");
    public string CloseDoorSuccess => GetString("CloseDoorSuccess");
    public string SetDelaySuccess => GetString("SetDelaySuccess");
    public string SetPasswordSuccess => GetString("SetPasswordSuccess");
    public string EnterDelay => GetString("EnterDelay");
    public string EnterPassword => GetString("EnterPassword");
    public string DoorNumber => GetString("DoorNumber");

    // Rename
    public string EnterName => GetString("EnterName");
    public string RenameSuccess => GetString("RenameSuccess");

    // Change IP
    public string ChangeIP => GetString("ChangeIP");
    public string EnterIP => GetString("EnterIP");
    public string ChangeIPSuccess => GetString("ChangeIPSuccess");

    // Players Page
    public string NavPlayers => GetString("NavPlayers");
    public string AddPlayer => GetString("AddPlayer");
    public string AddPlayerSuccess => GetString("AddPlayerSuccess");
    public string EditPlayerSuccess => GetString("EditPlayerSuccess");
    public string ConfirmDeletePlayer => GetString("ConfirmDeletePlayer");
    public string PlayerName => GetString("PlayerName");
    public string CardNo => GetString("CardNo");
    public string SearchBoxHint => GetString("SearchBoxHint");
    public string Subscription => GetString("Subscription");
    public string Fee => GetString("Fee");
    public string Paid => GetString("Paid");
    public string Remaining => GetString("Remaining");
    public string WithCard => GetString("WithCard");
    public string WithoutCard => GetString("WithoutCard");
    public string AssignCard => GetString("AssignCard");
    public string AssignCardSuccess => GetString("AssignCardSuccess");
    public string RemoveCard => GetString("RemoveCard");
    public string RemoveCardSuccess => GetString("RemoveCardSuccess");
    public string ConfirmRemoveCard => GetString("ConfirmRemoveCard");
    public string SelectCardToRemove => GetString("SelectCardToRemove");
    public string ViewCards => GetString("ViewCards");
    public string NoCardsAssigned => GetString("NoCardsAssigned");
    public string ShowAll => GetString("ShowAll");
    public string ClickShowAll => GetString("ClickShowAll");
    public string Loading => GetString("Loading");
    public string Page => GetString("Page");
    public string Phone => GetString("Phone");
    public string Notes => GetString("Notes");
    public string Photo => GetString("Photo");
    public string SyncToDevice => GetString("SyncToDevice");
    public string SyncSuccess => GetString("SyncSuccess");

    // Dialog Labels
    public string EditPlayer => GetString("EditPlayer");
    public string NameEn => GetString("NameEn");
    public string NameAr => GetString("NameAr");
    public string Period => GetString("Period");
    public string StartDate => GetString("StartDate");
    public string EndDate => GetString("EndDate");
    public string HeightCm => GetString("HeightCm");
    public string WeightKg => GetString("WeightKg");
    public string BrowsePhoto => GetString("BrowsePhoto");
    public string RemovePhoto => GetString("RemovePhoto");
    public string Cancel => GetString("Cancel");
    public string OK => GetString("OK");
    public string IQD => GetString("IQD");
    public string Month1 => GetString("Month1");
    public string Months3 => GetString("Months3");
    public string Months6 => GetString("Months6");
    public string Months12 => GetString("Months12");
    public string CustomPeriod => GetString("CustomPeriod");
    public string PlayerNameRequired => GetString("PlayerNameRequired");
    public string CardNoRequired => GetString("CardNoRequired");
    public string ValidationTitle => GetString("ValidationTitle");
    public string SelectPhoto => GetString("SelectPhoto");
    public string Submit => GetString("Submit");
    public string NameArRequired => GetString("NameArRequired");
    public string PhoneRequired => GetString("PhoneRequired");
    public string SubscriptionRequired => GetString("SubscriptionRequired");
    public string PeriodRequired => GetString("PeriodRequired");
    public string FeeRequired => GetString("FeeRequired");
    public string PhotoRequired => GetString("PhotoRequired");
    public string HeightInvalid => GetString("HeightInvalid");
    public string WeightInvalid => GetString("WeightInvalid");
    public string OptionalInfo => GetString("OptionalInfo");

    // Assign Card Dialog
    public string AuthorizeCard => GetString("AuthorizeCard");
    public string CardNumber => GetString("CardNumber");
    public string CardPassword => GetString("CardPassword");
    public string CardMode => GetString("CardMode");
    public string CardType => GetString("CardType");
    public string DoorAccess => GetString("DoorAccess");
    public string EffectiveTimes => GetString("EffectiveTimes");
    public string MaxVisits => GetString("MaxVisits");
    public string TimePeriod => GetString("TimePeriod");
    public string HolidayAccess => GetString("HolidayAccess");
    public string ValidUntil => GetString("ValidUntil");
    public string SelectDevices => GetString("SelectDevices");
    public string Ordinary => GetString("Ordinary");
    public string FirstCardPrivilege => GetString("FirstCardPrivilege");
    public string AlwaysOpenPrivilege => GetString("AlwaysOpenPrivilege");
    public string PatrolCheckIn => GetString("PatrolCheckIn");
    public string AntiTheftSetting => GetString("AntiTheftSetting");
    public string Standard => GetString("Standard");
    public string VIP => GetString("VIP");
    public string Temporary => GetString("Temporary");
    public string Unlimited => GetString("Unlimited");
    public string InvalidateImmediately => GetString("InvalidateImmediately");
    public string CardNumberRequired => GetString("CardNumberRequired");
    public string ValidFromBeforeValidTo => GetString("ValidFromBeforeValidTo");
    public string SelectAtLeastOneDevice => GetString("SelectAtLeastOneDevice");
    public string SelectAtLeastOneDoor => GetString("SelectAtLeastOneDoor");
    public string NoDevicesAvailable => GetString("NoDevicesAvailable");

    // Duplicate Validation
    public string DuplicateCardNo => GetString("DuplicateCardNo");
    public string DuplicatePhone => GetString("DuplicatePhone");
    public string DuplicateNameWarning => GetString("DuplicateNameWarning");

    // Confirm Dialog
    public string Yes => GetString("Yes");
    public string No => GetString("No");

    // Delete with Reason
    public string DeleteReason => GetString("DeleteReason");
    public string EnterDeleteReason => GetString("EnterDeleteReason");
    public string DeleteReasonRequired => GetString("DeleteReasonRequired");
    public string DeletePlayerSuccess => GetString("DeletePlayerSuccess");

    // Freeze / Unfreeze
    public string FreezePlayer => GetString("FreezePlayer");
    public string UnfreezePlayer => GetString("UnfreezePlayer");
    public string EnterFreezeReason => GetString("EnterFreezeReason");
    public string ConfirmUnfreeze => GetString("ConfirmUnfreeze");
    public string FreezeSuccess => GetString("FreezeSuccess");
    public string UnfreezeSuccess => GetString("UnfreezeSuccess");

    // Renew Subscription
    public string RenewSubscription => GetString("RenewSubscription");
    public string RenewSuccess => GetString("RenewSuccess");
    public string MigratedRenewBlockedTitle => GetString("MigratedRenewBlockedTitle");
    public string MigratedRenewBlockedMessage => GetString("MigratedRenewBlockedMessage");
    public string MigratedRenewStillBlocked => GetString("MigratedRenewStillBlocked");

    // Edit Reason
    public string EditReason => GetString("EditReason");
    public string EnterEditReason => GetString("EnterEditReason");
    public string EditReasonRequired => GetString("EditReasonRequired");

    // Logs Section
    public string NavLogs => GetString("NavLogs");
    public string LogTimestamp => GetString("LogTimestamp");
    public string LogAction => GetString("LogAction");
    public string LogEntity => GetString("LogEntity");
    public string LogDetails => GetString("LogDetails");
    public string ClickShowAllLogs => GetString("ClickShowAllLogs");
    public string LogYesterday => GetString("LogYesterday");
    public string LogLast3Months => GetString("LogLast3Months");

    // Deleted Records Section
    public string NavDeletedRecords => GetString("NavDeletedRecords");
    public string DeletedDate => GetString("DeletedDate");
    public string ClickShowAllDeleted => GetString("ClickShowAllDeleted");
    public string DeletedByPrefix => GetString("DeletedByPrefix");
    public string DeletedByCol => GetString("DeletedByCol");

    // Log Actions (localized)
    public string ActionCreate => GetString("ActionCreate");
    public string ActionUpdate => GetString("ActionUpdate");
    public string ActionDelete => GetString("ActionDelete");
    public string ActionSoftDelete => GetString("ActionSoftDelete");
    public string ActionFreeze => GetString("ActionFreeze");
    public string ActionUnfreeze => GetString("ActionUnfreeze");
    public string ActionRenew => GetString("ActionRenew");
    public string ActionSyncCard => GetString("ActionSyncCard");

    // Entity Types (localized)
    public string EntityPlayer => GetString("EntityPlayer");
    public string EntityAccessCard => GetString("EntityAccessCard");
    public string EntityDevice => GetString("EntityDevice");

    // Events & Monitor
    public string Device => GetString("Device");
    public string Player => GetString("Player");
    public string EventType => GetString("EventType");
    public string Direction => GetString("Direction");
    public string ClickShowAllEvents => GetString("ClickShowAllEvents");
    public string NavMonitor => GetString("NavMonitor");
    public string MonitorTitle => GetString("MonitorTitle");
    public string StartMonitor => GetString("StartMonitor");
    public string StopMonitor => GetString("StopMonitor");
    public string MonitorReady => GetString("MonitorReady");
    public string DownloadLogs => GetString("DownloadLogs");
    public string EventCardAction => GetString("EventCardAction");
    public string EventButtonAction => GetString("EventButtonAction");
    public string EventSoftwareAction => GetString("EventSoftwareAction");
    public string EventDoorSensor => GetString("EventDoorSensor");
    public string EventAlarm => GetString("EventAlarm");
    public string EventSystem => GetString("EventSystem");
    public string EntryLabel => GetString("Entry");
    public string ExitLabel => GetString("Exit");

    public string GetEventActionLabel(string eventType) => eventType switch
    {
        "Card" => EventCardAction,
        "Button" => EventButtonAction,
        "Software" => EventSoftwareAction,
        "DoorSensor" => EventDoorSensor,
        "Alarm" => EventAlarm,
        "System" => EventSystem,
        _ => eventType
    };

    public string GetDirectionLabel(string direction) => direction switch
    {
        "Entry" => EntryLabel,
        "Exit" => ExitLabel,
        _ => direction
    };

    // Card Status
    public string CardActive => GetString("CardActive");
    public string CardExpired => GetString("CardExpired");
    public string CardFrozen => GetString("CardFrozen");
    public string CardUnregistered => GetString("CardUnregistered");

    public string GetCardStatusLabel(string status) => status switch
    {
        "Active" => CardActive,
        "Expired" => CardExpired,
        "Frozen" => CardFrozen,
        "Unregistered" => CardUnregistered,
        _ => status
    };
    // Finance
    public string FinTotalRevenue => GetString("FinTotalRevenue");
    public string FinTotalExpenses => GetString("FinTotalExpenses");
    public string FinNetProfit => GetString("FinNetProfit");
    public string FinUnpaidBalances => GetString("FinUnpaidBalances");
    public string FinOutstandingPlayers => GetString("FinOutstandingPlayers");
    public string FinRecentTransactions => GetString("FinRecentTransactions");
    public string FinPayNow => GetString("FinPayNow");
    public string FinType => GetString("FinType");
    public string FinCategory => GetString("FinCategory");
    public string FinAmount => GetString("FinAmount");
    public string FinDescription => GetString("FinDescription");
    public string FinPayment => GetString("FinPayment");
    public string FinAddExpense => GetString("FinAddExpense");
    public string FinAddIncome => GetString("FinAddIncome");
    public string FinAll => GetString("FinAll");
    public string FinIncome => GetString("FinIncome");
    public string FinExpense => GetString("FinExpense");
    public string FinPeriod => GetString("FinPeriod");
    public string FinToday => GetString("FinToday");
    public string FinYesterday => GetString("FinYesterday");
    public string FinThisWeek => GetString("FinThisWeek");
    public string FinLastWeek => GetString("FinLastWeek");
    public string FinThisMonth => GetString("FinThisMonth");
    public string FinLastMonth => GetString("FinLastMonth");
    public string FinLast3Months => GetString("FinLast3Months");
    public string FinLast6Months => GetString("FinLast6Months");
    public string FinThisYear => GetString("FinThisYear");
    public string FinSearch => GetString("FinSearch");

    // Income Categories
    public string FinIncCatSubscription => GetString("FinIncCatSubscription");
    public string FinIncCatPOSSales => GetString("FinIncCatPOSSales");
    public string FinIncCatOwnerDeposit => GetString("FinIncCatOwnerDeposit");
    public string FinIncCatOther => GetString("FinIncCatOther");

    // Expense Categories
    public string FinExpCatRent => GetString("FinExpCatRent");
    public string FinExpCatElectricity => GetString("FinExpCatElectricity");
    public string FinExpCatWater => GetString("FinExpCatWater");
    public string FinExpCatSalaries => GetString("FinExpCatSalaries");
    public string FinExpCatEquipment => GetString("FinExpCatEquipment");
    public string FinExpCatMaintenance => GetString("FinExpCatMaintenance");
    public string FinExpCatSupplies => GetString("FinExpCatSupplies");
    public string FinExpCatMarketing => GetString("FinExpCatMarketing");

    // POS
    public string PosAddProduct => GetString("PosAddProduct");
    public string PosCart => GetString("PosCart");
    public string PosPayCash => GetString("PosPayCash");
    public string PosPayCard => GetString("PosPayCard");
    public string PosPayCredit => GetString("PosPayCredit");
    public string PosCollectDebt => GetString("PosCollectDebt");
    public string PosTopUp => GetString("PosTopUp");
    public string PosCardBalance => GetString("PosCardBalance");
    public string PosPrice => GetString("PosPrice");
    public string PosStock => GetString("PosStock");
    public string PosPlayerNotFound => GetString("PosPlayerNotFound");
    public string PosSearchPlayerFirst => GetString("PosSearchPlayerFirst");
    public string PosSaleSuccess => GetString("PosSaleSuccess");
    public string PosInsufficientBalance => GetString("PosInsufficientBalance");
    public string PosSuccess => GetString("PosSuccess");

    // Filters & Reports
    public string FilterAll => GetString("FilterAll");
    public string RptReports => GetString("RptReports");
    public string RptExpiring => GetString("RptExpiring");
    public string RptRenewed => GetString("RptRenewed");
    public string RptFrozen => GetString("RptFrozen");
    public string RptExpired => GetString("RptExpired");
    public string RptToday => GetString("RptToday");
    public string RptThisWeek => GetString("RptThisWeek");
    public string RptLastWeek => GetString("RptLastWeek");
    public string RptThisMonth => GetString("RptThisMonth");
    public string RptLastMonth => GetString("RptLastMonth");
    public string RptResults => GetString("RptResults");
    public string RptNoResults => GetString("RptNoResults");
    public string RptEndDate => GetString("RptEndDate");
    public string RptStartDate => GetString("RptStartDate");
    public string RptSubscription => GetString("RptSubscription");
    public string RptPhone => GetString("RptPhone");

    // Permissions
    public string PermPermissions => GetString("PermPermissions");
    public string PermSelectAll => GetString("PermSelectAll");
    public string PermClearAll => GetString("PermClearAll");
    public string PermAdminNote => GetString("PermAdminNote");
    public string PermAccessDenied => GetString("PermAccessDenied");
    public string PermNoPermission => GetString("PermNoPermission");

    // Admin Users Page
    public string UsrAddUser => GetString("UsrAddUser");
    public string UsrAddNewUser => GetString("UsrAddNewUser");
    public string UsrEditUser => GetString("UsrEditUser");
    public string UsrUsername => GetString("UsrUsername");
    public string UsrDisplayName => GetString("UsrDisplayName");
    public string UsrRole => GetString("UsrRole");
    public string UsrPassword => GetString("UsrPassword");
    public string UsrNewPassword => GetString("UsrNewPassword");
    public string UsrAccountActive => GetString("UsrAccountActive");
    public string UsrSave => GetString("UsrSave");
    public string UsrCancel => GetString("UsrCancel");
    public string UsrStatus => GetString("UsrStatus");
    public string UsrActive => GetString("UsrActive");
    public string UsrDisabled => GetString("UsrDisabled");
    public string UsrID => GetString("UsrID");
    public string UsrSubtitle => GetString("UsrSubtitle");
    public string UsrLoading => GetString("UsrLoading");
    public string UsrUsernameRequired => GetString("UsrUsernameRequired");
    public string UsrPasswordRequired => GetString("UsrPasswordRequired");
    public string UsrDisplayNameRequired => GetString("UsrDisplayNameRequired");
    public string UsrSearch => GetString("UsrSearch");
    public string UsrTotalUsers => GetString("UsrTotalUsers");
    public string UsrActiveUsers => GetString("UsrActiveUsers");
    public string UsrInactiveUsers => GetString("UsrInactiveUsers");
    public string UsrDeactivate => GetString("UsrDeactivate");
    public string UsrActivate => GetString("UsrActivate");
    public string UsrConfirmDeactivate => GetString("UsrConfirmDeactivate");
    public string UsrConfirmActivate => GetString("UsrConfirmActivate");
    public string UsrDeactivateReason => GetString("UsrDeactivateReason");
    public string UsrDeleteReason => GetString("UsrDeleteReason");
    public string UsrActions => GetString("UsrActions");

    /// <summary>
    /// Returns the localized display name for a permission key (e.g., "Players.View" -> "View Players" / "عرض اللاعبين").
    /// </summary>
    public string GetPermissionDisplayName(string permissionKey)
    {
        var resourceKey = "Perm" + permissionKey.Replace(".", "");
        var result = GetString(resourceKey);
        return result == resourceKey ? permissionKey : result;
    }

    /// <summary>
    /// Returns the localized group name for a permission group key (e.g., "Players" -> "اللاعبون").
    /// </summary>
    public string GetPermissionGroupName(string groupKey)
    {
        var resourceKey = "PermGroup" + groupKey;
        var result = GetString(resourceKey);
        return result == resourceKey ? groupKey : result;
    }

    // Renew Subscription Enhanced
    public string CustomDays => GetString("CustomDays");
    public string EnterDays => GetString("EnterDays");
    public string DaysRequired => GetString("DaysRequired");

    public string AllDevices => GetString("AllDevices");
    public string SelectDevice => GetString("SelectDevice");
    public string SelectPeriod => GetString("SelectPeriod");
    public string SelectPeriodDesc => GetString("SelectPeriodDesc");
    public string Last1Month => GetString("Last1Month");
    public string Last3Months => GetString("Last3Months");
    public string Last6Months => GetString("Last6Months");
    public string Last1Year => GetString("Last1Year");
    public string Download => GetString("Download");

    // Login
    public string Login => GetString("Login");
    public string Username => GetString("Username");
    public string LoginButton => GetString("LoginButton");
    public string LoginFailed => GetString("LoginFailed");
    public string LoggingIn => GetString("LoggingIn");
    public string LogUser => GetString("LogUser");
    public string WelcomeBack => GetString("WelcomeBack");
    public string PoweredBy => GetString("PoweredBy");
    public string UsernameRequired => GetString("UsernameRequired");
    public string PasswordRequired => GetString("PasswordRequired");
    public string UserNotFound => GetString("UserNotFound");
    public string WrongPassword => GetString("WrongPassword");
    public string AccountDisabled => GetString("AccountDisabled");
    public string Password => GetString("Password");

    // Change Password
    public string CpwChangePassword => GetString("CpwChangePassword");
    public string CpwCurrentPassword => GetString("CpwCurrentPassword");
    public string CpwNewPassword => GetString("CpwNewPassword");
    public string CpwConfirmPassword => GetString("CpwConfirmPassword");
    public string CpwPasswordMismatch => GetString("CpwPasswordMismatch");
    public string CpwMinLength => GetString("CpwMinLength");
    public string CpwPasswordChanged => GetString("CpwPasswordChanged");
    public string CpwDefaultPasswordWarning => GetString("CpwDefaultPasswordWarning");

    // Diagnostics
    public string DiagSendDiagnostics => GetString("DiagSendDiagnostics");
    public string DiagConfirmTitle => GetString("DiagConfirmTitle");
    public string DiagConfirmBody => GetString("DiagConfirmBody");
    public string DiagUploading => GetString("DiagUploading");
    public string DiagUploadSuccess => GetString("DiagUploadSuccess");
    public string DiagUploadFailed => GetString("DiagUploadFailed");
    public string DiagNoteLabel => GetString("DiagNoteLabel");
    public string DiagNoteHint => GetString("DiagNoteHint");
    public string DiagSendNow => GetString("DiagSendNow");

    // Settings
    public string SetSaved => GetString("SetSaved");
    public string SetBrowseLogo => GetString("SetBrowseLogo");
    public string SetRemoveLogo => GetString("SetRemoveLogo");
    public string SetCompanyName => GetString("SetCompanyName");
    public string SetGymName => GetString("SetGymName");
    public string SetOwner => GetString("SetOwner");

    // Categories Management
    public string NavCategories => GetString("NavCategories");
    public string CatTitle => GetString("CatTitle");
    public string CatSubtitle => GetString("CatSubtitle");
    public string CatType => GetString("CatType");
    public string CatNameEn => GetString("CatNameEn");
    public string CatNameAr => GetString("CatNameAr");
    public string CatRate => GetString("CatRate");
    public string CatAddNew => GetString("CatAddNew");
    public string CatEdit => GetString("CatEdit");
    public string CatDelete => GetString("CatDelete");

    // Products Management (Admin)
    public string NavProducts => GetString("NavProducts");
    public string PrdTitle => GetString("PrdTitle");
    public string PrdSubtitle => GetString("PrdSubtitle");
    public string PrdName => GetString("PrdName");
    public string PrdNameAr => GetString("PrdNameAr");
    public string PrdBarcode => GetString("PrdBarcode");
    public string PrdPrice => GetString("PrdPrice");
    public string PrdCostPrice => GetString("PrdCostPrice");
    public string PrdProfit => GetString("PrdProfit");
    public string PrdCategory => GetString("PrdCategory");
    public string PrdStock => GetString("PrdStock");
    public string PrdAddNew => GetString("PrdAddNew");
    public string PrdEdit => GetString("PrdEdit");

    // Suppliers Management (Admin)
    public string NavSuppliers => GetString("NavSuppliers");
    public string NavReminders => GetString("NavReminders");
    public string RemindersSubtitle => GetString("RemindersSubtitle");
    public string RemindersWithin => GetString("RemindersWithin");
    public string RemindersDays => GetString("RemindersDays");
    public string RemindersIncludeExpired => GetString("RemindersIncludeExpired");
    public string RemindersTemplate => GetString("RemindersTemplate");
    public string RemindersHint => GetString("RemindersHint");
    public string RemindersStatus => GetString("RemindersStatus");
    public string RemindersNone => GetString("RemindersNone");
    public string RemindersRefresh => GetString("RemindersRefresh");
    public string SupTitle => GetString("SupTitle");
    public string SupSubtitle => GetString("SupSubtitle");
    public string SupName => GetString("SupName");
    public string SupPhone => GetString("SupPhone");
    public string SupAddress => GetString("SupAddress");
    public string SupContact => GetString("SupContact");
    public string SupAddNew => GetString("SupAddNew");
    public string SupEdit => GetString("SupEdit");
    public string SupBalances => GetString("SupBalances");
    public string SupOrders => GetString("SupOrders");

    // Purchase Orders (Admin)
    public string NavPurchaseOrders => GetString("NavPurchaseOrders");
    public string PoTitle => GetString("PoTitle");
    public string PoSubtitle => GetString("PoSubtitle");
    public string PoHistory => GetString("PoHistory");
    public string PoNewOrder => GetString("PoNewOrder");
    public string PoSupplier => GetString("PoSupplier");
    public string PoDate => GetString("PoDate");
    public string PoTotal => GetString("PoTotal");
    public string PoBy => GetString("PoBy");
    public string PoAddItem => GetString("PoAddItem");
    public string PoNotes => GetString("PoNotes");
    public string PoSubmit => GetString("PoSubmit");
    public string PoEdit => GetString("PoEdit");

    // PO Payment
    public string PoDiscount => GetString("PoDiscount");
    public string PoPaid => GetString("PoPaid");
    public string PoRemaining => GetString("PoRemaining");
    public string PoStatus => GetString("PoStatus");
    public string PoPay => GetString("PoPay");
    public string PoPayAmount => GetString("PoPayAmount");

    // POS Barcode/Category
    public string PosScan => GetString("PosScan");
    public string PosProductNotFound => GetString("PosProductNotFound");
    public string PosAllCategories => GetString("PosAllCategories");
    public string PosReceipt => GetString("PosReceipt");
    public string PosTodaySales => GetString("PosTodaySales");
    public string PosInsufficientStock => GetString("PosInsufficientStock");

    // POS Extended Features
    public string PosPrintReceipt => GetString("PosPrintReceipt");
    public string PosSaveReceipt => GetString("PosSaveReceipt");

    // Print features (members list + ID card)
    public string PrintList => GetString("PrintList");
    public string CreateMissingCards => GetString("CreateMissingCards");
    public string MemberCard => GetString("MemberCard");
    public string PrintIncome => GetString("PrintIncome");
    public string PrintExpenses => GetString("PrintExpenses");
    public string DailyPass => GetString("DailyPass");
    public string PosDailySummary => GetString("PosDailySummary");
    public string PosTotalTransactions => GetString("PosTotalTransactions");
    public string PosTotalItemsSold => GetString("PosTotalItemsSold");
    public string PosCashSales => GetString("PosCashSales");
    public string PosCardSales => GetString("PosCardSales");
    public string PosTotalDiscounts => GetString("PosTotalDiscounts");
    public string PosPrintSummary => GetString("PosPrintSummary");
    public string PosOpenShift => GetString("PosOpenShift");
    public string PosCloseShift => GetString("PosCloseShift");
    public string PosOpeningCash => GetString("PosOpeningCash");
    public string PosClosingCash => GetString("PosClosingCash");
    public string PosVariance => GetString("PosVariance");
    public string PosShiftOpen => GetString("PosShiftOpen");
    public string PosShiftClosed => GetString("PosShiftClosed");
    public string PosNoOpenShift => GetString("PosNoOpenShift");
    public string PosDiscountLabel => GetString("PosDiscountLabel");
    public string PosDiscountPercent => GetString("PosDiscountPercent");
    public string PosDiscountAmount => GetString("PosDiscountAmount");
    public string PosDiscountReason => GetString("PosDiscountReason");
    public string PosOrderDiscount => GetString("PosOrderDiscount");
    public string PosSubtotal => GetString("PosSubtotal");
    public string PosSales => GetString("PosSales");
    public string PosSummary => GetString("PosSummary");
    public string PosShiftTab => GetString("PosShiftTab");
    public string PosEnterAmount => GetString("PosEnterAmount");
    public string PosShiftReport => GetString("PosShiftReport");
    public string PosExpectedCash => GetString("PosExpectedCash");
    public string PosByPaymentMethod => GetString("PosByPaymentMethod");

    // Stock Movements
    public string SmTitle => GetString("SmTitle");
    public string SmProduct => GetString("SmProduct");
    public string SmType => GetString("SmType");
    public string SmQty => GetString("SmQty");
    public string SmPrice => GetString("SmPrice");
    public string SmRef => GetString("SmRef");
    public string SmSupplier => GetString("SmSupplier");
    public string SmDate => GetString("SmDate");
    public string SmStockIn => GetString("SmStockIn");
    public string SmStockOut => GetString("SmStockOut");
    public string SmClear => GetString("SmClear");

    // Admin Dashboard
    public string NavDash => GetString("NavDash");
    public string DashTitle => GetString("DashTitle");
    public string DashSubtitle => GetString("DashSubtitle");
    public string DashTotalPlayers => GetString("DashTotalPlayers");
    public string DashExpiring => GetString("DashExpiring");
    public string DashRevenue => GetString("DashRevenue");
    public string DashExpenses => GetString("DashExpenses");
    public string DashProfit => GetString("DashProfit");
    public string DashUnpaid => GetString("DashUnpaid");
    public string DashSupplierDebt => GetString("DashSupplierDebt");
    public string DashLowStock => GetString("DashLowStock");
    public string DashRecentActivity => GetString("DashRecentActivity");

    // Audit Log
    public string NavAuditLog => GetString("NavAuditLog");
    public string AuditTitle => GetString("AuditTitle");
    public string AuditSubtitle => GetString("AuditSubtitle");
    public string AuditAction => GetString("AuditAction");
    public string AuditEntity => GetString("AuditEntity");
    public string AuditDetails => GetString("AuditDetails");
    public string AuditUser => GetString("AuditUser");
    public string AuditTime => GetString("AuditTime");

    // Alerts
    public string NavAlerts => GetString("NavAlerts");
    public string AlertTitle => GetString("AlertTitle");
    public string AlertSubtitle => GetString("AlertSubtitle");
    public string AlertExpiring => GetString("AlertExpiring");
    public string AlertExpired => GetString("AlertExpired");
    public string AlertFrozen => GetString("AlertFrozen");
    public string AlertFreezeDate => GetString("AlertFreezeDate");

    // Backup
    public string NavBackup => GetString("NavBackup");
    public string BkpTitle => GetString("BkpTitle");
    public string BkpSubtitle => GetString("BkpSubtitle");
    public string BkpBackup => GetString("BkpBackup");
    public string BkpBackupDesc => GetString("BkpBackupDesc");
    public string BkpCreateBackup => GetString("BkpCreateBackup");
    public string BkpRestore => GetString("BkpRestore");
    public string BkpRestoreDesc => GetString("BkpRestoreDesc");
    public string BkpRestoreBackup => GetString("BkpRestoreBackup");

    // Reports (Admin)
    public string NavReports => GetString("NavReports");
    public string RptTitle => GetString("RptTitle");
    public string RptSubtitle => GetString("RptSubtitle");
    public string RptExport => GetString("RptExport");
    public string RptPrint => GetString("RptPrint");
    public string RptPlayers => GetString("RptPlayers");
    public string RptFinance => GetString("RptFinance");
    public string RptInventory => GetString("RptInventory");
    public string RptSales => GetString("RptSales");
    public string RptPurchases => GetString("RptPurchases");
    public string RptMovements => GetString("RptMovements");
    public string RptSupplier => GetString("RptSupplier");
    public string RptPurchasedInRange => GetString("RptPurchasedInRange");
    public string RptOutstanding => GetString("RptOutstanding");
    public string RptRun => GetString("RptRun");

    // Monitor Display (Projector)
    public string DispSuccessPass => GetString("DispSuccessPass");
    public string DispExpiredCard => GetString("DispExpiredCard");
    public string DispFrozenCard => GetString("DispFrozenCard");
    public string DispNotRegistered => GetString("DispNotRegistered");
    public string DispQrPass => GetString("DispQrPass");
    public string DispQrPassExpired => GetString("DispQrPassExpired");
    public string DispQrPassUsedUp => GetString("DispQrPassUsedUp");
    public string DispQrGuest => GetString("DispQrGuest");
    public string DispActive => GetString("DispActive");
    public string DispEntry => GetString("DispEntry");
    public string DispExit => GetString("DispExit");
    public string DispCardOpen => GetString("DispCardOpen");
    public string DispPasswordOpen => GetString("DispPasswordOpen");
    public string DispCardPassword => GetString("DispCardPassword");
    public string DispCardRepeat => GetString("DispCardRepeat");
    public string DispInvalidCard => GetString("DispInvalidCard");
    public string DispButtonOpen => GetString("DispButtonOpen");
    public string DispCardOpenAlt => GetString("DispCardOpenAlt");
    public string DispCardRejected => GetString("DispCardRejected");
    public string DispCardNotFound => GetString("DispCardNotFound");
    public string DispCardExpiredHW => GetString("DispCardExpiredHW");
    public string DispRemoteOpen => GetString("DispRemoteOpen");
    public string DispRemoteClose => GetString("DispRemoteClose");
    public string DispDoorOpened => GetString("DispDoorOpened");
    public string DispDoorClosed => GetString("DispDoorClosed");
    public string DispOpenDisplay => GetString("DispOpenDisplay");
    public string DispCloseDisplay => GetString("DispCloseDisplay");

    // Projector Door Selection
    public string DispSelectDoor => GetString("DispSelectDoor");
    public string DispSelectDoorDesc => GetString("DispSelectDoorDesc");
    public string Confirm => GetString("Confirm");
    public string DispDoorName => GetString("DispDoorName");
    public string DispNoDoors => GetString("DispNoDoors");

    // WiFi & Device Validation
    public string WifiWarning => GetString("WifiWarning");
    public string WifiWarningSearch => GetString("WifiWarningSearch");
    public string DeviceNotExist => GetString("DeviceNotExist");
    public string DeviceUnreachable => GetString("DeviceUnreachable");
    public string CheckingConnection => GetString("CheckingConnection");
    public string PlayerNotExist => GetString("PlayerNotExist");
    public string DevicesOfflineList => GetString("DevicesOfflineList");
    public string DevicesOfflineRenew => GetString("DevicesOfflineRenew");
    public string VerifyingDevices => GetString("VerifyingDevices");
    public string DownloadedRecords => GetString("DownloadedRecords");
    public string NoRecordsDevice => GetString("NoRecordsDevice");
    public string DownloadingLogs => GetString("DownloadingLogs");

    // Player Operations (bilingual)
    public string SyncingCardToDevice => GetString("SyncingCardToDevice");
    public string CardAssignedSynced => GetString("CardAssignedSynced");
    public string CardAssignedSyncErrors => GetString("CardAssignedSyncErrors");
    public string CardRemovedSuccess => GetString("CardRemovedSuccess");
    public string CardRemoveConstraint => GetString("CardRemoveConstraint");
    public string CardNotFoundRemoved => GetString("CardNotFoundRemoved");
    public string PlayerNotFoundDeleted => GetString("PlayerNotFoundDeleted");
    public string RenewalCancelled => GetString("RenewalCancelled");
    public string RenewedSuccessfully => GetString("RenewedSuccessfully");
    public string RenewSyncErrors => GetString("RenewSyncErrors");
    public string SyncingCardDevices => GetString("SyncingCardDevices");
    public string RenewingSubscription => GetString("RenewingSubscription");
    public string PlayerDbInconsistent => GetString("PlayerDbInconsistent");
    public string DeviceTimeout => GetString("DeviceTimeout");
    public string ConcurrencyError => GetString("ConcurrencyError");
    public string DatabaseError => GetString("DatabaseError");
    public string SomeDevicesOffline => GetString("SomeDevicesOffline");
    public string UnfreezingPlayer => GetString("UnfreezingPlayer");
    public string FreezingAccount => GetString("FreezingAccount");
    public string RemovingCard => GetString("RemovingCard");

    // General
    public string BtnClose => GetString("BtnClose");

    // Door Operations (bilingual)
    public string DoorOpenFailed => GetString("DoorOpenFailed");
    public string DoorCloseFailed => GetString("DoorCloseFailed");
    public string DoorDelayFailed => GetString("DoorDelayFailed");
    public string DoorPasswordFailed => GetString("DoorPasswordFailed");
    public string DoorDelayInvalid => GetString("DoorDelayInvalid");
    public string OpeningDoor => GetString("OpeningDoor");
    public string ClosingDoor => GetString("ClosingDoor");
    public string SettingDelay => GetString("SettingDelay");
    public string SettingPassword => GetString("SettingPassword");

    // Device Sync All Players
    public string DevSyncAllPlayers => GetString("DevSyncAllPlayers");
    public string DevSyncAllConfirm => GetString("DevSyncAllConfirm");
    public string DevSyncAllProgress => GetString("DevSyncAllProgress");
    public string DevSyncAllDone => GetString("DevSyncAllDone");
    public string DevSyncAllNoCards => GetString("DevSyncAllNoCards");
    public string DevSynced => GetString("DevSynced");
    public string DevFailed => GetString("DevFailed");

    // Data Migration
    public string MigDataMigration => GetString("MigDataMigration");
    public string MigConnectionString => GetString("MigConnectionString");
    public string MigTableName => GetString("MigTableName");
    public string MigPreview => GetString("MigPreview");
    public string MigImport => GetString("MigImport");
    public string MigConnecting => GetString("MigConnecting");
    public string MigTotalRows => GetString("MigTotalRows");
    public string MigNewPlayers => GetString("MigNewPlayers");
    public string MigDuplicates => GetString("MigDuplicates");
    public string MigSampleNames => GetString("MigSampleNames");
    public string MigConfirmImport => GetString("MigConfirmImport");
    public string MigImportComplete => GetString("MigImportComplete");
    public string MigImported => GetString("MigImported");
    public string MigSkipped => GetString("MigSkipped");
    public string MigFailedCount => GetString("MigFailedCount");
    public string MigErrors => GetString("MigErrors");
    public string MigError => GetString("MigError");
    public string MigFillFields => GetString("MigFillFields");
    public string MigTestConnection => GetString("MigTestConnection");
    public string MigTestingConnection => GetString("MigTestingConnection");
    public string MigConnectionSuccess => GetString("MigConnectionSuccess");
    public string MigConnectionFailed => GetString("MigConnectionFailed");

    // Receipt
    public string RcpRenewalReceipt => GetString("RcpRenewalReceipt");
    public string RcpPlayerName => GetString("RcpPlayerName");
    public string RcpSubscriptionType => GetString("RcpSubscriptionType");
    public string RcpPeriod => GetString("RcpPeriod");
    public string RcpPrint => GetString("RcpPrint");
    public string RcpMonth => GetString("RcpMonth");
    public string RcpMonths => GetString("RcpMonths");
    public string RcpDays => GetString("RcpDays");

    // Bulk Operations
    public string BulkOperations => GetString("BulkOperations");
    public string BulkSelectOperation => GetString("BulkSelectOperation");
    public string BulkFreeze => GetString("BulkFreeze");
    public string BulkUnfreeze => GetString("BulkUnfreeze");
    public string BulkExtend => GetString("BulkExtend");
    public string BulkExtendDays => GetString("BulkExtendDays");
    public string BulkExtendDaysRequired => GetString("BulkExtendDaysRequired");
    public string BulkTarget => GetString("BulkTarget");
    public string BulkAllActive => GetString("BulkAllActive");
    public string BulkExpiring7 => GetString("BulkExpiring7");
    public string BulkExpired => GetString("BulkExpired");
    public string BulkFrozenPlayers => GetString("BulkFrozenPlayers");
    public string BulkExecute => GetString("BulkExecute");
    public string BulkConfirm => GetString("BulkConfirm");
    public string BulkNoPlayers => GetString("BulkNoPlayers");
    public string BulkComplete => GetString("BulkComplete");
    public string BulkSuccessCount => GetString("BulkSuccessCount");
    public string BulkFailedCount => GetString("BulkFailedCount");
    public string BulkUploadAll => GetString("BulkUploadAll");
    public string BulkSelectDevices => GetString("BulkSelectDevices");
    public string BulkSelectAtLeastOneDevice => GetString("BulkSelectAtLeastOneDevice");
    public string BulkUploadProgress => GetString("BulkUploadProgress");
    public string BulkUploadComplete => GetString("BulkUploadComplete");
    public string BulkUploaded => GetString("BulkUploaded");
    public string BulkSkipped => GetString("BulkSkipped");

    // Player Profile
    public string PrfPlayerProfile => GetString("PrfPlayerProfile");
    public string PrfOverview => GetString("PrfOverview");
    public string PrfFreezeHistory => GetString("PrfFreezeHistory");
    public string PrfTransactions => GetString("PrfTransactions");
    public string PrfActivityLog => GetString("PrfActivityLog");
    public string PrfAction => GetString("PrfAction");
    public string PrfPerformedBy => GetString("PrfPerformedBy");
    public string PrfDetails => GetString("PrfDetails");
    public string PrfFreezeStart => GetString("PrfFreezeStart");
    public string PrfFreezeEnd => GetString("PrfFreezeEnd");
    public string PrfNoRecords => GetString("PrfNoRecords");
    public string FilterActive => GetString("FilterActive");

    // QR Pass
    public string QrNavTitle => GetString("QrNavTitle");
    public string QrCreatePass => GetString("QrCreatePass");
    public string QrPlayerName => GetString("QrPlayerName");
    public string QrValidDays => GetString("QrValidDays");
    public string QrMaxUses => GetString("QrMaxUses");
    public string QrValidTo => GetString("QrValidTo");
    public string QrActiveOnly => GetString("QrActiveOnly");
    public string QrActiveToday => GetString("QrActiveToday");
    public string QrScanMode => GetString("QrScanMode");
    public string QrScanReady => GetString("QrScanReady");
    public string QrScanInstruction => GetString("QrScanInstruction");
    public string QrPassCreated => GetString("QrPassCreated");
    public string QrPassDeactivated => GetString("QrPassDeactivated");
    public string QrConfirmDeactivate => GetString("QrConfirmDeactivate");
    public string QrDeactivate => GetString("QrDeactivate");
    public string QrCodeTitle => GetString("QrCodeTitle");
    public string QrPrint => GetString("QrPrint");

    // Door Schedule
    public string WorkingHours => GetString("WorkingHours");
    public string WorkingDays => GetString("WorkingDays");
    public string Is24Hours => GetString("Is24Hours");
    public string StartTime => GetString("StartTime");
    public string EndTime => GetString("EndTime");
    public string SetSchedule => GetString("SetSchedule");
    public string Monday => GetString("Monday");
    public string Tuesday => GetString("Tuesday");
    public string Wednesday => GetString("Wednesday");
    public string Thursday => GetString("Thursday");
    public string Friday => GetString("Friday");
    public string Saturday => GetString("Saturday");
    public string Sunday => GetString("Sunday");

    // Validation
    public string AtLeastOneNameRequired => GetString("AtLeastOneNameRequired");

    // Time Groups
    public string NavTimeGroups => GetString("NavTimeGroups");
    public string TgTitle => GetString("TgTitle");
    public string TgSubtitle => GetString("TgSubtitle");
    public string TgAddNew => GetString("TgAddNew");
    public string TgEdit => GetString("TgEdit");
    public string TgNameEn => GetString("TgNameEn");
    public string TgNameAr => GetString("TgNameAr");
    public string TgHwIndex => GetString("TgHwIndex");
    public string TgSchedule => GetString("TgSchedule");
    public string TgDefault => GetString("TgDefault");
    public string TgFullAccess => GetString("TgFullAccess");
    public string TgClosed => GetString("TgClosed");
    public string TgActive => GetString("TgActive");
    public string TgAddSegment => GetString("TgAddSegment");
    public string TgDeleteConfirm => GetString("TgDeleteConfirm");
    public string TgCannotDeleteDefault => GetString("TgCannotDeleteDefault");
    public string TgSlotsFull => GetString("TgSlotsFull");
    public string TgSyncToDevice => GetString("TgSyncToDevice");
    public string TgSegments => GetString("TgSegments");
    public string TgSaved => GetString("TgSaved");
    public string TgDeleted => GetString("TgDeleted");
    public string TgNameRequired => GetString("TgNameRequired");
    public string TgScheduleLabel => GetString("TgScheduleLabel");

    // Device Notification Bar
    public string BtnSync => GetString("BtnSync");

    // Filter Tooltips
    public string FilterTipAll => GetString("FilterTipAll");
    public string FilterTipToday => GetString("FilterTipToday");
    public string FilterTipYesterday => GetString("FilterTipYesterday");
    public string FilterTipThisWeek => GetString("FilterTipThisWeek");
    public string FilterTipLastWeek => GetString("FilterTipLastWeek");
    public string FilterTipThisMonth => GetString("FilterTipThisMonth");
    public string FilterTipLastMonth => GetString("FilterTipLastMonth");
    public string FilterTipLast3Months => GetString("FilterTipLast3Months");
    public string FilterTipLast6Months => GetString("FilterTipLast6Months");
    public string FilterTipThisYear => GetString("FilterTipThisYear");

    // Sync Status
    public string SyncStatus => GetString("SyncStatus");
    // SyncToDevice already defined above (line 160)
    public string PlayerSynced => GetString("PlayerSynced");
    public string PlayerSyncFailed => GetString("PlayerSyncFailed");
    public string PlayerNoCard => GetString("PlayerNoCard");

    private string GetString(string name)
    {
        return _resourceManager.GetString(name, _currentCulture) ?? name;
    }

    public void SwitchLanguage()
    {
        if (IsArabic)
            SetLanguage("en");
        else
            SetLanguage("ar");
    }

    private static readonly string LangFilePath = Path.Combine(AppContext.BaseDirectory, ".language");

    /// <summary>
    /// Loads the saved language preference from disk. Call on app startup before login.
    /// </summary>
    public void LoadSavedLanguage()
    {
        try
        {
            if (File.Exists(LangFilePath))
            {
                var saved = File.ReadAllText(LangFilePath).Trim();
                if (saved == "ar" || saved == "en")
                    SetLanguage(saved);
            }
        }
        catch { }
    }

    public void SetLanguage(string cultureCode)
    {
        _currentCulture = new CultureInfo(cultureCode);
        Thread.CurrentThread.CurrentUICulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;

        IsArabic = cultureCode.StartsWith("ar");
        FlowDirection = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // Persist language choice to disk
        try { File.WriteAllText(LangFilePath, cultureCode.StartsWith("ar") ? "ar" : "en"); }
        catch { }

        // Notify all string properties changed
        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(NavDashboard));
        OnPropertyChanged(nameof(NavDevices));
        OnPropertyChanged(nameof(NavDoors));
        OnPropertyChanged(nameof(NavEmployees));
        OnPropertyChanged(nameof(NavCards));
        OnPropertyChanged(nameof(NavEvents));
        OnPropertyChanged(nameof(NavSettings));
        OnPropertyChanged(nameof(NavFinance));
        OnPropertyChanged(nameof(NavCashFlow));
        OnPropertyChanged(nameof(NavPOS));
        OnPropertyChanged(nameof(NavUsers));
        OnPropertyChanged(nameof(TotalDevices));
        OnPropertyChanged(nameof(OnlineDoors));
        OnPropertyChanged(nameof(TodayEvents));
        OnPropertyChanged(nameof(ActiveAlarms));
        OnPropertyChanged(nameof(DoorStatus));
        OnPropertyChanged(nameof(RecentEvents));
        OnPropertyChanged(nameof(Refresh));
        OnPropertyChanged(nameof(StatusOpen));
        OnPropertyChanged(nameof(StatusClosed));
        OnPropertyChanged(nameof(StatusAlarm));
        OnPropertyChanged(nameof(StatusFault));
        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(Door));
        OnPropertyChanged(nameof(Card));
        OnPropertyChanged(nameof(Event));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(SearchNetwork));
        OnPropertyChanged(nameof(AddDevice));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(Online));
        OnPropertyChanged(nameof(Offline));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(IPAddress));
        OnPropertyChanged(nameof(MACAddress));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(Doors));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Actions));
        OnPropertyChanged(nameof(Edit));
        OnPropertyChanged(nameof(Delete));
        OnPropertyChanged(nameof(NoDevices));
        OnPropertyChanged(nameof(ConfirmDelete));
        OnPropertyChanged(nameof(Connect));
        OnPropertyChanged(nameof(DeviceInfo));
        OnPropertyChanged(nameof(OpenDoor));
        OnPropertyChanged(nameof(SyncTime));
        OnPropertyChanged(nameof(ConnectSuccess));
        OnPropertyChanged(nameof(OpenDoorSuccess));
        OnPropertyChanged(nameof(SyncTimeSuccess));
        OnPropertyChanged(nameof(SelectDoor));
        OnPropertyChanged(nameof(CloseDoor));
        OnPropertyChanged(nameof(SetDelay));
        OnPropertyChanged(nameof(SetPassword));
        OnPropertyChanged(nameof(CloseDoorSuccess));
        OnPropertyChanged(nameof(SetDelaySuccess));
        OnPropertyChanged(nameof(SetPasswordSuccess));
        OnPropertyChanged(nameof(EnterDelay));
        OnPropertyChanged(nameof(EnterPassword));
        OnPropertyChanged(nameof(DoorNumber));
        OnPropertyChanged(nameof(EnterName));
        OnPropertyChanged(nameof(RenameSuccess));
        OnPropertyChanged(nameof(ChangeIP));
        OnPropertyChanged(nameof(EnterIP));
        OnPropertyChanged(nameof(ChangeIPSuccess));
        OnPropertyChanged(nameof(NavPlayers));
        OnPropertyChanged(nameof(AddPlayer));
        OnPropertyChanged(nameof(AddPlayerSuccess));
        OnPropertyChanged(nameof(EditPlayerSuccess));
        OnPropertyChanged(nameof(ConfirmDeletePlayer));
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(CardNo));
        OnPropertyChanged(nameof(Subscription));
        OnPropertyChanged(nameof(Fee));
        OnPropertyChanged(nameof(Paid));
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(WithCard));
        OnPropertyChanged(nameof(WithoutCard));
        OnPropertyChanged(nameof(AssignCard));
        OnPropertyChanged(nameof(AssignCardSuccess));
        OnPropertyChanged(nameof(RemoveCard));
        OnPropertyChanged(nameof(RemoveCardSuccess));
        OnPropertyChanged(nameof(ConfirmRemoveCard));
        OnPropertyChanged(nameof(SelectCardToRemove));
        OnPropertyChanged(nameof(ViewCards));
        OnPropertyChanged(nameof(NoCardsAssigned));
        OnPropertyChanged(nameof(ShowAll));
        OnPropertyChanged(nameof(ClickShowAll));
        OnPropertyChanged(nameof(Loading));
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(Phone));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(Photo));
        OnPropertyChanged(nameof(SyncToDevice));
        OnPropertyChanged(nameof(SyncSuccess));
        OnPropertyChanged(nameof(EditPlayer));
        OnPropertyChanged(nameof(NameEn));
        OnPropertyChanged(nameof(NameAr));
        OnPropertyChanged(nameof(Period));
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(HeightCm));
        OnPropertyChanged(nameof(WeightKg));
        OnPropertyChanged(nameof(BrowsePhoto));
        OnPropertyChanged(nameof(RemovePhoto));
        OnPropertyChanged(nameof(Cancel));
        OnPropertyChanged(nameof(OK));
        OnPropertyChanged(nameof(IQD));
        OnPropertyChanged(nameof(Month1));
        OnPropertyChanged(nameof(Months3));
        OnPropertyChanged(nameof(Months6));
        OnPropertyChanged(nameof(Months12));
        OnPropertyChanged(nameof(CustomPeriod));
        OnPropertyChanged(nameof(PlayerNameRequired));
        OnPropertyChanged(nameof(CardNoRequired));
        OnPropertyChanged(nameof(ValidationTitle));
        OnPropertyChanged(nameof(SelectPhoto));
        OnPropertyChanged(nameof(Submit));
        OnPropertyChanged(nameof(NameArRequired));
        OnPropertyChanged(nameof(PhoneRequired));
        OnPropertyChanged(nameof(SubscriptionRequired));
        OnPropertyChanged(nameof(PeriodRequired));
        OnPropertyChanged(nameof(FeeRequired));
        OnPropertyChanged(nameof(PhotoRequired));
        OnPropertyChanged(nameof(HeightInvalid));
        OnPropertyChanged(nameof(WeightInvalid));
        OnPropertyChanged(nameof(OptionalInfo));
        OnPropertyChanged(nameof(AuthorizeCard));
        OnPropertyChanged(nameof(CardNumber));
        OnPropertyChanged(nameof(CardPassword));
        OnPropertyChanged(nameof(CardMode));
        OnPropertyChanged(nameof(CardType));
        OnPropertyChanged(nameof(DoorAccess));
        OnPropertyChanged(nameof(EffectiveTimes));
        OnPropertyChanged(nameof(MaxVisits));
        OnPropertyChanged(nameof(TimePeriod));
        OnPropertyChanged(nameof(HolidayAccess));
        OnPropertyChanged(nameof(ValidUntil));
        OnPropertyChanged(nameof(SelectDevices));
        OnPropertyChanged(nameof(Ordinary));
        OnPropertyChanged(nameof(FirstCardPrivilege));
        OnPropertyChanged(nameof(AlwaysOpenPrivilege));
        OnPropertyChanged(nameof(PatrolCheckIn));
        OnPropertyChanged(nameof(AntiTheftSetting));
        OnPropertyChanged(nameof(Standard));
        OnPropertyChanged(nameof(VIP));
        OnPropertyChanged(nameof(Temporary));
        OnPropertyChanged(nameof(Unlimited));
        OnPropertyChanged(nameof(InvalidateImmediately));
        OnPropertyChanged(nameof(CardNumberRequired));
        OnPropertyChanged(nameof(ValidFromBeforeValidTo));
        OnPropertyChanged(nameof(SelectAtLeastOneDevice));
        OnPropertyChanged(nameof(SelectAtLeastOneDoor));
        OnPropertyChanged(nameof(NoDevicesAvailable));
        OnPropertyChanged(nameof(DuplicateCardNo));
        OnPropertyChanged(nameof(DuplicatePhone));
        OnPropertyChanged(nameof(DuplicateNameWarning));
        OnPropertyChanged(nameof(Yes));
        OnPropertyChanged(nameof(No));
        OnPropertyChanged(nameof(DeleteReason));
        OnPropertyChanged(nameof(EnterDeleteReason));
        OnPropertyChanged(nameof(DeleteReasonRequired));
        OnPropertyChanged(nameof(DeletePlayerSuccess));
        OnPropertyChanged(nameof(FreezePlayer));
        OnPropertyChanged(nameof(UnfreezePlayer));
        OnPropertyChanged(nameof(EnterFreezeReason));
        OnPropertyChanged(nameof(ConfirmUnfreeze));
        OnPropertyChanged(nameof(FreezeSuccess));
        OnPropertyChanged(nameof(UnfreezeSuccess));
        OnPropertyChanged(nameof(RenewSubscription));
        OnPropertyChanged(nameof(RenewSuccess));
        OnPropertyChanged(nameof(MigratedRenewBlockedTitle));
        OnPropertyChanged(nameof(MigratedRenewBlockedMessage));
        OnPropertyChanged(nameof(MigratedRenewStillBlocked));
        OnPropertyChanged(nameof(EditReason));
        OnPropertyChanged(nameof(EnterEditReason));
        OnPropertyChanged(nameof(EditReasonRequired));
        OnPropertyChanged(nameof(NavLogs));
        OnPropertyChanged(nameof(LogTimestamp));
        OnPropertyChanged(nameof(LogAction));
        OnPropertyChanged(nameof(LogEntity));
        OnPropertyChanged(nameof(LogDetails));
        OnPropertyChanged(nameof(ClickShowAllLogs));
        OnPropertyChanged(nameof(LogYesterday));
        OnPropertyChanged(nameof(LogLast3Months));
        OnPropertyChanged(nameof(NavDeletedRecords));
        OnPropertyChanged(nameof(DeletedDate));
        OnPropertyChanged(nameof(ClickShowAllDeleted));
        OnPropertyChanged(nameof(DeletedByPrefix));
        OnPropertyChanged(nameof(DeletedByCol));
        OnPropertyChanged(nameof(ActionCreate));
        OnPropertyChanged(nameof(ActionUpdate));
        OnPropertyChanged(nameof(ActionDelete));
        OnPropertyChanged(nameof(ActionSoftDelete));
        OnPropertyChanged(nameof(ActionFreeze));
        OnPropertyChanged(nameof(ActionUnfreeze));
        OnPropertyChanged(nameof(ActionRenew));
        OnPropertyChanged(nameof(ActionSyncCard));
        OnPropertyChanged(nameof(EntityPlayer));
        OnPropertyChanged(nameof(EntityAccessCard));
        OnPropertyChanged(nameof(EntityDevice));
        OnPropertyChanged(nameof(Device));
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(EventType));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(ClickShowAllEvents));
        OnPropertyChanged(nameof(NavMonitor));
        OnPropertyChanged(nameof(MonitorTitle));
        OnPropertyChanged(nameof(StartMonitor));
        OnPropertyChanged(nameof(StopMonitor));
        OnPropertyChanged(nameof(MonitorReady));
        OnPropertyChanged(nameof(DownloadLogs));
        OnPropertyChanged(nameof(AllDevices));
        OnPropertyChanged(nameof(SelectDevice));
        OnPropertyChanged(nameof(EventCardAction));
        OnPropertyChanged(nameof(EventButtonAction));
        OnPropertyChanged(nameof(EventSoftwareAction));
        OnPropertyChanged(nameof(EventDoorSensor));
        OnPropertyChanged(nameof(EventAlarm));
        OnPropertyChanged(nameof(EventSystem));
        OnPropertyChanged(nameof(EntryLabel));
        OnPropertyChanged(nameof(ExitLabel));
        OnPropertyChanged(nameof(SelectPeriod));
        OnPropertyChanged(nameof(SelectPeriodDesc));
        OnPropertyChanged(nameof(Last1Month));
        OnPropertyChanged(nameof(Last3Months));
        OnPropertyChanged(nameof(Last6Months));
        OnPropertyChanged(nameof(Last1Year));
        OnPropertyChanged(nameof(Download));
        OnPropertyChanged(nameof(CardActive));
        OnPropertyChanged(nameof(CardExpired));
        OnPropertyChanged(nameof(CardFrozen));
        OnPropertyChanged(nameof(CardUnregistered));
        OnPropertyChanged(nameof(CustomDays));
        OnPropertyChanged(nameof(EnterDays));
        OnPropertyChanged(nameof(DaysRequired));
        OnPropertyChanged(nameof(Login));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(LoginButton));
        OnPropertyChanged(nameof(LoginFailed));
        OnPropertyChanged(nameof(LoggingIn));
        OnPropertyChanged(nameof(LogUser));
        OnPropertyChanged(nameof(WelcomeBack));
        OnPropertyChanged(nameof(PoweredBy));
        OnPropertyChanged(nameof(UsernameRequired));
        OnPropertyChanged(nameof(PasswordRequired));
        OnPropertyChanged(nameof(UserNotFound));
        OnPropertyChanged(nameof(WrongPassword));
        OnPropertyChanged(nameof(AccountDisabled));
        OnPropertyChanged(nameof(Password));
        // Finance
        OnPropertyChanged(nameof(FinTotalRevenue));
        OnPropertyChanged(nameof(FinTotalExpenses));
        OnPropertyChanged(nameof(FinNetProfit));
        OnPropertyChanged(nameof(FinUnpaidBalances));
        OnPropertyChanged(nameof(FinOutstandingPlayers));
        OnPropertyChanged(nameof(FinRecentTransactions));
        OnPropertyChanged(nameof(FinPayNow));
        OnPropertyChanged(nameof(FinType));
        OnPropertyChanged(nameof(FinCategory));
        OnPropertyChanged(nameof(FinAmount));
        OnPropertyChanged(nameof(FinDescription));
        OnPropertyChanged(nameof(FinPayment));
        OnPropertyChanged(nameof(FinAddExpense));
        OnPropertyChanged(nameof(FinAddIncome));
        OnPropertyChanged(nameof(FinAll));
        OnPropertyChanged(nameof(FinIncome));
        OnPropertyChanged(nameof(FinExpense));
        OnPropertyChanged(nameof(FinPeriod));
        OnPropertyChanged(nameof(FinToday));
        OnPropertyChanged(nameof(FinYesterday));
        OnPropertyChanged(nameof(FinThisWeek));
        OnPropertyChanged(nameof(FinLastWeek));
        OnPropertyChanged(nameof(FinThisMonth));
        OnPropertyChanged(nameof(FinLastMonth));
        OnPropertyChanged(nameof(FinLast3Months));
        OnPropertyChanged(nameof(FinLast6Months));
        OnPropertyChanged(nameof(FinThisYear));
        OnPropertyChanged(nameof(FinSearch));
        // Income Categories
        OnPropertyChanged(nameof(FinIncCatSubscription));
        OnPropertyChanged(nameof(FinIncCatPOSSales));
        OnPropertyChanged(nameof(FinIncCatOwnerDeposit));
        OnPropertyChanged(nameof(FinIncCatOther));
        // Expense Categories
        OnPropertyChanged(nameof(FinExpCatRent));
        OnPropertyChanged(nameof(FinExpCatElectricity));
        OnPropertyChanged(nameof(FinExpCatWater));
        OnPropertyChanged(nameof(FinExpCatSalaries));
        OnPropertyChanged(nameof(FinExpCatEquipment));
        OnPropertyChanged(nameof(FinExpCatMaintenance));
        OnPropertyChanged(nameof(FinExpCatSupplies));
        OnPropertyChanged(nameof(FinExpCatMarketing));
        // POS
        OnPropertyChanged(nameof(PosAddProduct));
        OnPropertyChanged(nameof(PosCart));
        OnPropertyChanged(nameof(PosPayCash));
        OnPropertyChanged(nameof(PosPayCard));
        OnPropertyChanged(nameof(PosPayCredit));
        OnPropertyChanged(nameof(PosCollectDebt));
        OnPropertyChanged(nameof(PosTopUp));
        OnPropertyChanged(nameof(PosCardBalance));
        OnPropertyChanged(nameof(PosPrice));
        OnPropertyChanged(nameof(PosStock));
        OnPropertyChanged(nameof(PosPlayerNotFound));
        OnPropertyChanged(nameof(PosSearchPlayerFirst));
        OnPropertyChanged(nameof(PosSaleSuccess));
        OnPropertyChanged(nameof(PosInsufficientBalance));
        OnPropertyChanged(nameof(PosSuccess));
        // Filters & Reports
        OnPropertyChanged(nameof(FilterAll));
        OnPropertyChanged(nameof(RptReports));
        OnPropertyChanged(nameof(RptExpiring));
        OnPropertyChanged(nameof(RptRenewed));
        OnPropertyChanged(nameof(RptFrozen));
        OnPropertyChanged(nameof(RptExpired));
        OnPropertyChanged(nameof(RptToday));
        OnPropertyChanged(nameof(RptThisWeek));
        OnPropertyChanged(nameof(RptLastWeek));
        OnPropertyChanged(nameof(RptThisMonth));
        OnPropertyChanged(nameof(RptLastMonth));
        OnPropertyChanged(nameof(RptResults));
        OnPropertyChanged(nameof(RptNoResults));
        OnPropertyChanged(nameof(RptEndDate));
        OnPropertyChanged(nameof(RptStartDate));
        OnPropertyChanged(nameof(RptSubscription));
        OnPropertyChanged(nameof(RptPhone));
        // Permissions
        OnPropertyChanged(nameof(PermPermissions));
        OnPropertyChanged(nameof(PermSelectAll));
        OnPropertyChanged(nameof(PermClearAll));
        OnPropertyChanged(nameof(PermAdminNote));
        OnPropertyChanged(nameof(PermAccessDenied));
        OnPropertyChanged(nameof(PermNoPermission));
        // Admin Users Page
        OnPropertyChanged(nameof(UsrAddUser));
        OnPropertyChanged(nameof(UsrAddNewUser));
        OnPropertyChanged(nameof(UsrEditUser));
        OnPropertyChanged(nameof(UsrUsername));
        OnPropertyChanged(nameof(UsrDisplayName));
        OnPropertyChanged(nameof(UsrRole));
        OnPropertyChanged(nameof(UsrPassword));
        OnPropertyChanged(nameof(UsrNewPassword));
        OnPropertyChanged(nameof(UsrAccountActive));
        OnPropertyChanged(nameof(UsrSave));
        OnPropertyChanged(nameof(UsrCancel));
        OnPropertyChanged(nameof(UsrStatus));
        OnPropertyChanged(nameof(UsrActive));
        OnPropertyChanged(nameof(UsrDisabled));
        OnPropertyChanged(nameof(UsrID));
        OnPropertyChanged(nameof(UsrSubtitle));
        OnPropertyChanged(nameof(UsrLoading));
        OnPropertyChanged(nameof(UsrUsernameRequired));
        OnPropertyChanged(nameof(UsrPasswordRequired));
        OnPropertyChanged(nameof(UsrDisplayNameRequired));
        OnPropertyChanged(nameof(UsrSearch));
        OnPropertyChanged(nameof(UsrTotalUsers));
        OnPropertyChanged(nameof(UsrActiveUsers));
        OnPropertyChanged(nameof(UsrInactiveUsers));
        OnPropertyChanged(nameof(UsrDeactivate));
        OnPropertyChanged(nameof(UsrActivate));
        OnPropertyChanged(nameof(UsrConfirmDeactivate));
        OnPropertyChanged(nameof(UsrConfirmActivate));
        OnPropertyChanged(nameof(UsrDeactivateReason));
        OnPropertyChanged(nameof(UsrDeleteReason));
        OnPropertyChanged(nameof(UsrActions));
        // Change Password
        OnPropertyChanged(nameof(CpwChangePassword));
        OnPropertyChanged(nameof(CpwCurrentPassword));
        OnPropertyChanged(nameof(CpwNewPassword));
        OnPropertyChanged(nameof(CpwConfirmPassword));
        OnPropertyChanged(nameof(CpwPasswordMismatch));
        OnPropertyChanged(nameof(CpwMinLength));
        OnPropertyChanged(nameof(CpwPasswordChanged));
        OnPropertyChanged(nameof(CpwDefaultPasswordWarning));
        // Diagnostics
        OnPropertyChanged(nameof(DiagSendDiagnostics));
        OnPropertyChanged(nameof(DiagConfirmTitle));
        OnPropertyChanged(nameof(DiagConfirmBody));
        OnPropertyChanged(nameof(DiagUploading));
        OnPropertyChanged(nameof(DiagUploadSuccess));
        OnPropertyChanged(nameof(DiagUploadFailed));
        OnPropertyChanged(nameof(DiagNoteLabel));
        OnPropertyChanged(nameof(DiagNoteHint));
        OnPropertyChanged(nameof(DiagSendNow));
        // Settings
        OnPropertyChanged(nameof(SetSaved));
        OnPropertyChanged(nameof(SetBrowseLogo));
        OnPropertyChanged(nameof(SetRemoveLogo));
        OnPropertyChanged(nameof(SetCompanyName));
        OnPropertyChanged(nameof(SetGymName));
        OnPropertyChanged(nameof(SetOwner));
        // Categories
        OnPropertyChanged(nameof(NavCategories));
        OnPropertyChanged(nameof(CatTitle));
        OnPropertyChanged(nameof(CatSubtitle));
        OnPropertyChanged(nameof(CatType));
        OnPropertyChanged(nameof(CatNameEn));
        OnPropertyChanged(nameof(CatNameAr));
        OnPropertyChanged(nameof(CatRate));
        OnPropertyChanged(nameof(CatAddNew));
        OnPropertyChanged(nameof(CatEdit));
        OnPropertyChanged(nameof(CatDelete));
        // Products
        OnPropertyChanged(nameof(NavProducts));
        OnPropertyChanged(nameof(PrdTitle));
        OnPropertyChanged(nameof(PrdSubtitle));
        OnPropertyChanged(nameof(PrdName));
        OnPropertyChanged(nameof(PrdNameAr));
        OnPropertyChanged(nameof(PrdBarcode));
        OnPropertyChanged(nameof(PrdPrice));
        OnPropertyChanged(nameof(PrdCostPrice));
        OnPropertyChanged(nameof(PrdProfit));
        OnPropertyChanged(nameof(PrdCategory));
        OnPropertyChanged(nameof(PrdStock));
        OnPropertyChanged(nameof(PrdAddNew));
        OnPropertyChanged(nameof(PrdEdit));
        // Suppliers
        OnPropertyChanged(nameof(NavSuppliers));
        OnPropertyChanged(nameof(SupTitle));
        OnPropertyChanged(nameof(SupSubtitle));
        OnPropertyChanged(nameof(SupName));
        OnPropertyChanged(nameof(SupPhone));
        OnPropertyChanged(nameof(SupAddress));
        OnPropertyChanged(nameof(SupContact));
        OnPropertyChanged(nameof(SupAddNew));
        OnPropertyChanged(nameof(SupEdit));
        OnPropertyChanged(nameof(SupBalances));
        OnPropertyChanged(nameof(SupOrders));
        // Purchase Orders
        OnPropertyChanged(nameof(NavPurchaseOrders));
        OnPropertyChanged(nameof(PoTitle));
        OnPropertyChanged(nameof(PoSubtitle));
        OnPropertyChanged(nameof(PoHistory));
        OnPropertyChanged(nameof(PoNewOrder));
        OnPropertyChanged(nameof(PoSupplier));
        OnPropertyChanged(nameof(PoDate));
        OnPropertyChanged(nameof(PoTotal));
        OnPropertyChanged(nameof(PoBy));
        OnPropertyChanged(nameof(PoAddItem));
        OnPropertyChanged(nameof(PoNotes));
        OnPropertyChanged(nameof(PoSubmit));
        // PO Payment
        OnPropertyChanged(nameof(PoDiscount));
        OnPropertyChanged(nameof(PoPaid));
        OnPropertyChanged(nameof(PoRemaining));
        OnPropertyChanged(nameof(PoStatus));
        OnPropertyChanged(nameof(PoPay));
        OnPropertyChanged(nameof(PoPayAmount));
        // POS Barcode/Category
        OnPropertyChanged(nameof(PosScan));
        OnPropertyChanged(nameof(PosProductNotFound));
        OnPropertyChanged(nameof(PosAllCategories));
        OnPropertyChanged(nameof(PosReceipt));
        OnPropertyChanged(nameof(PosTodaySales));
        OnPropertyChanged(nameof(PosInsufficientStock));
        // POS Extended Features
        OnPropertyChanged(nameof(PosPrintReceipt));
        OnPropertyChanged(nameof(PosSaveReceipt));
        OnPropertyChanged(nameof(PrintList));
        OnPropertyChanged(nameof(MemberCard));
        OnPropertyChanged(nameof(PrintIncome));
        OnPropertyChanged(nameof(PrintExpenses));
        OnPropertyChanged(nameof(DailyPass));
        OnPropertyChanged(nameof(PosDailySummary));
        OnPropertyChanged(nameof(PosTotalTransactions));
        OnPropertyChanged(nameof(PosTotalItemsSold));
        OnPropertyChanged(nameof(PosCashSales));
        OnPropertyChanged(nameof(PosCardSales));
        OnPropertyChanged(nameof(PosTotalDiscounts));
        OnPropertyChanged(nameof(PosPrintSummary));
        OnPropertyChanged(nameof(PosOpenShift));
        OnPropertyChanged(nameof(PosCloseShift));
        OnPropertyChanged(nameof(PosOpeningCash));
        OnPropertyChanged(nameof(PosClosingCash));
        OnPropertyChanged(nameof(PosVariance));
        OnPropertyChanged(nameof(PosShiftOpen));
        OnPropertyChanged(nameof(PosShiftClosed));
        OnPropertyChanged(nameof(PosNoOpenShift));
        OnPropertyChanged(nameof(PosDiscountLabel));
        OnPropertyChanged(nameof(PosDiscountPercent));
        OnPropertyChanged(nameof(PosDiscountAmount));
        OnPropertyChanged(nameof(PosDiscountReason));
        OnPropertyChanged(nameof(PosOrderDiscount));
        OnPropertyChanged(nameof(PosSubtotal));
        OnPropertyChanged(nameof(PosSales));
        OnPropertyChanged(nameof(PosSummary));
        OnPropertyChanged(nameof(PosShiftTab));
        OnPropertyChanged(nameof(PosEnterAmount));
        OnPropertyChanged(nameof(PosShiftReport));
        OnPropertyChanged(nameof(PosExpectedCash));
        OnPropertyChanged(nameof(PosByPaymentMethod));
        // Stock Movements
        OnPropertyChanged(nameof(SmTitle));
        OnPropertyChanged(nameof(SmProduct));
        OnPropertyChanged(nameof(SmType));
        OnPropertyChanged(nameof(SmQty));
        OnPropertyChanged(nameof(SmPrice));
        OnPropertyChanged(nameof(SmRef));
        OnPropertyChanged(nameof(SmSupplier));
        OnPropertyChanged(nameof(SmDate));
        OnPropertyChanged(nameof(SmStockIn));
        OnPropertyChanged(nameof(SmStockOut));
        OnPropertyChanged(nameof(SmClear));
        // Admin Dashboard
        OnPropertyChanged(nameof(NavDash));
        OnPropertyChanged(nameof(DashTitle));
        OnPropertyChanged(nameof(DashSubtitle));
        OnPropertyChanged(nameof(DashTotalPlayers));
        OnPropertyChanged(nameof(DashExpiring));
        OnPropertyChanged(nameof(DashRevenue));
        OnPropertyChanged(nameof(DashExpenses));
        OnPropertyChanged(nameof(DashProfit));
        OnPropertyChanged(nameof(DashUnpaid));
        OnPropertyChanged(nameof(DashSupplierDebt));
        OnPropertyChanged(nameof(DashLowStock));
        OnPropertyChanged(nameof(DashRecentActivity));
        // Audit Log
        OnPropertyChanged(nameof(NavAuditLog));
        OnPropertyChanged(nameof(AuditTitle));
        OnPropertyChanged(nameof(AuditSubtitle));
        OnPropertyChanged(nameof(AuditAction));
        OnPropertyChanged(nameof(AuditEntity));
        OnPropertyChanged(nameof(AuditDetails));
        OnPropertyChanged(nameof(AuditUser));
        OnPropertyChanged(nameof(AuditTime));
        // Alerts
        OnPropertyChanged(nameof(NavAlerts));
        OnPropertyChanged(nameof(AlertTitle));
        OnPropertyChanged(nameof(AlertSubtitle));
        OnPropertyChanged(nameof(AlertExpiring));
        OnPropertyChanged(nameof(AlertExpired));
        OnPropertyChanged(nameof(AlertFrozen));
        OnPropertyChanged(nameof(AlertFreezeDate));
        // Backup
        OnPropertyChanged(nameof(NavBackup));
        OnPropertyChanged(nameof(BkpTitle));
        OnPropertyChanged(nameof(BkpSubtitle));
        OnPropertyChanged(nameof(BkpBackup));
        OnPropertyChanged(nameof(BkpBackupDesc));
        OnPropertyChanged(nameof(BkpCreateBackup));
        OnPropertyChanged(nameof(BkpRestore));
        OnPropertyChanged(nameof(BkpRestoreDesc));
        OnPropertyChanged(nameof(BkpRestoreBackup));
        // Reports (Admin)
        OnPropertyChanged(nameof(NavReports));
        OnPropertyChanged(nameof(RptTitle));
        OnPropertyChanged(nameof(RptSubtitle));
        OnPropertyChanged(nameof(RptExport));
        OnPropertyChanged(nameof(RptPrint));
        OnPropertyChanged(nameof(RptPlayers));
        OnPropertyChanged(nameof(RptFinance));
        OnPropertyChanged(nameof(RptInventory));
        OnPropertyChanged(nameof(RptSales));
        OnPropertyChanged(nameof(RptPurchases));
        OnPropertyChanged(nameof(RptMovements));
        OnPropertyChanged(nameof(RptSupplier));
        OnPropertyChanged(nameof(RptPurchasedInRange));
        OnPropertyChanged(nameof(RptOutstanding));
        OnPropertyChanged(nameof(RptRun));
        // Monitor Display (Projector)
        OnPropertyChanged(nameof(DispSuccessPass));
        OnPropertyChanged(nameof(DispExpiredCard));
        OnPropertyChanged(nameof(DispFrozenCard));
        OnPropertyChanged(nameof(DispNotRegistered));
        OnPropertyChanged(nameof(DispQrPass));
        OnPropertyChanged(nameof(DispQrPassExpired));
        OnPropertyChanged(nameof(DispQrPassUsedUp));
        OnPropertyChanged(nameof(DispQrGuest));
        OnPropertyChanged(nameof(DispActive));
        OnPropertyChanged(nameof(DispEntry));
        OnPropertyChanged(nameof(DispExit));
        OnPropertyChanged(nameof(DispCardOpen));
        OnPropertyChanged(nameof(DispPasswordOpen));
        OnPropertyChanged(nameof(DispCardPassword));
        OnPropertyChanged(nameof(DispCardRepeat));
        OnPropertyChanged(nameof(DispInvalidCard));
        OnPropertyChanged(nameof(DispButtonOpen));
        OnPropertyChanged(nameof(DispCardOpenAlt));
        OnPropertyChanged(nameof(DispCardRejected));
        OnPropertyChanged(nameof(DispCardNotFound));
        OnPropertyChanged(nameof(DispCardExpiredHW));
        OnPropertyChanged(nameof(DispRemoteOpen));
        OnPropertyChanged(nameof(DispRemoteClose));
        OnPropertyChanged(nameof(DispDoorOpened));
        OnPropertyChanged(nameof(DispDoorClosed));
        OnPropertyChanged(nameof(DispOpenDisplay));
        OnPropertyChanged(nameof(DispCloseDisplay));
        // Projector Door Selection
        OnPropertyChanged(nameof(DispSelectDoor));
        OnPropertyChanged(nameof(DispSelectDoorDesc));
        OnPropertyChanged(nameof(Confirm));
        OnPropertyChanged(nameof(DispDoorName));
        OnPropertyChanged(nameof(DispNoDoors));
        // WiFi & Device Validation
        OnPropertyChanged(nameof(WifiWarning));
        OnPropertyChanged(nameof(WifiWarningSearch));
        OnPropertyChanged(nameof(DeviceNotExist));
        OnPropertyChanged(nameof(DeviceUnreachable));
        OnPropertyChanged(nameof(CheckingConnection));
        OnPropertyChanged(nameof(PlayerNotExist));
        OnPropertyChanged(nameof(DevicesOfflineList));
        OnPropertyChanged(nameof(DevicesOfflineRenew));
        OnPropertyChanged(nameof(VerifyingDevices));
        OnPropertyChanged(nameof(DownloadedRecords));
        OnPropertyChanged(nameof(NoRecordsDevice));
        OnPropertyChanged(nameof(DownloadingLogs));
        // Player Operations (bilingual)
        OnPropertyChanged(nameof(SyncingCardToDevice));
        OnPropertyChanged(nameof(CardAssignedSynced));
        OnPropertyChanged(nameof(CardAssignedSyncErrors));
        OnPropertyChanged(nameof(CardRemovedSuccess));
        OnPropertyChanged(nameof(CardRemoveConstraint));
        OnPropertyChanged(nameof(CardNotFoundRemoved));
        OnPropertyChanged(nameof(PlayerNotFoundDeleted));
        OnPropertyChanged(nameof(RenewalCancelled));
        OnPropertyChanged(nameof(RenewedSuccessfully));
        OnPropertyChanged(nameof(RenewSyncErrors));
        OnPropertyChanged(nameof(SyncingCardDevices));
        OnPropertyChanged(nameof(RenewingSubscription));
        OnPropertyChanged(nameof(PlayerDbInconsistent));
        OnPropertyChanged(nameof(DeviceTimeout));
        OnPropertyChanged(nameof(ConcurrencyError));
        OnPropertyChanged(nameof(DatabaseError));
        OnPropertyChanged(nameof(SomeDevicesOffline));
        OnPropertyChanged(nameof(UnfreezingPlayer));
        OnPropertyChanged(nameof(FreezingAccount));
        OnPropertyChanged(nameof(RemovingCard));
        // General
        OnPropertyChanged(nameof(BtnClose));
        // Door Operations
        OnPropertyChanged(nameof(DoorOpenFailed));
        OnPropertyChanged(nameof(DoorCloseFailed));
        OnPropertyChanged(nameof(DoorDelayFailed));
        OnPropertyChanged(nameof(DoorPasswordFailed));
        OnPropertyChanged(nameof(DoorDelayInvalid));
        OnPropertyChanged(nameof(OpeningDoor));
        OnPropertyChanged(nameof(ClosingDoor));
        OnPropertyChanged(nameof(SettingDelay));
        OnPropertyChanged(nameof(SettingPassword));
        // Device Sync All Players
        OnPropertyChanged(nameof(DevSyncAllPlayers));
        OnPropertyChanged(nameof(DevSyncAllConfirm));
        OnPropertyChanged(nameof(DevSyncAllProgress));
        OnPropertyChanged(nameof(DevSyncAllDone));
        OnPropertyChanged(nameof(DevSyncAllNoCards));
        OnPropertyChanged(nameof(DevSynced));
        OnPropertyChanged(nameof(DevFailed));
        // Data Migration
        OnPropertyChanged(nameof(MigDataMigration));
        OnPropertyChanged(nameof(MigConnectionString));
        OnPropertyChanged(nameof(MigTableName));
        OnPropertyChanged(nameof(MigPreview));
        OnPropertyChanged(nameof(MigImport));
        OnPropertyChanged(nameof(MigConnecting));
        OnPropertyChanged(nameof(MigTotalRows));
        OnPropertyChanged(nameof(MigNewPlayers));
        OnPropertyChanged(nameof(MigDuplicates));
        OnPropertyChanged(nameof(MigSampleNames));
        OnPropertyChanged(nameof(MigConfirmImport));
        OnPropertyChanged(nameof(MigImportComplete));
        OnPropertyChanged(nameof(MigImported));
        OnPropertyChanged(nameof(MigSkipped));
        OnPropertyChanged(nameof(MigFailedCount));
        OnPropertyChanged(nameof(MigErrors));
        OnPropertyChanged(nameof(MigError));
        OnPropertyChanged(nameof(MigFillFields));
        OnPropertyChanged(nameof(MigTestConnection));
        OnPropertyChanged(nameof(MigTestingConnection));
        OnPropertyChanged(nameof(MigConnectionSuccess));
        OnPropertyChanged(nameof(MigConnectionFailed));

        OnPropertyChanged(nameof(RcpRenewalReceipt));
        OnPropertyChanged(nameof(RcpPlayerName));
        OnPropertyChanged(nameof(RcpSubscriptionType));
        OnPropertyChanged(nameof(RcpPeriod));
        OnPropertyChanged(nameof(RcpPrint));
        OnPropertyChanged(nameof(RcpMonth));
        OnPropertyChanged(nameof(RcpMonths));
        OnPropertyChanged(nameof(RcpDays));

        OnPropertyChanged(nameof(BulkOperations));
        OnPropertyChanged(nameof(BulkSelectOperation));
        OnPropertyChanged(nameof(BulkFreeze));
        OnPropertyChanged(nameof(BulkUnfreeze));
        OnPropertyChanged(nameof(BulkExtend));
        OnPropertyChanged(nameof(BulkExtendDays));
        OnPropertyChanged(nameof(BulkExtendDaysRequired));
        OnPropertyChanged(nameof(BulkTarget));
        OnPropertyChanged(nameof(BulkAllActive));
        OnPropertyChanged(nameof(BulkExpiring7));
        OnPropertyChanged(nameof(BulkExpired));
        OnPropertyChanged(nameof(BulkFrozenPlayers));
        OnPropertyChanged(nameof(BulkExecute));
        OnPropertyChanged(nameof(BulkConfirm));
        OnPropertyChanged(nameof(BulkNoPlayers));
        OnPropertyChanged(nameof(BulkComplete));
        OnPropertyChanged(nameof(BulkSuccessCount));
        OnPropertyChanged(nameof(BulkFailedCount));
        OnPropertyChanged(nameof(BulkUploadAll));
        OnPropertyChanged(nameof(BulkSelectDevices));
        OnPropertyChanged(nameof(BulkSelectAtLeastOneDevice));
        OnPropertyChanged(nameof(BulkUploadProgress));
        OnPropertyChanged(nameof(BulkUploadComplete));
        OnPropertyChanged(nameof(BulkUploaded));
        OnPropertyChanged(nameof(BulkSkipped));

        OnPropertyChanged(nameof(PrfPlayerProfile));
        OnPropertyChanged(nameof(PrfOverview));
        OnPropertyChanged(nameof(PrfFreezeHistory));
        OnPropertyChanged(nameof(PrfTransactions));
        OnPropertyChanged(nameof(PrfActivityLog));
        OnPropertyChanged(nameof(PrfAction));
        OnPropertyChanged(nameof(PrfPerformedBy));
        OnPropertyChanged(nameof(PrfDetails));
        OnPropertyChanged(nameof(PrfFreezeStart));
        OnPropertyChanged(nameof(PrfFreezeEnd));
        OnPropertyChanged(nameof(PrfNoRecords));
        OnPropertyChanged(nameof(FilterActive));
        // QR Pass
        OnPropertyChanged(nameof(QrNavTitle));
        OnPropertyChanged(nameof(QrCreatePass));
        OnPropertyChanged(nameof(QrPlayerName));
        OnPropertyChanged(nameof(QrValidDays));
        OnPropertyChanged(nameof(QrMaxUses));
        OnPropertyChanged(nameof(QrValidTo));
        OnPropertyChanged(nameof(QrActiveOnly));
        OnPropertyChanged(nameof(QrActiveToday));
        OnPropertyChanged(nameof(QrScanMode));
        OnPropertyChanged(nameof(QrScanReady));
        OnPropertyChanged(nameof(QrScanInstruction));
        OnPropertyChanged(nameof(QrPassCreated));
        OnPropertyChanged(nameof(QrPassDeactivated));
        OnPropertyChanged(nameof(QrConfirmDeactivate));
        OnPropertyChanged(nameof(QrDeactivate));
        OnPropertyChanged(nameof(QrCodeTitle));
        OnPropertyChanged(nameof(QrPrint));
        // Door Schedule
        OnPropertyChanged(nameof(WorkingHours));
        OnPropertyChanged(nameof(WorkingDays));
        OnPropertyChanged(nameof(Is24Hours));
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(SetSchedule));
        OnPropertyChanged(nameof(Monday));
        OnPropertyChanged(nameof(Tuesday));
        OnPropertyChanged(nameof(Wednesday));
        OnPropertyChanged(nameof(Thursday));
        OnPropertyChanged(nameof(Friday));
        OnPropertyChanged(nameof(Saturday));
        OnPropertyChanged(nameof(Sunday));
        OnPropertyChanged(nameof(AtLeastOneNameRequired));
        // Time Groups
        OnPropertyChanged(nameof(NavTimeGroups));
        OnPropertyChanged(nameof(TgTitle));
        OnPropertyChanged(nameof(TgSubtitle));
        OnPropertyChanged(nameof(TgAddNew));
        OnPropertyChanged(nameof(TgEdit));
        OnPropertyChanged(nameof(TgNameEn));
        OnPropertyChanged(nameof(TgNameAr));
        OnPropertyChanged(nameof(TgHwIndex));
        OnPropertyChanged(nameof(TgSchedule));
        OnPropertyChanged(nameof(TgDefault));
        OnPropertyChanged(nameof(TgFullAccess));
        OnPropertyChanged(nameof(TgClosed));
        OnPropertyChanged(nameof(TgActive));
        OnPropertyChanged(nameof(TgAddSegment));
        OnPropertyChanged(nameof(TgDeleteConfirm));
        OnPropertyChanged(nameof(TgCannotDeleteDefault));
        OnPropertyChanged(nameof(TgSlotsFull));
        OnPropertyChanged(nameof(TgSyncToDevice));
        OnPropertyChanged(nameof(TgSegments));
        OnPropertyChanged(nameof(TgSaved));
        OnPropertyChanged(nameof(TgDeleted));
        OnPropertyChanged(nameof(TgNameRequired));
        OnPropertyChanged(nameof(TgScheduleLabel));
        // Device Notification Bar
        OnPropertyChanged(nameof(BtnSync));
        // Filter Tooltips
        OnPropertyChanged(nameof(FilterTipAll));
        OnPropertyChanged(nameof(FilterTipToday));
        OnPropertyChanged(nameof(FilterTipYesterday));
        OnPropertyChanged(nameof(FilterTipThisWeek));
        OnPropertyChanged(nameof(FilterTipLastWeek));
        OnPropertyChanged(nameof(FilterTipThisMonth));
        OnPropertyChanged(nameof(FilterTipLastMonth));
        OnPropertyChanged(nameof(FilterTipLast3Months));
        OnPropertyChanged(nameof(FilterTipLast6Months));
        OnPropertyChanged(nameof(FilterTipThisYear));
        // Sync Status
        OnPropertyChanged(nameof(SyncStatus));
        OnPropertyChanged(nameof(SyncToDevice));
        OnPropertyChanged(nameof(PlayerSynced));
        OnPropertyChanged(nameof(PlayerSyncFailed));
        OnPropertyChanged(nameof(PlayerNoCard));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
