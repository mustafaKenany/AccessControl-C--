using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;

    public LanguageManager Lang => LanguageManager.Instance;

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

    public ObservableCollection<AccessEventDto> RecentEvents { get; } = new();
    public ObservableCollection<DoorStatusDto> DoorStatuses { get; } = new();

    private bool _isInitialized;

    public DashboardViewModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
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
