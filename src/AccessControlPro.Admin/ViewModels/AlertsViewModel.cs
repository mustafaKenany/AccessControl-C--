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
    private readonly IInventoryService _inventoryService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedTab; // 0=Expiring 1=Expired 2=Frozen 3=LowStock 4=ExpiringProducts

    public ObservableCollection<EmployeeDto> ExpiringPlayers { get; } = new();
    public ObservableCollection<EmployeeDto> ExpiredPlayers { get; } = new();
    public ObservableCollection<EmployeeDto> FrozenPlayers { get; } = new();
    // Feature 6 — product (retail) alerts.
    public ObservableCollection<ProductDto> LowStockProducts { get; } = new();
    public ObservableCollection<ProductDto> ExpiringProducts { get; } = new();

    [ObservableProperty] private int _expiringCount;
    [ObservableProperty] private int _expiredCount;
    [ObservableProperty] private int _frozenCount;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _expiringProductsCount;

    public AlertsViewModel(IEmployeeService employeeService, IInventoryService inventoryService)
    {
        _employeeService = employeeService;
        _inventoryService = inventoryService;
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

            // Product low-stock (at/under reorder level or out of stock)
            var lowStock = await _inventoryService.GetLowStockProductsAsync();
            LowStockProducts.Clear();
            foreach (var p in lowStock) LowStockProducts.Add(p);
            LowStockCount = LowStockProducts.Count;

            // Products expiring within 30 days (and expired items still in stock)
            var expiringProducts = await _inventoryService.GetExpiringProductsAsync(30);
            ExpiringProducts.Clear();
            foreach (var p in expiringProducts) ExpiringProducts.Add(p);
            ExpiringProductsCount = ExpiringProducts.Count;
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
            "lowstock" => 3,
            "expiringproducts" => 4,
            _ => 0
        };
    }
}
