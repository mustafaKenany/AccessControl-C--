using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly ILookupService _lookupService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private bool _isEditing;

    // Edit fields
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editNameAr = "";
    [ObservableProperty] private string _editBarcode = "";
    [ObservableProperty] private string _editPrice = "";
    [ObservableProperty] private string _editCategory = "";

    public ObservableCollection<ProductDto> Products { get; } = new();

    public ProductsViewModel(IInventoryService inventoryService, ILookupService lookupService)
    {
        _inventoryService = inventoryService;
        _lookupService = lookupService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var products = await _inventoryService.GetAllProductsAsync();
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        if (value != null)
        {
            EditName = value.Name;
            EditNameAr = value.NameAr;
            EditBarcode = value.Barcode;
            EditPrice = value.Price.ToString("0");
            EditCategory = value.Category;
            IsEditing = true;
        }
        else
        {
            ClearFields();
        }
    }

    private void ClearFields()
    {
        EditName = "";
        EditNameAr = "";
        EditBarcode = "";
        EditPrice = "";
        EditCategory = "";
        SelectedProduct = null;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            CustomMessageBox.Show(Lang.PlayerNameRequired, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        if (!decimal.TryParse(EditPrice, out var price) || price <= 0)
        {
            CustomMessageBox.Show(Lang.FeeRequired, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        try
        {
            if (IsEditing && SelectedProduct != null)
            {
                SelectedProduct.Name = EditName.Trim();
                SelectedProduct.NameAr = EditNameAr.Trim();
                SelectedProduct.Barcode = EditBarcode.Trim();
                SelectedProduct.Price = price;
                SelectedProduct.Category = EditCategory.Trim();
                await _inventoryService.UpdateProductAsync(SelectedProduct);
            }
            else
            {
                await _inventoryService.AddProductAsync(new ProductDto
                {
                    Name = EditName.Trim(),
                    NameAr = EditNameAr.Trim(),
                    Barcode = EditBarcode.Trim(),
                    Price = price,
                    Category = EditCategory.Trim(),
                    Stock = 0,
                    IsActive = true
                });
            }

            ClearFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProduct == null) return;

        if (!CustomMessageBox.Confirm(
            Lang.IsArabic ? $"هل تريد حذف \"{SelectedProduct.NameAr}\"?" : $"Delete \"{SelectedProduct.Name}\"?",
            Lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            await _inventoryService.DeleteProductAsync(SelectedProduct.Id);
            ClearFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearFields();
    }
}
