using System.Collections.ObjectModel;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AccessControlPro.WPF.ViewModels;

public partial class PosViewModel : ObservableObject
{
    private readonly IPosService _posService;
    private readonly IEmployeeService _employeeService;
    private readonly IAppSettingsService _settingsService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _playerSearch;
    [ObservableProperty] private EmployeeDto? _selectedPlayer;
    [ObservableProperty] private decimal _playerCardBalance;
    [ObservableProperty] private decimal _cartTotal;
    [ObservableProperty] private decimal _cartSubtotal;
    [ObservableProperty] private string _playerInfoText = string.Empty;
    [ObservableProperty] private bool _hasSelectedPlayer;
    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private int _todaySalesCount;
    [ObservableProperty] private decimal _todaySalesTotal;

    // Discount
    [ObservableProperty] private decimal _orderDiscountAmount;
    [ObservableProperty] private string _orderDiscountReason = string.Empty;

    // Shift
    [ObservableProperty] private bool _hasOpenShift;
    [ObservableProperty] private string _shiftStatusText = string.Empty;

    // Daily Summary
    [ObservableProperty] private DailySummaryDto? _dailySummary;
    [ObservableProperty] private bool _showDailySummary;

    // Last sale data for receipt
    private List<CartItemDto>? _lastSaleItems;
    private decimal _lastSaleTotal;
    private PaymentMethod _lastSaleMethod;
    private decimal _lastSaleDiscount;
    [ObservableProperty] private bool _hasLastSale;

    private List<ProductDto> _allProducts = new();
    private string _gymName = string.Empty;

