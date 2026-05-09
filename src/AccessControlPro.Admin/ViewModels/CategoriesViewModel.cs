using System.Collections.ObjectModel;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly ILookupService _lookupService;

    public LanguageManager Lang => LanguageManager.Instance;

    // Category types the admin can manage
    public class CategoryType
    {
        public string Key { get; set; } = "";
        public string DisplayEn { get; set; } = "";
        public string DisplayAr { get; set; } = "";
        public bool ShowRate { get; set; }
        public string Display => LanguageManager.Instance.IsArabic ? DisplayAr : DisplayEn;
        public override string ToString() => Display;
    }

    public List<CategoryType> CategoryTypes { get; } =
    [
        new() { Key = "IncomeCategory", DisplayEn = "Income Categories", DisplayAr = "فئات الدخل" },
        new() { Key = "ExpenseCategory", DisplayEn = "Expense Categories", DisplayAr = "فئات المصروفات" },
        new() { Key = "ProductCategory", DisplayEn = "Product Categories", DisplayAr = "فئات المنتجات" },
        // Subscription Plans intentionally not exposed here — they live on the dedicated
        // "Subscription Plans" admin page (SubscriptionPlansViewModel) which writes to the
        // SubscriptionPlans table that the main app's player dialogs read from. The old
        // LookupItem-based path was abandoned 2026-05-09 to fix the disconnect where plans
        // added in this Categories page never appeared in Add Player.
    ];

    [ObservableProperty] private CategoryType? _selectedCategoryType;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showRate;

    // Edit fields
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editNameAr = "";
    [ObservableProperty] private string _editRate = "";
    [ObservableProperty] private LookupItem? _selectedItem;
    [ObservableProperty] private bool _isEditing;

    public ObservableCollection<LookupItem> Items { get; } = new();

    public CategoriesViewModel(ILookupService lookupService)
    {
        _lookupService = lookupService;
        _selectedCategoryType = CategoryTypes[0];
    }

    async partial void OnSelectedCategoryTypeChanged(CategoryType? value)
    {
        ShowRate = value?.ShowRate ?? false;
        ClearEditFields();
        await LoadItemsAsync();
    }

    public async Task LoadItemsAsync()
    {
        if (SelectedCategoryType == null) return;

        IsLoading = true;
        try
        {
            var items = await _lookupService.GetByCategoryAsync(SelectedCategoryType.Key);
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
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

    partial void OnSelectedItemChanged(LookupItem? value)
    {
        if (value != null)
        {
            EditName = value.Name;
            EditNameAr = value.NameAr;
            EditRate = value.NumericValue > 0 ? value.NumericValue.ToString("0") : "";
            IsEditing = true;
        }
        else
        {
            ClearEditFields();
        }
    }

    private void ClearEditFields()
    {
        EditName = "";
        EditNameAr = "";
        EditRate = "";
        SelectedItem = null;
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

        if (SelectedCategoryType == null) return;

        decimal rate = 0;
        if (ShowRate && !string.IsNullOrWhiteSpace(EditRate))
            decimal.TryParse(EditRate, out rate);

        try
        {
            if (IsEditing && SelectedItem != null)
            {
                // Update existing
                SelectedItem.Name = EditName.Trim();
                SelectedItem.NameAr = EditNameAr.Trim();
                SelectedItem.NumericValue = rate;
                await _lookupService.UpdateAsync(SelectedItem);
            }
            else
            {
                // Add new
                var maxOrder = Items.Count > 0 ? Items.Max(x => x.SortOrder) : 0;
                var newItem = new LookupItem
                {
                    Category = SelectedCategoryType.Key,
                    Name = EditName.Trim(),
                    NameAr = EditNameAr.Trim(),
                    NumericValue = rate,
                    SortOrder = maxOrder + 1,
                    IsActive = true
                };
                await _lookupService.AddAsync(newItem);
            }

            ClearEditFields();
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) return;

        var lang = Lang;
        if (!CustomMessageBox.Confirm(
            lang.IsArabic ? $"هل تريد حذف \"{SelectedItem.NameAr}\"?" : $"Delete \"{SelectedItem.Name}\"?",
            lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            await _lookupService.DeleteAsync(SelectedItem.Id);
            ClearEditFields();
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearEditFields();
    }
}
