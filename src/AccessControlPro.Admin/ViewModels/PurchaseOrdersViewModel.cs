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
            Orders.Clear();
            foreach (var o in orders.OrderByDescending(o => o.OrderDate))
                Orders.Add(o);

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
            "movements" => 1,
            "balances" => 2,
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
            var dto = new PurchaseOrderDto
            {
                SupplierId = SelectedSupplier.Id,
                OrderDate = DateTime.UtcNow,
                Discount = discount,
                AmountPaid = amountPaid,
                Notes = EditNotes.Trim(),
                Items = PoItems.ToList()
            };

            await _inventoryService.CreatePurchaseOrderAsync(dto);

            PoItems.Clear();
            EditNotes = "";
            EditDiscount = "";
            EditAmountPaid = "";
            SelectedSupplier = null;
            PoTotal = 0;
            await LoadAsync();

            CustomMessageBox.Show(
                Lang.IsArabic ? "تم إنشاء طلب الشراء وتحديث المخزون" : "Purchase order created and stock updated",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
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
        SelectedTab = 0;
        var firstUnpaid = Orders.FirstOrDefault(o => o.SupplierId == balance.SupplierId && o.RemainingAmount > 0);
        SelectedOrder = firstUnpaid ?? Orders.FirstOrDefault(o => o.SupplierId == balance.SupplierId);
    }

    private void UpdatePoTotal()
    {
        PoTotal = PoItems.Sum(i => i.TotalCost);
    }
}
