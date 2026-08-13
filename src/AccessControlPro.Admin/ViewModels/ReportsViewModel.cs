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
    private readonly ISupplierService _supplierService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedReportType; // 0=Players 1=Finance 2=Inventory 3=Sales 4=Purchases 5=Movements 6=Supplier 7=Daily
    [ObservableProperty] private DateTime _dateFrom = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;

    // Supplier report
    [ObservableProperty] private SupplierDto? _selectedSupplierFilter;
    [ObservableProperty] private decimal _supplierBalance; // overall remaining owed to the selected supplier (all-time)
    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public ObservableCollection<EmployeeDto> PlayerResults { get; } = new();
    public ObservableCollection<TransactionDto> FinanceResults { get; } = new();
    public ObservableCollection<ProductDto> InventoryResults { get; } = new();
    public ObservableCollection<SalesReportRow> SalesResults { get; } = new();
    public ObservableCollection<PurchaseOrderDto> PurchaseResults { get; } = new();
    public ObservableCollection<StockMovementDto> MovementResults { get; } = new();
    public ObservableCollection<SupplierPurchaseRow> SupplierResults { get; } = new();
    public ObservableCollection<DailyEntryRow> DailyResults { get; } = new();

    [ObservableProperty] private int _resultCount;
    [ObservableProperty] private decimal _reportTotal;       // sales revenue / purchases total for the period
    [ObservableProperty] private decimal _reportTotalProfit; // sales profit for the period

    private readonly IPosService _posService;

    public ReportsViewModel(
        IEmployeeService employeeService,
        IFinanceService financeService,
        IInventoryService inventoryService,
        ISupplierService supplierService,
        IPosService posService)
    {
        _employeeService = employeeService;
        _financeService = financeService;
        _inventoryService = inventoryService;
        _supplierService = supplierService;
        _posService = posService;
    }

    /// <summary>Players who currently owe money from POS credit sales, with debt-aging (days owing).</summary>
    public ObservableCollection<EmployeeDto> DebtorResults { get; } = new();

    /// <summary>Populate the supplier picker the first time the Supplier report is opened.</summary>
    public async Task EnsureSuppliersLoadedAsync()
    {
        if (Suppliers.Count > 0) return;
        try
        {
            var suppliers = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            foreach (var s in suppliers) Suppliers.Add(s);
        }
        catch { /* non-fatal: combo just stays empty */ }
    }

    public bool IsSupplierReport => SelectedReportType == 6;

    partial void OnSelectedReportTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsSupplierReport));
        if (value == 6) _ = EnsureSuppliersLoadedAsync();
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

                case 6: // Supplier purchases (itemized) + outstanding balance
                    await EnsureSuppliersLoadedAsync();
                    SupplierResults.Clear();
                    ReportTotal = 0;
                    SupplierBalance = 0;
                    if (SelectedSupplierFilter == null)
                    {
                        ResultCount = 0;
                        break;
                    }
                    var sid = SelectedSupplierFilter.Id;
                    var theirOrders = (await _inventoryService.GetAllPurchaseOrdersAsync())
                        .Where(o => o.SupplierId == sid)
                        .ToList();
                    // Overall outstanding balance owed to this supplier (all-time, not just the range).
                    SupplierBalance = theirOrders.Sum(o => o.RemainingAmount);
                    // Itemized purchases within the selected date range, aggregated by product.
                    var inRange = theirOrders
                        .Where(o => o.OrderDate.Date >= DateFrom.Date && o.OrderDate.Date <= DateTo.Date)
                        .ToList();
                    var rows = inRange
                        .SelectMany(o => o.Items)
                        .GroupBy(i => i.ProductName)
                        .Select(g => new SupplierPurchaseRow
                        {
                            ProductName = g.Key,
                            Quantity = g.Sum(x => x.Quantity),
                            Total = g.Sum(x => x.TotalCost)
                        })
                        .OrderByDescending(r => r.Total)
                        .ToList();
                    foreach (var r in rows) SupplierResults.Add(r);
                    ResultCount = rows.Count;
                    ReportTotal = inRange.Sum(o => o.TotalAmount - o.Discount);
                    break;

                case 7: // Daily entries — daily-pass income within the range (count + revenue)
                    var incomes = await _financeService.GetTransactionsAsync(
                        Domain.Enums.TransactionType.Income,
                        DateFrom.Date, DateTo.Date.AddDays(1).AddTicks(-1));
                    var dailyRows = incomes
                        .Where(t => IsDailyPassCategory(t.Category))
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => new DailyEntryRow
                        {
                            When = t.CreatedAt,
                            Tier = t.Category,
                            Amount = t.Amount,
                            Description = t.Description
                        })
                        .ToList();
                    DailyResults.Clear();
                    foreach (var r in dailyRows) DailyResults.Add(r);
                    ResultCount = dailyRows.Count;
                    ReportTotal = dailyRows.Sum(r => r.Amount);
                    break;

                case 8: // Debtors — players who owe money, most-owed first, with days-owing aging
                    var debtors = (await _posService.GetPlayersWithDebtAsync())
                        .OrderByDescending(e => e.Debt).ToList();
                    DebtorResults.Clear();
                    foreach (var d in debtors) DebtorResults.Add(d);
                    ResultCount = debtors.Count;
                    ReportTotal = debtors.Sum(d => d.Debt);
                    break;
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    /// <summary>A transaction belongs to the daily-entry report if its category is a daily-pass
    /// plan — same heuristic the Daily Pass dialog uses to pick plans (name starts "Daily" or
    /// contains "يومي"), so every entry it records is captured here.</summary>
    private static bool IsDailyPassCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        return category.TrimStart().StartsWith("Daily", StringComparison.OrdinalIgnoreCase)
            || category.Contains("يومي");
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
            "supplier" => 6,
            "daily" => 7,
            "debtors" => 8,
            _ => 0
        };
    }

    [RelayCommand]
    private void PrintReport()
    {
        bool ar = Lang.IsArabic;
        var period = $"{DateFrom:yyyy-MM-dd}  →  {DateTo:yyyy-MM-dd}";
        string title;
        string[] headers;
        double[] widths;
        int[] rightCols;
        List<IReadOnlyList<string>> rows = new();
        IReadOnlyList<string>? total = null;

        switch (SelectedReportType)
        {
            case 0:
                title = Lang.RptPlayers;
                headers = new[] { Lang.PlayerName, Lang.Phone, Lang.Subscription, Lang.RptEndDate, Lang.Fee, Lang.Paid, Lang.Remaining };
                widths = new[] { 2.2, 1.4, 1.6, 1.2, 1.0, 1.0, 1.0 };
                rightCols = new[] { 4, 5, 6 };
                foreach (var p in PlayerResults)
                    rows.Add(new[] { p.FullNameEn, p.Phone, p.SubscriptionType, p.EndDate.ToString("yyyy-MM-dd"), p.SubscriptionFee.ToString("N0"), p.AmountPaid.ToString("N0"), p.RemainingBalance.ToString("N0") });
                break;
            case 1:
                title = Lang.RptFinance;
                headers = new[] { Lang.SmDate, Lang.SmType, Lang.PrdCategory, Lang.PrdPrice, Lang.AuditDetails };
                widths = new[] { 1.4, 1.0, 1.6, 1.2, 3.0 };
                rightCols = new[] { 3 };
                foreach (var t in FinanceResults)
                    rows.Add(new[] { t.CreatedAt.ToString("yyyy-MM-dd"), t.Type.ToString(), t.Category, t.Amount.ToString("N0"), t.Description });
                break;
            case 2:
                title = Lang.RptInventory;
                headers = new[] { Lang.PrdName, Lang.PrdNameAr, Lang.PrdBarcode, Lang.PrdPrice, Lang.PrdCostPrice, Lang.PrdProfit, Lang.PrdCategory, Lang.PrdStock };
                widths = new[] { 2.0, 2.0, 1.2, 1.0, 1.0, 1.0, 1.2, 0.8 };
                rightCols = new[] { 3, 4, 5, 7 };
                foreach (var p in InventoryResults)
                    rows.Add(new[] { p.Name, p.NameAr, p.Barcode, p.Price.ToString("N0"), p.CostPrice.ToString("N0"), p.Profit.ToString("N0"), p.Category, p.Stock.ToString() });
                break;
            case 3:
                title = Lang.RptSales;
                headers = new[] { Lang.PrdName, ar ? "الكمية" : "Qty", ar ? "الإيراد" : "Revenue", Lang.PrdProfit };
                widths = new[] { 3.0, 1.0, 1.4, 1.4 };
                rightCols = new[] { 1, 2, 3 };
                foreach (var s in SalesResults)
                    rows.Add(new[] { s.ProductName, s.Quantity.ToString(), s.Revenue.ToString("N0"), s.Profit.ToString("N0") });
                total = new[] { ar ? "الإجمالي" : "TOTAL", "", ReportTotal.ToString("N0"), ReportTotalProfit.ToString("N0") };
                break;
            case 4:
                title = Lang.RptPurchases;
                headers = new[] { Lang.RptEndDate, Lang.NavSuppliers, ar ? "الإجمالي" : "Total", Lang.Paid, Lang.Remaining, ar ? "الحالة" : "Status" };
                widths = new[] { 1.2, 2.0, 1.2, 1.2, 1.2, 1.0 };
                rightCols = new[] { 2, 3, 4 };
                foreach (var o in PurchaseResults)
                    rows.Add(new[] { o.OrderDate.ToString("yyyy-MM-dd"), o.SupplierName, o.TotalAmount.ToString("N0"), o.AmountPaid.ToString("N0"), o.RemainingAmount.ToString("N0"), o.PaymentStatus });
                total = new[] { ar ? "الإجمالي" : "TOTAL", "", ReportTotal.ToString("N0"), "", "", "" };
                break;
            case 5:
                title = Lang.RptMovements;
                headers = new[] { Lang.RptEndDate, Lang.PrdName, ar ? "وارد/صادر" : "In/Out", ar ? "الكمية" : "Qty", Lang.PrdPrice, ar ? "المرجع" : "Ref" };
                widths = new[] { 1.6, 2.4, 1.0, 0.8, 1.2, 1.4 };
                rightCols = new[] { 3, 4 };
                foreach (var m in MovementResults)
                    rows.Add(new[] { m.CreatedAt.ToString("yyyy-MM-dd HH:mm"), m.ProductName, m.Type.ToString(), m.Quantity.ToString(), m.UnitPrice.ToString("N0"), m.Reference ?? "" });
                break;
            case 6:
                title = $"{Lang.RptSupplier}: {SelectedSupplierFilter?.Name}";
                headers = new[] { Lang.PrdName, ar ? "الكمية" : "Qty", ar ? "الإجمالي" : "Total" };
                widths = new[] { 3.0, 1.2, 1.6 };
                rightCols = new[] { 1, 2 };
                foreach (var r in SupplierResults)
                    rows.Add(new[] { r.ProductName, r.Quantity.ToString(), r.Total.ToString("N0") });
                total = new[] { ar ? $"المشتريات: {ReportTotal:N0}  —  المستحق: {SupplierBalance:N0}" : $"Purchased: {ReportTotal:N0}  —  Outstanding: {SupplierBalance:N0}", "", "" };
                break;
            case 7:
                title = Lang.RptDaily;
                headers = new[] { Lang.SmDate, Lang.PrdCategory, Lang.PrdPrice, Lang.AuditDetails };
                widths = new[] { 1.6, 1.6, 1.2, 3.0 };
                rightCols = new[] { 2 };
                foreach (var r in DailyResults)
                    rows.Add(new[] { r.When.ToString("yyyy-MM-dd HH:mm"), r.Tier, r.Amount.ToString("N0"), r.Description });
                total = new[] { ar ? $"العدد: {ResultCount}  —  الإجمالي" : $"Count: {ResultCount}  —  TOTAL", "", ReportTotal.ToString("N0"), "" };
                break;
            default:
                return;
        }

        if (rows.Count == 0)
        {
            CustomMessageBox.Show(
                ar ? "لا توجد بيانات للطباعة" : "No data to print",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        TableReportPrinter.Print(title, period, headers, widths, rows, rightCols, total, docName: title);
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

                case 6:
                    lines.Add($"Supplier,\"{CsvSafe(SelectedSupplierFilter?.Name)}\"");
                    lines.Add($"Outstanding balance,{SupplierBalance}");
                    lines.Add($"Purchased in range (net),{ReportTotal}");
                    lines.Add("");
                    lines.Add("Product,Quantity,Total");
                    foreach (var r in SupplierResults)
                        lines.Add($"\"{CsvSafe(r.ProductName)}\",{r.Quantity},{r.Total}");
                    break;

                case 7:
                    lines.Add("DateTime,Tier,Amount,Description");
                    foreach (var r in DailyResults)
                        lines.Add($"{r.When:yyyy-MM-dd HH:mm},\"{CsvSafe(r.Tier)}\",{r.Amount},\"{CsvSafe(r.Description)}\"");
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

/// <summary>One row of the supplier report: total quantity and cost of a product bought from that supplier.</summary>
public class SupplierPurchaseRow
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Total { get; set; }
}

/// <summary>One daily-entry (daily-pass) row: when it was sold, which tier/plan, the amount, and detail.</summary>
public class DailyEntryRow
{
    public DateTime When { get; set; }
    public string Tier { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}
