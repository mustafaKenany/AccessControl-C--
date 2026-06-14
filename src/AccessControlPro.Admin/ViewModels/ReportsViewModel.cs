using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;
    private readonly IFinanceService _financeService;
    private readonly IInventoryService _inventoryService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedReportType; // 0=Players 1=Finance 2=Inventory 3=Sales 4=Purchases 5=Movements
    [ObservableProperty] private DateTime _dateFrom = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;

    public ObservableCollection<EmployeeDto> PlayerResults { get; } = new();
    public ObservableCollection<TransactionDto> FinanceResults { get; } = new();
    public ObservableCollection<ProductDto> InventoryResults { get; } = new();
    public ObservableCollection<SalesReportRow> SalesResults { get; } = new();
    public ObservableCollection<PurchaseOrderDto> PurchaseResults { get; } = new();
    public ObservableCollection<StockMovementDto> MovementResults { get; } = new();

    [ObservableProperty] private int _resultCount;
    [ObservableProperty] private decimal _reportTotal;       // sales revenue / purchases total for the period
    [ObservableProperty] private decimal _reportTotalProfit; // sales profit for the period

    public ReportsViewModel(
        IEmployeeService employeeService,
        IFinanceService financeService,
        IInventoryService inventoryService)
    {
        _employeeService = employeeService;
        _financeService = financeService;
        _inventoryService = inventoryService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            switch (SelectedReportType)
            {
                case 0: // Players expiring in range
                    var expiring = await _employeeService.GetExpiringAsync(DateFrom, DateTo);
                    PlayerResults.Clear();
                    foreach (var p in expiring) PlayerResults.Add(p);
                    ResultCount = PlayerResults.Count;
                    break;

                case 1: // Finance summary
                    var summary = await _financeService.GetSummaryAsync(DateFrom, DateTo);
                    FinanceResults.Clear();
                    foreach (var t in summary.RecentTransactions) FinanceResults.Add(t);
                    ResultCount = FinanceResults.Count;
                    break;

                case 2: // Inventory
                    var products = await _inventoryService.GetAllProductsAsync();
                    InventoryResults.Clear();
                    foreach (var p in products) InventoryResults.Add(p);
                    ResultCount = InventoryResults.Count;
                    break;

                case 3: // Sales by product (from OUT stock movements in range)
                    var moves = await _inventoryService.GetStockMovementsAsync(DateFrom, DateTo);
                    var costMap = (await _inventoryService.GetAllProductsAsync())
                        .ToDictionary(p => p.Id, p => p.CostPrice);
                    var sales = moves
                        .Where(m => m.Type == AccessControlPro.Domain.Enums.MovementType.Out)
                        .GroupBy(m => new { m.ProductId, m.ProductName })
                        .Select(g => new SalesReportRow
                        {
                            ProductName = g.Key.ProductName,
                            Quantity = g.Sum(x => x.Quantity),
                            Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
                            Profit = g.Sum(x => x.Quantity * (x.UnitPrice - (costMap.TryGetValue(g.Key.ProductId, out var c) ? c : 0)))
                        })
                        .OrderByDescending(r => r.Revenue)
                        .ToList();
                    SalesResults.Clear();
                    foreach (var s in sales) SalesResults.Add(s);
                    ResultCount = SalesResults.Count;
                    ReportTotal = sales.Sum(s => s.Revenue);
                    ReportTotalProfit = sales.Sum(s => s.Profit);
                    break;

                case 4: // Purchases (POs in range)
                    var orders = (await _inventoryService.GetAllPurchaseOrdersAsync())
                        .Where(o => o.OrderDate.Date >= DateFrom.Date && o.OrderDate.Date <= DateTo.Date)
                        .OrderByDescending(o => o.OrderDate)
                        .ToList();
                    PurchaseResults.Clear();
                    foreach (var o in orders) PurchaseResults.Add(o);
                    ResultCount = PurchaseResults.Count;
                    ReportTotal = orders.Sum(o => o.TotalAmount - o.Discount);
                    break;

                case 5: // Stock movements in range
                    var movements = await _inventoryService.GetStockMovementsAsync(DateFrom, DateTo);
                    MovementResults.Clear();
                    foreach (var m in movements) MovementResults.Add(m);
                    ResultCount = MovementResults.Count;
                    break;
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SelectReport(string type)
    {
        SelectedReportType = type switch
        {
            "finance" => 1,
            "inventory" => 2,
            "sales" => 3,
            "purchases" => 4,
            "movements" => 5,
            _ => 0
        };
    }

    // Sanitize CSV field to prevent formula injection (=, +, -, @, tab, CR)
    private static string CsvSafe(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var s = value.Replace("\"", "\"\"");
        // Prefix dangerous chars that Excel treats as formulas
        if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@' || s[0] == '\t' || s[0] == '\r'))
            s = "'" + s;
        return s;
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Lang.IsArabic ? "تصدير التقرير" : "Export Report",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var lines = new List<string>();

            switch (SelectedReportType)
            {
                case 0:
                    lines.Add("Name,Phone,Subscription,EndDate,Fee,Paid,Remaining");
                    foreach (var p in PlayerResults)
                        lines.Add($"\"{CsvSafe(p.FullNameEn)}\",\"{CsvSafe(p.Phone)}\",\"{CsvSafe(p.SubscriptionType)}\",{p.EndDate:yyyy-MM-dd},{p.SubscriptionFee},{p.AmountPaid},{p.RemainingBalance}");
                    break;

                case 1:
                    lines.Add("Date,Type,Category,Amount,Description");
                    foreach (var t in FinanceResults)
                        lines.Add($"{t.CreatedAt:yyyy-MM-dd},{t.Type},\"{CsvSafe(t.Category)}\",{t.Amount},\"{CsvSafe(t.Description)}\"");
                    break;

                case 2:
                    lines.Add("Name,NameAr,Barcode,Price,Cost,Profit,Category,Stock");
                    foreach (var p in InventoryResults)
                        lines.Add($"\"{CsvSafe(p.Name)}\",\"{CsvSafe(p.NameAr)}\",\"{CsvSafe(p.Barcode)}\",{p.Price},{p.CostPrice},{p.Profit},\"{CsvSafe(p.Category)}\",{p.Stock}");
                    break;

                case 3:
                    lines.Add("Product,Quantity,Revenue,Profit");
                    foreach (var s in SalesResults)
                        lines.Add($"\"{CsvSafe(s.ProductName)}\",{s.Quantity},{s.Revenue},{s.Profit}");
                    break;

                case 4:
                    lines.Add("Date,Supplier,Total,Discount,Paid,Remaining,Status");
                    foreach (var o in PurchaseResults)
                        lines.Add($"{o.OrderDate:yyyy-MM-dd},\"{CsvSafe(o.SupplierName)}\",{o.TotalAmount},{o.Discount},{o.AmountPaid},{o.RemainingAmount},\"{CsvSafe(o.PaymentStatus)}\"");
                    break;

                case 5:
                    lines.Add("Date,Product,Type,Quantity,UnitPrice,Reference,By");
                    foreach (var m in MovementResults)
                        lines.Add($"{m.CreatedAt:yyyy-MM-dd HH:mm},\"{CsvSafe(m.ProductName)}\",{m.Type},{m.Quantity},{m.UnitPrice},\"{CsvSafe(m.Reference)}\",\"{CsvSafe(m.CreatedBy)}\"");
                    break;
            }

            await File.WriteAllLinesAsync(dialog.FileName, lines);
            CustomMessageBox.Show(
                Lang.IsArabic ? "تم التصدير بنجاح" : "Export completed successfully",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }
}

/// <summary>One row of the per-product sales report: quantity sold, revenue, and profit.</summary>
public class SalesReportRow
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
}
