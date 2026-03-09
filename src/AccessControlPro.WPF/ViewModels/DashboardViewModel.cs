using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
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

    public ObservableCollection<AccessEventDto> RecentEvents { get; } = new();
    public ObservableCollection<DoorStatusDto> DoorStatuses { get; } = new();

    public DashboardViewModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        _ = LoadDashboardAsync();
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
        }
        catch
        {
            // Dashboard loads with zeros on first run (empty DB)
        }
        finally
        {
            IsLoading = false;
        }
    }
}
