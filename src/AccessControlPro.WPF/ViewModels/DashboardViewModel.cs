using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly IQrPassService _qrPassService;
    private readonly ILookupService _lookupService;
    private readonly IFinanceService _financeService;
    private readonly IEmployeeService _employeeService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    public bool CanDailyPass => _currentUser.HasPermission(AppPermission.PlayersDailyPass);

    [ObservableProperty]
    private int _totalDevices;

    [ObservableProperty]
    private int _onlineDoors;

    [ObservableProperty]
    private int _todayEvents;

    [ObservableProperty]
    private int _activeAlarms;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showBackupWarning;

    [ObservableProperty]
    private string _backupWarningMessage = "";

    [ObservableProperty]
    private string _welcomeMessage = "";

    public ObservableCollection<AccessEventDto> RecentEvents { get; } = new();
    public ObservableCollection<DoorStatusDto> DoorStatuses { get; } = new();

    private bool _isInitialized;

    public DashboardViewModel(IDashboardService dashboardService, CurrentUserService currentUser,
        IQrPassService qrPassService, ILookupService lookupService, IFinanceService financeService,
        IEmployeeService employeeService)
    {
        _dashboardService = dashboardService;
        _qrPassService = qrPassService;
        _lookupService = lookupService;
        _financeService = financeService;
        _employeeService = employeeService;
        _currentUser = currentUser;
        WelcomeMessage = currentUser.DisplayName ?? "";
    }

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

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        ActivityLogger.LogNavigation("Dashboard");
        await LoadDashboardAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            var data = await _dashboardService.GetDashboardDataAsync();

            TotalDevices = data.TotalDevices;
            OnlineDoors = data.TotalDoors;
            TodayEvents = data.TodayEvents;
            ActiveAlarms = data.ActiveAlarms;

            RecentEvents.Clear();
            foreach (var e in data.RecentEvents)
                RecentEvents.Add(e);

            DoorStatuses.Clear();
            foreach (var d in data.DoorStatuses)
                DoorStatuses.Add(d);

            // Check backup health
            var backupStatus = BackupService.LoadStatus();
            if (backupStatus.ConsecutiveFailures >= 3)
            {
                ShowBackupWarning = true;
                BackupWarningMessage = Lang.IsArabic
                    ? $"فشل النسخ الاحتياطي {backupStatus.ConsecutiveFailures} مرات متتالية!\nآخر نسخة ناجحة: {backupStatus.LastSuccess?.ToString("yyyy-MM-dd HH:mm") ?? "لا يوجد"}\nالسبب: {backupStatus.LastError}"
                    : $"Backup failed {backupStatus.ConsecutiveFailures} consecutive times!\nLast successful: {backupStatus.LastSuccess?.ToString("yyyy-MM-dd HH:mm") ?? "Never"}\nReason: {backupStatus.LastError}";
            }
            else if (backupStatus.LastSuccess.HasValue && (DateTime.Now - backupStatus.LastSuccess.Value).TotalDays > 3)
            {
                ShowBackupWarning = true;
                BackupWarningMessage = Lang.IsArabic
                    ? $"لم يتم عمل نسخة احتياطية منذ {(int)(DateTime.Now - backupStatus.LastSuccess.Value).TotalDays} يوم!"
                    : $"No backup for {(int)(DateTime.Now - backupStatus.LastSuccess.Value).TotalDays} days!";
            }
            else
            {
                ShowBackupWarning = false;
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
