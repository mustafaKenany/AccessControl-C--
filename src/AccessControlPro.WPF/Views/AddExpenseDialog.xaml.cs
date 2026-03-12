using System.Windows;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class AddExpenseDialog : Window
{
    public string SelectedCategory { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;

    private readonly ILookupService? _lookupService;
    private List<LookupItem> _items = new();

    private static readonly string[] FallbackEn = { "Rent", "Electricity", "Water", "Salaries", "Equipment", "Maintenance", "Supplies", "Marketing", "Other" };
    private static readonly string[] FallbackAr = { "إيجار", "كهرباء", "ماء", "رواتب", "معدات", "صيانة", "مستلزمات", "تسويق", "أخرى" };

    public AddExpenseDialog(ILookupService? lookupService = null)
    {
        _lookupService = lookupService;
        InitializeComponent();
    }

    private async void CategoryCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var lang = LanguageManager.Instance;

        if (_lookupService != null)
        {
            try { _items = await _lookupService.GetByCategoryAsync("ExpenseCategory"); }
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
        if (CategoryCombo.SelectedItem == null)
        {
            CustomMessageBox.Show(LanguageManager.Instance.FinCategory,
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
            return;
        }

        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            CustomMessageBox.Show(LanguageManager.Instance.FeeRequired,
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
            return;
        }

        // Store EN name for DB consistency
        if (_items.Count > 0 && CategoryCombo.SelectedIndex >= 0 && CategoryCombo.SelectedIndex < _items.Count)
            SelectedCategory = _items[CategoryCombo.SelectedIndex].Name;
        else
            SelectedCategory = CategoryCombo.SelectedItem?.ToString() ?? "Other";

        Amount = amount;
        Description = DescriptionBox.Text;
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
