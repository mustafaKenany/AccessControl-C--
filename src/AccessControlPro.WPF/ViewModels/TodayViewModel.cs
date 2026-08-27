using System.Collections.ObjectModel;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

/// <summary>
/// "شاشة اليوم" / "Today" — the OWNER's home screen. Instead of the security-focused dashboard
/// (devices/doors/alarms), it answers the three questions a gym owner actually asks every morning:
/// how many people came in today, how many subscriptions were sold/renewed today, how much money
/// came in today — and WHO is about to expire (with photos) so he can call them before they lapse.
/// Deliberately photo-light: only the small "expiring this week" set loads images.
/// </summary>
public partial class TodayViewModel : ObservableObject
{
    private readonly IAccessEventRepository _eventRepository;
    private readonly IFinanceService _financeService;
    private readonly IEmployeeService _employeeService;
    private readonly IQrPassService _qrPassService;
    private readonly ILookupService _lookupService;
    private readonly CurrentUserService _currentUser;

    // Frozen brushes for the expiring-card accent (red ≤2 days, amber otherwise).
    internal static readonly System.Windows.Media.Brush BrushRed = Freeze("#F7685B");
    internal static readonly System.Windows.Media.Brush BrushAmber = Freeze("#FFB946");
    private static System.Windows.Media.Brush Freeze(string hex)
    {
        var b = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private int _entriesToday;
    [ObservableProperty] private int _subscriptionsToday;
    [ObservableProperty] private string _revenueTodayText = "0";
    [ObservableProperty] private int _expiringCount;
    [ObservableProperty] private bool _isLoading;

    public bool NoExpiring => ExpiringCount == 0;
    partial void OnExpiringCountChanged(int value) => OnPropertyChanged(nameof(NoExpiring));

    public ObservableCollection<ExpiringMemberVm> ExpiringSoon { get; } = new();

    private bool _isInitialized;

    public TodayViewModel(IAccessEventRepository eventRepository, IFinanceService financeService,
        IEmployeeService employeeService, IQrPassService qrPassService, ILookupService lookupService,
        CurrentUserService currentUser)
    {
        _eventRepository = eventRepository;
        _financeService = financeService;
        _employeeService = employeeService;
        _qrPassService = qrPassService;
        _lookupService = lookupService;
        _currentUser = currentUser;
        // Re-emit the bilingual labels when the operator toggles the app language.
        LanguageManager.Instance.PropertyChanged += (_, _) => RefreshLabels();
    }

    /// <summary>Only owners/staff with the Daily-Pass permission see the button (same gate the old
    /// Dashboard used). Daily Pass now lives ONLY here on the home screen.</summary>
    public bool CanDailyPass => _currentUser.HasPermission(AppPermission.PlayersDailyPass);

    public string DailyPassText => Lang.IsArabic ? "دخول يومي" : "Daily Pass";

    [RelayCommand]
    private void CreateDailyPass()
    {
        var dialog = new DailyPassDialog(_qrPassService, _lookupService, _financeService, _employeeService)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog();
    }

    // ── Bilingual labels (kept in the VM so the feature is one self-contained file) ──
    public string GreetingText
    {
        get
        {
            var name = _currentUser.DisplayName ?? _currentUser.Username ?? "";
            var hour = DateTime.Now.Hour;
            if (Lang.IsArabic)
            {
                var part = hour < 12 ? "صباح الخير" : hour < 17 ? "مساء الخير" : "مساء الخير";
                return string.IsNullOrWhiteSpace(name) ? part : $"{part}، {name}";
            }
            else
            {
                var part = hour < 12 ? "Good morning" : "Good evening";
                return string.IsNullOrWhiteSpace(name) ? part : $"{part}, {name}";
            }
        }
    }

    public string TodayDateText
    {
        get
        {
            try
            {
                var culture = Lang.IsArabic
                    ? System.Globalization.CultureInfo.GetCultureInfo("ar")
                    : System.Globalization.CultureInfo.InvariantCulture;
                return DateTime.Now.ToString(Lang.IsArabic ? "dddd، d MMMM yyyy" : "dddd, d MMMM yyyy", culture);
            }
            catch
            {
                return DateTime.Now.ToString("yyyy-MM-dd"); // globalization-invariant fallback
            }
        }
    }

    public string TitleText => Lang.IsArabic ? "شاشة اليوم" : "Today";
    public string EntriesLabel => Lang.IsArabic ? "دخول اليوم" : "Entries today";
    public string SubscriptionsLabel => Lang.IsArabic ? "اشتراكات اليوم" : "Subscriptions today";
    public string RevenueLabel => Lang.IsArabic ? "إيراد اليوم" : "Revenue today";
    public string ExpiringTitle => Lang.IsArabic ? "ينتهي اشتراكهم قريباً" : "Expiring soon";
    public string ExpiringHint => Lang.IsArabic ? "اتصل بهم قبل انتهاء الاشتراك" : "Call them before their subscription lapses";
    public string EmptyExpiringText => Lang.IsArabic ? "لا أحد ينتهي اشتراكه خلال 7 أيام 👍" : "No one expires within 7 days 👍";
    public string RefreshText => Lang.IsArabic ? "تحديث" : "Refresh";

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        ActivityLogger.LogNavigation("Today");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    /// <summary>Public reload used when the owner navigates back to Today (numbers move during the day).</summary>
    public Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var todayStart = DateTime.Today;
            var todayEnd = DateTime.Today.AddDays(1).AddTicks(-1);

            // Entries today — lightweight count straight from the event repo (no device/door load).
            try { EntriesToday = await _eventRepository.GetTodayCountAsync(); } catch { EntriesToday = 0; }

            // Subscriptions started/renewed today (a new member's StartDate is today too).
            try
            {
                var renewed = await _employeeService.GetRenewedAsync(todayStart, todayEnd);
                SubscriptionsToday = renewed.Count();
            }
            catch { SubscriptionsToday = 0; }

            // Money in today.
            try
            {
                var summary = await _financeService.GetSummaryAsync(todayStart, todayEnd);
                RevenueTodayText = $"{summary.TotalRevenue:N0} {Lang.IQD}";
            }
            catch { RevenueTodayText = $"0 {Lang.IQD}"; }

            // Who expires within the next 7 days — the actionable retention list, WITH photos
            // (small set only, so no OOM risk). Skip frozen members (they're paused on purpose).
            ExpiringSoon.Clear();
            try
            {
                var expiring = (await _employeeService.GetExpiringAsync(todayStart, DateTime.Today.AddDays(7).AddDays(1).AddTicks(-1)))
                    .Where(e => !e.IsFrozen)
                    .OrderBy(e => e.EndDate)
                    .Take(30);

                foreach (var e in expiring)
                {
                    var name = Lang.IsArabic && !string.IsNullOrWhiteSpace(e.FullNameAr) ? e.FullNameAr : e.FullNameEn;
                    var daysLeft = (e.EndDate.Date - DateTime.Today).Days;
                    ExpiringSoon.Add(new ExpiringMemberVm
                    {
                        Name = name,
                        Phone = e.Phone,
                        PhotoData = e.PhotoData,
                        DaysLeftText = DaysLeftLabel(daysLeft),
                        AccentBrush = daysLeft <= 2 ? BrushRed : BrushAmber
                    });
                }
            }
            catch { /* leave the list empty on error */ }
            ExpiringCount = ExpiringSoon.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string DaysLeftLabel(int days)
    {
        if (Lang.IsArabic)
        {
            if (days <= 0) return "ينتهي اليوم";
            if (days == 1) return "باقي يوم واحد";
            if (days == 2) return "باقي يومان";
            return $"باقي {days} أيام";
        }
        if (days <= 0) return "Expires today";
        if (days == 1) return "1 day left";
        return $"{days} days left";
    }

    /// <summary>Re-emit the bilingual label properties after a language switch so the view updates.</summary>
    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(GreetingText));
        OnPropertyChanged(nameof(TodayDateText));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(EntriesLabel));
        OnPropertyChanged(nameof(SubscriptionsLabel));
        OnPropertyChanged(nameof(RevenueLabel));
        OnPropertyChanged(nameof(ExpiringTitle));
        OnPropertyChanged(nameof(ExpiringHint));
        OnPropertyChanged(nameof(EmptyExpiringText));
        OnPropertyChanged(nameof(RefreshText));
        OnPropertyChanged(nameof(DailyPassText));
    }
}

/// <summary>One card in the "expiring soon" strip.</summary>
public class ExpiringMemberVm
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte[]? PhotoData { get; set; }
    public string DaysLeftText { get; set; } = string.Empty;
    public System.Windows.Media.Brush AccentBrush { get; set; } = TodayViewModel.BrushAmber;
}
