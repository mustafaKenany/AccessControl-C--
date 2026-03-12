using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class AdminDashboardViewModel : ObservableObject
{
    private readonly IFinanceService _financeService;
    private readonly IEmployeeService _employeeService;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditLogService _auditLogService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _totalPlayers;
    [ObservableProperty] private int _expiringThisWeek;
    [ObservableProperty] private int _frozenPlayers;
    [ObservableProperty] private decimal _monthlyRevenue;
    [ObservableProperty] private decimal _monthlyExpenses;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _unpaidBalances;
    [ObservableProperty] private decimal _supplierDebt;
    [ObservableProperty] private int _lowStockCount;

    public ObservableCollection<AuditLogDto> RecentLogs { get; } = new();
    public ObservableCollection<EmployeeDto> ExpiringPlayers { get; } = new();
    public ObservableCollection<ProductDto> LowStockProducts { get; } = new();

    public AdminDashboardViewModel(
        IFinanceService financeService,
        IEmployeeService employeeService,
        IInventoryService inventoryService,
        IAuditLogService auditLogService)
    {
        _financeService = financeService;
        _employeeService = employeeService;
        _inventoryService = inventoryService;
        _auditLogService = auditLogService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var weekEnd = now.AddDays(7);

            // Finance
            var summary = await _financeService.GetSummaryAsync(monthStart, now);
            MonthlyRevenue = summary.TotalRevenue;
            MonthlyExpenses = summary.TotalExpenses;
            NetProfit = summary.NetProfit;
            UnpaidBalances = summary.UnpaidBalances;

            // Players
            var allPlayers = await _employeeService.GetAllEmployeesAsync();
            TotalPlayers = allPlayers.Count();

            var expiring = await _employeeService.GetExpiringAsync(now, weekEnd);
            ExpiringThisWeek = expiring.Count();
            ExpiringPlayers.Clear();
            foreach (var p in expiring.Take(10))
                ExpiringPlayers.Add(p);

            var frozen = await _employeeService.GetFrozenPlayersAsync();
            FrozenPlayers = frozen.Count();

            // Inventory
            var products = await _inventoryService.GetAllProductsAsync();
            var lowStock = products.Where(p => p.IsActive && p.Stock <= 5).ToList();
            LowStockCount = lowStock.Count;
            LowStockProducts.Clear();
            foreach (var p in lowStock)
                LowStockProducts.Add(p);

            // Supplier debt
            var orders = await _inventoryService.GetAllPurchaseOrdersAsync();
            SupplierDebt = orders.Sum(o => o.RemainingAmount);

            // Audit log
            var (logs, _) = await _auditLogService.GetPagedAsync(1, 15);
            RecentLogs.Clear();
            foreach (var l in logs)
                RecentLogs.Add(l);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }
}
