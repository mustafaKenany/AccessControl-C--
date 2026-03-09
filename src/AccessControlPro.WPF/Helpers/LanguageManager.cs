using System.ComponentModel;
using System.Globalization;
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

    // Assign Card Dialog
    public string AuthorizeCard => GetString("AuthorizeCard");
    public string CardNumber => GetString("CardNumber");
    public string CardPassword => GetString("CardPassword");
    public string CardMode => GetString("CardMode");
    public string CardType => GetString("CardType");
    public string DoorAccess => GetString("DoorAccess");
    public string EffectiveTimes => GetString("EffectiveTimes");
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

    // Deleted Records Section
    public string NavDeletedRecords => GetString("NavDeletedRecords");
    public string DeletedDate => GetString("DeletedDate");
    public string ClickShowAllDeleted => GetString("ClickShowAllDeleted");
    public string DeletedByPrefix => GetString("DeletedByPrefix");

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

    public void SetLanguage(string cultureCode)
    {
        _currentCulture = new CultureInfo(cultureCode);
        Thread.CurrentThread.CurrentUICulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;

        IsArabic = cultureCode.StartsWith("ar");
        FlowDirection = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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
        OnPropertyChanged(nameof(AuthorizeCard));
        OnPropertyChanged(nameof(CardNumber));
        OnPropertyChanged(nameof(CardPassword));
        OnPropertyChanged(nameof(CardMode));
        OnPropertyChanged(nameof(CardType));
        OnPropertyChanged(nameof(DoorAccess));
        OnPropertyChanged(nameof(EffectiveTimes));
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
        OnPropertyChanged(nameof(EditReason));
        OnPropertyChanged(nameof(EnterEditReason));
        OnPropertyChanged(nameof(EditReasonRequired));
        OnPropertyChanged(nameof(NavLogs));
        OnPropertyChanged(nameof(LogTimestamp));
        OnPropertyChanged(nameof(LogAction));
        OnPropertyChanged(nameof(LogEntity));
        OnPropertyChanged(nameof(LogDetails));
        OnPropertyChanged(nameof(ClickShowAllLogs));
        OnPropertyChanged(nameof(NavDeletedRecords));
        OnPropertyChanged(nameof(DeletedDate));
        OnPropertyChanged(nameof(ClickShowAllDeleted));
        OnPropertyChanged(nameof(DeletedByPrefix));
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
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
