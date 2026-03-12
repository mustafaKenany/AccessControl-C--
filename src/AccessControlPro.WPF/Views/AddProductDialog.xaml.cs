using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class AddProductDialog : Window
{
    public ProductDto Product { get; private set; } = new();

    private readonly ILookupService? _lookupService;
    private List<LookupItem> _items = new();

    private static readonly string[] FallbackEn = { "Drinks", "Supplements", "Gear", "Accessories", "Other" };
    private static readonly string[] FallbackAr = { "مشروبات", "مكملات", "ملابس رياضية", "إكسسوارات", "أخرى" };

    public AddProductDialog(ILookupService? lookupService = null)
    {
        _lookupService = lookupService;
        InitializeComponent();
    }

    private async void CategoryCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var lang = LanguageManager.Instance;

        if (_lookupService != null)
        {
            try { _items = await _lookupService.GetByCategoryAsync("ProductCategory"); }
            catch { }
        }

        if (_items.Count > 0)
        {
            CategoryCombo.ItemsSource = _items.Select(i =>
                lang.IsArabic && !string.IsNullOrWhiteSpace(i.NameAr) ? i.NameAr : i.Name).ToList();
        }
        else
        {
            CategoryCombo.ItemsSource = lang.IsArabic ? FallbackAr : FallbackEn;
        }

        if (CategoryCombo.Items.Count > 0)
            CategoryCombo.SelectedIndex = 0;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEnBox.Text))
        {
            CustomMessageBox.Show(LanguageManager.Instance.PlayerNameRequired,
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
            return;
        }

        if (!decimal.TryParse(PriceBox.Text, out var price) || price <= 0)
        {
            CustomMessageBox.Show(LanguageManager.Instance.FeeRequired,
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
            return;
        }

        int.TryParse(StockBox.Text, out var stock);

        // Store EN name for DB consistency
        string category;
        if (_items.Count > 0 && CategoryCombo.SelectedIndex >= 0 && CategoryCombo.SelectedIndex < _items.Count)
            category = _items[CategoryCombo.SelectedIndex].Name;
        else
            category = CategoryCombo.SelectedItem?.ToString() ?? "Other";

        Product = new ProductDto
        {
            Name = NameEnBox.Text.Trim(),
            NameAr = NameArBox.Text.Trim(),
            Price = price,
            Category = category,
            Stock = stock,
            IsActive = true
        };

        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
