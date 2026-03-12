using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class PosViewModel : ObservableObject
{
    private readonly IPosService _posService;
    private readonly IEmployeeService _employeeService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _playerSearch;
    [ObservableProperty] private EmployeeDto? _selectedPlayer;
    [ObservableProperty] private decimal _playerCardBalance;
    [ObservableProperty] private decimal _cartTotal;
    [ObservableProperty] private string _playerInfoText = string.Empty;
    [ObservableProperty] private bool _hasSelectedPlayer;
    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private int _todaySalesCount;
    [ObservableProperty] private decimal _todaySalesTotal;

    private List<ProductDto> _allProducts = new();

    public ObservableCollection<ProductDto> FilteredProducts { get; } = new();
    public ObservableCollection<CartItemDto> CartItems { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    private bool _isInitialized;

    public PosViewModel(IPosService posService, IEmployeeService employeeService)
    {
        _posService = posService;
        _employeeService = employeeService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadProductsAsync();
        await LoadTodaySalesAsync();
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
        UpdateCartTotal();
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
            var saleTotal = CartTotal;
            var success = await _posService.SellAsync(
                saleItems, method, SelectedPlayer?.Id);

            if (!success)
            {
                CustomMessageBox.Show(Lang.PosInsufficientBalance, Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            CartItems.Clear();
            UpdateCartTotal();
            await LoadProductsAsync();
            await LoadTodaySalesAsync();

            if (SelectedPlayer != null)
            {
                PlayerCardBalance = await _posService.GetCardBalanceAsync(SelectedPlayer.Id);
                PlayerInfoText = $"{SelectedPlayer.FullNameEn}  |  {Lang.PosCardBalance}: {PlayerCardBalance:N0}";
            }

            // Show receipt dialog
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
        CartTotal = CartItems.Sum(c => c.Total);
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
}
