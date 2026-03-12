using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class AlertsViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedTab; // 0=Expiring, 1=Expired, 2=Frozen

    public ObservableCollection<EmployeeDto> ExpiringPlayers { get; } = new();
    public ObservableCollection<EmployeeDto> ExpiredPlayers { get; } = new();
    public ObservableCollection<EmployeeDto> FrozenPlayers { get; } = new();

    [ObservableProperty] private int _expiringCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private int _frozenCount;

    public AlertsViewModel(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var now = DateTime.UtcNow;

            // Expiring in next 7 days
            var expiring = await _employeeService.GetExpiringAsync(now, now.AddDays(7));
            ExpiringPlayers.Clear();
            foreach (var p in expiring)
                ExpiringPlayers.Add(p);
            ExpiringCount = ExpiringPlayers.Count;

            // Already expired
            var expired = await _employeeService.GetExpiredPlayersAsync();
            ExpiredPlayers.Clear();
            foreach (var p in expired)
                ExpiredPlayers.Add(p);
            ExpiredCount = ExpiredPlayers.Count;

            // Frozen
            var frozen = await _employeeService.GetFrozenPlayersAsync();
            FrozenPlayers.Clear();
            foreach (var p in frozen)
                FrozenPlayers.Add(p);
            FrozenCount = FrozenPlayers.Count;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ShowTab(string tab)
    {
        SelectedTab = tab switch
        {
            "expired" => 1,
            "frozen" => 2,
            _ => 0
        };
    }
}
