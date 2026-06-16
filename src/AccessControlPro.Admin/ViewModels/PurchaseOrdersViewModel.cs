using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class PurchaseOrdersViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ISupplierService _supplierService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;

    // Tab: 0 = Purchase Orders, 1 = Stock Movements, 2 = Supplier Balances
    [ObservableProperty] private int _selectedTab;

    // New PO fields
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private string _editQuantity = "";
    [ObservableProperty] private string _editUnitCost = "";
    [ObservableProperty] private string _editDiscount = "";
    [ObservableProperty] private string _editAmountPaid = "";
    [ObservableProperty] private string _editNotes = "";
    [ObservableProperty] private decimal _poTotal;

    // Stock movements filter
    [ObservableProperty] private ProductDto? _filterProduct;
    [ObservableProperty] private int _filterTypeIndex; // 0=All, 1=In, 2=Out

    // Selected order for detail + pay
    [ObservableProperty] private PurchaseOrderDto? _selectedOrder;
    [ObservableProperty] private string _payAmount = "";

    // Order history search
    [ObservableProperty] private string _orderSearchText = "";

    // Edit mode: when editing an existing PO, the builder on the right acts as an editor.
    [ObservableProperty] private bool _isEditingOrder;
    private int _editingOrderId;
    public string SubmitButtonText => IsEditingOrder
        ? (Lang.IsArabic ? "تحديث الطلب" : "Update Order")
        : Lang.PoSubmit;
    partial void OnIsEditingOrderChanged(bool value) => OnPropertyChanged(nameof(SubmitButtonText));

    private readonly List<PurchaseOrderDto> _allOrders = new();
    public ObservableCollection<PurchaseOrderDto> Orders { get; } = new();
    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<ProductDto> AllProducts { get; } = new();
    public ObservableCollection<PurchaseOrderItemDto> PoItems { get; } = new();
    public ObservableCollection<StockMovementDto> StockMovements { get; } = new();
    public ObservableCollection<StockMovementDto> FilteredMovements { get; } = new();
    public ObservableCollection<SupplierBalanceDto> SupplierBalances { get; } = new();

    public PurchaseOrdersViewModel(IInventoryService inventoryService, ISupplierService supplierService)
    {
        _inventoryService = inventoryService;
        _supplierService = supplierService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var orders = await _inventoryService.GetAllPurchaseOrdersAsync();
            _allOrders.Clear();
            _allOrders.AddRange(orders.OrderByDescending(o => o.OrderDate));
            ApplyOrderFilter();

            var suppliers = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            foreach (var s in suppliers)
                Suppliers.Add(s);

            var products = await _inventoryService.GetAllProductsAsync();
            Products.Clear();
            AllProducts.Clear();
            foreach (var p in products)
            {
                AllProducts.Add(p);
                if (p.IsActive)
                    Products.Add(p);
            }

            var movements = await _inventoryService.GetAllStockMovementsAsync();
            StockMovements.Clear();
            foreach (var m in movements)
                StockMovements.Add(m);

            ApplyMovementFilter();
            RebuildSupplierBalances();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    partial void OnFilterProductChanged(ProductDto? value) => ApplyMovementFilter();
    partial void OnFilterTypeIndexChanged(int value) => ApplyMovementFilter();
    partial void OnOrderSearchTextChanged(string value) => ApplyOrderFilter();

    private void ApplyOrderFilter()
    {
        Orders.Clear();
        var q = _allOrders.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(OrderSearchText))
        {
            var t = OrderSearchText.Trim();
            q = q.Where(o =>
                (o.SupplierName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (o.PaymentStatus?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (o.Notes?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                o.Id.ToString().Contains(t) ||
                o.OrderDate.ToString("yyyy-MM-dd").Contains(t) ||
                (o.Items?.Any(i => i.ProductName?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ?? false));
        }
        foreach (var o in q) Orders.Add(o);
    }

    private void ApplyMovementFilter()
    {
        FilteredMovements.Clear();
        var filtered = StockMovements.AsEnumerable();

        if (FilterProduct != null)
            filtered = filtered.Where(m => m.ProductId == FilterProduct.Id);

        if (FilterTypeIndex == 1)
            filtered = filtered.Where(m => m.Type == MovementType.In);
        else if (FilterTypeIndex == 2)
            filtered = filtered.Where(m => m.Type == MovementType.Out);

        foreach (var m in filtered)
            FilteredMovements.Add(m);
    }

    [RelayCommand]
    private void ClearMovementFilter()
    {
        FilterProduct = null;
        FilterTypeIndex = 0;
    }

    [RelayCommand]
    private void PrintMovements()
    {
        bool ar = Lang.IsArabic;
        var headers = new[]
        {
            Lang.SmDate, Lang.SmType, Lang.SmProduct, Lang.SmQty,
            Lang.SmPrice, Lang.SmRef, Lang.SmSupplier, Lang.PoBy
        };
        var widths = new[] { 1.6, 1.0, 2.4, 0.8, 1.2, 1.4, 1.6, 1.2 };
        var rows = FilteredMovements.Select(m => (IReadOnlyList<string>)new[]
        {
            m.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            m.Type == MovementType.In ? (ar ? "وارد" : "In") : (ar ? "صادر" : "Out"),
            m.ProductName,
            m.Quantity.ToString(),
            m.UnitPrice.ToString("N0"),
            m.Reference ?? "",
            m.SupplierName ?? "",
            m.CreatedBy ?? ""
        }).ToList();

        var period = FilterProduct != null ? FilterProduct.Name : (ar ? "كل المواد" : "All products");
        TableReportPrinter.Print(
            ar ? "تقرير حركة المخزون" : "Stock Movements Report",
            period, headers, widths, rows,
            rightAlignColumns: new[] { 3, 4 },
            docName: "Stock Movements");
    }

    private void RebuildSupplierBalances()
    {
        SupplierBalances.Clear();
        var grouped = Orders.GroupBy(o => o.SupplierId);
        foreach (var g in grouped)
        {
            var supplier = Suppliers.FirstOrDefault(s => s.Id == g.Key);
            SupplierBalances.Add(new SupplierBalanceDto
            {
                SupplierId = g.Key,
                SupplierName = supplier?.Name ?? g.First().SupplierName,
                Phone = supplier?.Phone ?? "",
                TotalOrders = g.Count(),
                TotalAmount = g.Sum(o => o.TotalAmount),
                TotalDiscount = g.Sum(o => o.Discount),
                TotalPaid = g.Sum(o => o.AmountPaid)
            });
        }
    }

    [RelayCommand]
    private void ShowTab(string tab)
    {
        SelectedTab = tab switch
        {
            "neworder" => 0,
            "history" => 1,
            "movements" => 2,
            "balances" => 3,
            _ => 0
        };
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SelectedProduct == null) return;

        if (!int.TryParse(EditQuantity, out var qty) || qty <= 0)
        {
            CustomMessageBox.Show(Lang.IsArabic ? "أدخل كمية صحيحة" : "Enter a valid quantity",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        if (!decimal.TryParse(EditUnitCost, out var cost) || cost <= 0)
        {
            CustomMessageBox.Show(Lang.FeeRequired, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        var existing = PoItems.FirstOrDefault(i => i.ProductId == SelectedProduct.Id);
        if (existing != null)
        {
            existing.Quantity += qty;
            existing.UnitCost = cost;
            var idx = PoItems.IndexOf(existing);
            PoItems.RemoveAt(idx);
            PoItems.Insert(idx, existing);
        }
        else
        {
            PoItems.Add(new PurchaseOrderItemDto
            {
                ProductId = SelectedProduct.Id,
                ProductName = SelectedProduct.Name,
                Quantity = qty,
                UnitCost = cost
            });
        }

        EditQuantity = "";
        EditUnitCost = "";
        UpdatePoTotal();
    }

    [RelayCommand]
    private void RemoveItem(PurchaseOrderItemDto? item)
    {
        if (item == null) return;
        PoItems.Remove(item);
        UpdatePoTotal();
    }

    [RelayCommand]
    private void EditOrder(PurchaseOrderDto? order)
    {
        if (order == null) return;
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
        PoItems.Clear();
        foreach (var it in order.Items)
            PoItems.Add(new PurchaseOrderItemDto
            {
                ProductId = it.ProductId,
                ProductName = it.ProductName,
                Quantity = it.Quantity,
                UnitCost = it.UnitCost
            });
        EditDiscount = order.Discount > 0 ? order.Discount.ToString("0.##") : "";
        EditAmountPaid = order.AmountPaid > 0 ? order.AmountPaid.ToString("0.##") : "";
        EditNotes = order.Notes ?? "";
        _editingOrderId = order.Id;
        IsEditingOrder = true;
        UpdatePoTotal();
        SelectedTab = 0; // jump to the builder tab, now in edit mode
    }

    [RelayCommand]
    private void CancelEditOrder() => ClearBuilder();

    [RelayCommand]
    private async Task DeleteOrderAsync(PurchaseOrderDto? order)
    {
        if (order == null) return;

        if (!CustomMessageBox.Confirm(
            Lang.IsArabic
                ? $"حذف طلب الشراء #{order.Id}؟ سيتم إرجاع المخزون وتعديل الأرصدة."
                : $"Delete purchase order #{order.Id}? Stock will be reversed and balances updated.",
            Lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            await _inventoryService.DeletePurchaseOrderAsync(order.Id);
            if (_editingOrderId == order.Id) ClearBuilder();
            SelectedOrder = null;
            await LoadAsync();
            CustomMessageBox.Show(
                Lang.IsArabic ? "تم حذف طلب الشراء وتحديث الأرصدة" : "Purchase order deleted and balances updated",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    private void ClearBuilder()
    {
        PoItems.Clear();
        EditNotes = "";
        EditDiscount = "";
        EditAmountPaid = "";
        EditQuantity = "";
        EditUnitCost = "";
        SelectedSupplier = null;
        SelectedProduct = null;
        PoTotal = 0;
        _editingOrderId = 0;
        IsEditingOrder = false;
    }

    [RelayCommand]
    private async Task SubmitOrderAsync()
    {
        if (SelectedSupplier == null)
        {
            CustomMessageBox.Show(Lang.IsArabic ? "اختر المورد" : "Select a supplier",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        if (PoItems.Count == 0)
        {
            CustomMessageBox.Show(Lang.IsArabic ? "أضف منتجات للطلب" : "Add items to the order",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        decimal.TryParse(EditDiscount, out var discount);
        decimal.TryParse(EditAmountPaid, out var amountPaid);

        try
        {
            if (IsEditingOrder)
            {
                await _inventoryService.UpdatePurchaseOrderAsync(new PurchaseOrderDto
                {
                    Id = _editingOrderId,
                    SupplierId = SelectedSupplier.Id,
                    Discount = discount,
                    AmountPaid = amountPaid,
                    Notes = EditNotes.Trim(),
                    Items = PoItems.ToList()
                });

                ClearBuilder();
                await LoadAsync();
                SelectedTab = 1; // show the updated order in history
                CustomMessageBox.Show(
                    Lang.IsArabic ? "تم تحديث طلب الشراء والمخزون" : "Purchase order and stock updated",
                    Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
            }
            else
            {
                await _inventoryService.CreatePurchaseOrderAsync(new PurchaseOrderDto
                {
                    SupplierId = SelectedSupplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Discount = discount,
                    AmountPaid = amountPaid,
                    Notes = EditNotes.Trim(),
                    Items = PoItems.ToList()
                });

                ClearBuilder();
                await LoadAsync();
                SelectedTab = 1; // show the new order in history
                CustomMessageBox.Show(
                    Lang.IsArabic ? "تم إنشاء طلب الشراء وتحديث المخزون" : "Purchase order created and stock updated",
                    Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task PayOrderAsync()
    {
        if (SelectedOrder == null) return;

        if (!decimal.TryParse(PayAmount, out var amount) || amount <= 0)
        {
            CustomMessageBox.Show(Lang.IsArabic ? "أدخل مبلغ صحيح" : "Enter a valid amount",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        try
        {
            await _inventoryService.PayPurchaseOrderAsync(SelectedOrder.Id, amount);
            PayAmount = "";
            await LoadAsync();

            CustomMessageBox.Show(
                Lang.IsArabic ? "تم تسجيل الدفعة بنجاح" : "Payment recorded successfully",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void ViewSupplierOrders(SupplierBalanceDto? balance)
    {
        if (balance == null) return;
        SelectedTab = 1; // jump to the order-history tab
        var firstUnpaid = Orders.FirstOrDefault(o => o.SupplierId == balance.SupplierId && o.RemainingAmount > 0);
        SelectedOrder = firstUnpaid ?? Orders.FirstOrDefault(o => o.SupplierId == balance.SupplierId);
    }

    private void UpdatePoTotal()
    {
        PoTotal = PoItems.Sum(i => i.TotalCost);
    }
}