    public ObservableCollection<ProductDto> FilteredProducts { get; } = new();
    public ObservableCollection<CartItemDto> CartItems { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    private bool _isInitialized;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public PosViewModel(IPosService posService, IEmployeeService employeeService, IAppSettingsService settingsService, CurrentUserService currentUser)
    {
        _posService = posService;
        _employeeService = employeeService;
        _settingsService = settingsService;
        _currentUser = currentUser;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadProductsAsync();
        await LoadTodaySalesAsync();
        await LoadShiftStatusAsync();
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            _gymName = !string.IsNullOrEmpty(settings.GymName) ? settings.GymName : "GYM";
        }
        catch { _gymName = "GYM"; }
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        IsLoading = true;
        try
        {
            var products = await _posService.GetAllProductsAsync();
            _allProducts = products.ToList();
            RebuildCategories();
            ApplyCategoryFilter();
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

    private void RebuildCategories()
    {
        var cats = _allProducts
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories.Clear();
        Categories.Add("All");
        foreach (var c in cats)
            Categories.Add(c);

        if (!Categories.Contains(SelectedCategory))
            SelectedCategory = "All";
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyCategoryFilter();
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        SelectedCategory = category ?? "All";
    }

    private void ApplyCategoryFilter()
    {
        FilteredProducts.Clear();
        var source = SelectedCategory == "All"
            ? _allProducts
            : _allProducts.Where(p => p.Category == SelectedCategory);

        foreach (var p in source)
            FilteredProducts.Add(p);
    }

    [RelayCommand]
    private async Task ScanBarcodeAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

        try
        {
            var product = await _posService.GetByBarcodeAsync(BarcodeInput.Trim());
            if (product != null)
            {
                AddToCart(product);
            }
            else
            {
                CustomMessageBox.Show(Lang.PosProductNotFound, Lang.ValidationTitle, MsgType.Info);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            BarcodeInput = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SearchPlayerAsync()
    {
        if (string.IsNullOrWhiteSpace(PlayerSearch)) return;

        try
        {
            var (items, _) = await _employeeService.GetPagedAsync(1, 10, PlayerSearch);
            var player = items.FirstOrDefault();
            if (player != null)
            {
                SelectedPlayer = player;
                PlayerCardBalance = player.CardBalance;
                HasSelectedPlayer = true;
                PlayerInfoText = $"{player.FullNameEn}  |  {Lang.PosCardBalance}: {player.CardBalance:N0}";
            }
            else
            {
                SelectedPlayer = null;
                PlayerCardBalance = 0;
                HasSelectedPlayer = false;
                PlayerInfoText = string.Empty;
                CustomMessageBox.Show(Lang.PosPlayerNotFound, Lang.ValidationTitle, MsgType.Info);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void AddToCart(ProductDto? product)
    {
        if (product == null || product.Stock <= 0) return;

        var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            if (existing.Quantity >= product.Stock) return;
            existing.Quantity++;
            var index = CartItems.IndexOf(existing);
            CartItems.RemoveAt(index);
            CartItems.Insert(index, existing);
        }
        else
        {
            var name = Lang.IsArabic ? product.NameAr : product.Name;
            CartItems.Add(new CartItemDto
            {
                ProductId = product.Id,
                ProductName = string.IsNullOrEmpty(name) ? product.Name : name,
                Price = product.Price,
                Quantity = 1
            });
        }
        UpdateCartTotal();
    }

    [RelayCommand]
    private void IncreaseQuantity(CartItemDto? item)
    {
        if (item == null) return;
        var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
        if (product != null && item.Quantity >= product.Stock) return;
        item.Quantity++;
        RefreshCartItem(item);
    }

    [RelayCommand]
    private void DecreaseQuantity(CartItemDto? item)
    {
        if (item == null) return;
        if (item.Quantity <= 1)
        {
            CartItems.Remove(item);
        }
        else
        {
            item.Quantity--;
            RefreshCartItem(item);
        }
        UpdateCartTotal();
    }

    private void RefreshCartItem(CartItemDto item)
    {
        var index = CartItems.IndexOf(item);
        if (index >= 0)
        {
            CartItems.RemoveAt(index);
            CartItems.Insert(index, item);
        }
        UpdateCartTotal();
    }

    [RelayCommand]
    private void RemoveFromCart(CartItemDto? item)
    {
        if (item == null) return;
        CartItems.Remove(item);
        UpdateCartTotal();
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        OrderDiscountAmount = 0;
        OrderDiscountReason = string.Empty;
        UpdateCartTotal();
    }

    [RelayCommand]
    private void ApplyItemDiscount(CartItemDto? item)
    {
        if (item == null) return;

        if (!_currentUser.HasPermission(AppPermission.POSApplyDiscount))
        {
            StatusMessage = "No permission to apply discounts";
            return;
        }

        var dialog = new Views.DiscountDialog(item.Price * item.Quantity);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            item.DiscountAmount = dialog.DiscountValue;
            RefreshCartItem(item);
        }
    }

    [RelayCommand]
    private async Task CheckoutCashAsync()
    {
        await CheckoutAsync(PaymentMethod.Cash);
    }

    [RelayCommand]
    private async Task CheckoutCardAsync()
    {
        if (SelectedPlayer == null)
        {
            CustomMessageBox.Show(Lang.PosSearchPlayerFirst, Lang.ValidationTitle, MsgType.Warning);
            return;
        }
        await CheckoutAsync(PaymentMethod.CardBalance);
    }

    private async Task CheckoutAsync(PaymentMethod method)
    {
        if (CartItems.Count == 0) return;

        try
        {
            var saleItems = CartItems.ToList();
            var subtotal = saleItems.Sum(i => i.Price * i.Quantity);
            var itemDiscounts = saleItems.Sum(i => i.DiscountAmount);
            var totalDiscount = OrderDiscountAmount + itemDiscounts;
            var saleTotal = subtotal - totalDiscount;
            if (saleTotal < 0) saleTotal = 0;

            var success = await _posService.SellAsync(
                saleItems, method, SelectedPlayer?.Id,
                OrderDiscountAmount, OrderDiscountReason);

            if (!success)
            {
                CustomMessageBox.Show(Lang.PosInsufficientBalance, Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            // Store last sale for receipt
            _lastSaleItems = saleItems;
            _lastSaleTotal = saleTotal;
            _lastSaleMethod = method;
            _lastSaleDiscount = totalDiscount;
            HasLastSale = true;

            CartItems.Clear();
            OrderDiscountAmount = 0;
            OrderDiscountReason = string.Empty;
            UpdateCartTotal();
            await LoadProductsAsync();
            await LoadTodaySalesAsync();

            if (SelectedPlayer != null)
            {
                PlayerCardBalance = await _posService.GetCardBalanceAsync(SelectedPlayer.Id);
                PlayerInfoText = $"{SelectedPlayer.FullNameEn}  |  {Lang.PosCardBalance}: {PlayerCardBalance:N0}";
            }

            // Show receipt dialog with print option
            var receiptDialog = new Views.ReceiptDialog(saleItems, saleTotal, method,
                SelectedPlayer?.FullNameEn);
            receiptDialog.Owner = System.Windows.Application.Current.MainWindow;
            receiptDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task TopUpCardAsync()
    {
        if (SelectedPlayer == null)
        {
            CustomMessageBox.Show(Lang.PosSearchPlayerFirst, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        var dialog = new Views.TopUpDialog();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _posService.TopUpCardAsync(SelectedPlayer.Id, dialog.Amount);
            PlayerCardBalance = await _posService.GetCardBalanceAsync(SelectedPlayer.Id);
            PlayerInfoText = $"{SelectedPlayer.FullNameEn}  |  {Lang.PosCardBalance}: {PlayerCardBalance:N0}";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    private void UpdateCartTotal()
    {
        CartSubtotal = CartItems.Sum(c => c.Price * c.Quantity);
        var itemDiscounts = CartItems.Sum(c => c.DiscountAmount);
        CartTotal = CartSubtotal - itemDiscounts - OrderDiscountAmount;
        if (CartTotal < 0) CartTotal = 0;
    }

    partial void OnOrderDiscountAmountChanged(decimal value)
    {
        UpdateCartTotal();
    }

    private async Task LoadTodaySalesAsync()
    {
        try
        {
            var (count, total) = await _posService.GetTodaySalesAsync();
            TodaySalesCount = count;
            TodaySalesTotal = total;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    // ── Receipt Printing ──

    [RelayCommand]
    private void PrintLastReceipt()
    {
        if (_lastSaleItems == null || _lastSaleItems.Count == 0) return;

        if (!_currentUser.HasPermission(AppPermission.POSPrintReceipt))
        {
            StatusMessage = "No permission to print receipts";
            return;
        }

        PrintReceipt(_lastSaleItems, _lastSaleTotal, _lastSaleMethod, _lastSaleDiscount,
            SelectedPlayer?.FullNameEn);
    }

    public void PrintReceipt(List<CartItemDto> items, decimal total, PaymentMethod method,
        decimal discount = 0, string? playerName = null)
    {
        if (!_currentUser.HasPermission(AppPermission.POSPrintReceipt))
        {
            StatusMessage = "No permission to print receipts";
            return;
        }

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = CreateReceiptDocument(items, total, method, discount, playerName);
        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        printDialog.PrintDocument(paginator, "POS Receipt");
    }

    private FlowDocument CreateReceiptDocument(List<CartItemDto> items, decimal total,
        PaymentMethod method, decimal discount, string? playerName)
    {
        var doc = new FlowDocument
        {
            PageWidth = 280,
            ColumnWidth = 280,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Consolas")
        };

        // Header
        doc.Blocks.Add(new Paragraph(new Run(_gymName))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        doc.Blocks.Add(new Paragraph(new Run(DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss")))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (!string.IsNullOrEmpty(playerName))
        {
            doc.Blocks.Add(new Paragraph(new Run(playerName))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32)))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 4)
        });

        // Items
        foreach (var item in items)
        {
            var line = $"{item.ProductName}";
            doc.Blocks.Add(new Paragraph(new Run(line))
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var detail = $"  {item.Quantity} x {item.Price:N0} = {item.Price * item.Quantity:N0}";
            if (item.DiscountAmount > 0)
                detail += $" (-{item.DiscountAmount:N0})";
            doc.Blocks.Add(new Paragraph(new Run(detail))
            {
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32)))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Margin = new Thickness(0, 4, 0, 4)
        });

        // Discount
        if (discount > 0)
        {
            doc.Blocks.Add(new Paragraph(new Run($"Discount: -{discount:N0}"))
            {
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        // Total
        doc.Blocks.Add(new Paragraph(new Run($"TOTAL: {total:N0}"))
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4)
        });

        // Payment method
        var methodText = method == PaymentMethod.Cash ? "CASH" : "CARD BALANCE";
        doc.Blocks.Add(new Paragraph(new Run($"Paid: {methodText}"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 8)
        });

        // Footer
        doc.Blocks.Add(new Paragraph(new Run("Thank you!"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            FontWeight = FontWeights.Bold
        });

        return doc;
    }

    [RelayCommand]
    private void SaveLastReceiptAsText()
    {
        if (_lastSaleItems == null || _lastSaleItems.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true) return;

        var lines = new List<string>
        {
            _gymName,
            DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss"),
            new string('-', 32)
        };

        foreach (var item in _lastSaleItems)
        {
            lines.Add($"{item.ProductName}");
            var detail = $"  {item.Quantity} x {item.Price:N0} = {item.Price * item.Quantity:N0}";
            if (item.DiscountAmount > 0)
                detail += $" (-{item.DiscountAmount:N0})";
            lines.Add(detail);
        }

        lines.Add(new string('-', 32));
        if (_lastSaleDiscount > 0)
            lines.Add($"Discount: -{_lastSaleDiscount:N0}");
        lines.Add($"TOTAL: {_lastSaleTotal:N0}");
        lines.Add($"Paid: {(_lastSaleMethod == PaymentMethod.Cash ? "CASH" : "CARD BALANCE")}");
        lines.Add("");
        lines.Add("Thank you!");

        File.WriteAllLines(dialog.FileName, lines);
    }

    // ── Daily Summary ──

    [RelayCommand]
    private async Task LoadDailySummaryAsync()
    {
        if (!_currentUser.HasPermission(AppPermission.POSViewSummary))
        {
            StatusMessage = "No permission to view daily summary";
            return;
        }

        try
        {
            DailySummary = await _posService.GetDailySummaryAsync(DateTime.Today);
            ShowDailySummary = true;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void HideDailySummary()
    {
        ShowDailySummary = false;
    }

    [RelayCommand]
    private void PrintDailySummary()
    {
        if (DailySummary == null) return;

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PageWidth = 280,
            ColumnWidth = 280,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Consolas")
        };

        doc.Blocks.Add(new Paragraph(new Run($"{_gymName} - {Lang.PosDailySummary}"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        doc.Blocks.Add(new Paragraph(new Run(DailySummary.Date.ToString("yyyy-MM-dd")))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 8)
        });

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32))) { TextAlignment = TextAlignment.Center, FontSize = 10 });

        var lines = new[]
        {
            $"{Lang.PosTotalTransactions}: {DailySummary.TotalTransactions}",
            $"{Lang.PosTotalItemsSold}: {DailySummary.TotalItemsSold}",
            $"{Lang.PosTodaySales}: {DailySummary.TotalSales:N0}",
            $"{Lang.PosCashSales}: {DailySummary.TotalCashSales:N0}",
            $"{Lang.PosCardSales}: {DailySummary.TotalCardSales:N0}",
            $"{Lang.PosTotalDiscounts}: {DailySummary.TotalDiscounts:N0}"
        };

        foreach (var line in lines)
        {
            doc.Blocks.Add(new Paragraph(new Run(line)) { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        }

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        printDialog.PrintDocument(paginator, "Daily Summary");
    }

    // ── Shift Management ──

    private async Task LoadShiftStatusAsync()
    {
        try
        {
            var shift = await _posService.GetOpenShiftAsync();
            HasOpenShift = shift != null;
            if (shift != null)
            {
                ShiftStatusText = $"{Lang.PosShiftOpen} - {shift.OpenedBy} ({shift.OpenedAt.ToLocalTime():HH:mm})";
            }
            else
            {
                ShiftStatusText = string.Empty;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (!_currentUser.HasPermission(AppPermission.POSManageShift))
        {
            StatusMessage = "No permission to manage shifts";
            return;
        }

        var dialog = new Views.AmountInputDialog(Lang.PosOpeningCash, Lang.PosEnterAmount);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        try
        {
            var shift = await _posService.OpenShiftAsync(dialog.Amount);
            HasOpenShift = true;
            ShiftStatusText = $"{Lang.PosShiftOpen} - {shift.OpenedBy} ({shift.OpenedAt.ToLocalTime():HH:mm})";
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (!_currentUser.HasPermission(AppPermission.POSManageShift))
        {
            StatusMessage = "No permission to manage shifts";
            return;
        }

        var dialog = new Views.AmountInputDialog(Lang.PosClosingCash, Lang.PosEnterAmount);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        try
        {
            var shift = await _posService.CloseShiftAsync(dialog.Amount);
            HasOpenShift = false;
            ShiftStatusText = string.Empty;

            // Show shift report
            var expected = shift.OpeningCash + shift.TotalCashSales;
            var reportMsg = $"{Lang.PosShiftReport}\n\n" +
                $"{Lang.PosOpeningCash}: {shift.OpeningCash:N0}\n" +
                $"{Lang.PosTodaySales}: {shift.TotalSales:N0}\n" +
                $"{Lang.PosCashSales}: {shift.TotalCashSales:N0}\n" +
                $"{Lang.PosCardSales}: {shift.TotalCardSales:N0}\n" +
                $"{Lang.PosExpectedCash}: {expected:N0}\n" +
                $"{Lang.PosClosingCash}: {shift.ClosingCash:N0}\n" +
                $"{Lang.PosVariance}: {shift.Variance:N0}";

            CustomMessageBox.Show(reportMsg, Lang.PosShiftClosed, MsgType.Info);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }
}
