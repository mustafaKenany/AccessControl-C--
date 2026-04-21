using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class CashFlowViewModel : ObservableObject
{
    private readonly ICashFlowService _cashFlowService;
    private readonly ILookupService _lookupService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    // Permission-based action visibility
    public bool CanManage => _currentUser.HasPermission(AppPermission.CashFlowManage);

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _selectedFilterIndex;  // 0=All, 1=Income, 2=Expense
    [ObservableProperty] private int _selectedPeriodIndex;  // 0=All, 1=Today, ...
    [ObservableProperty] private string _paginationText = "1 / 1";
    [ObservableProperty] private string _dateRangeText = "";

    private const int PageSize = 20;

    // Category item: display name + EN key for DB query
    public class CategoryItem
    {
        public string Display { get; }
        public string? EnKey { get; }
        public CategoryItem(string display, string? enKey) { Display = display; EnKey = enKey; }
        public override string ToString() => Display;
    }

    // Loaded from DB (LookupItems table)
    private List<LookupItem> _incomeCategories = new();
    private List<LookupItem> _expenseCategories = new();

    public ObservableCollection<CategoryItem> Categories { get; } = new();

    private CategoryItem? _selectedCategory;
    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    private bool _isInitialized;

    public ObservableCollection<TransactionDto> Transactions { get; } = new();

    public CashFlowViewModel(ICashFlowService cashFlowService, ILookupService lookupService, CurrentUserService currentUser)
    {
        _cashFlowService = cashFlowService;
        _lookupService = lookupService;
        _currentUser = currentUser;
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        if (_isInitialized)
            RebuildCategories();
    }

    partial void OnSelectedPeriodIndexChanged(int value)
    {
        UpdateDateRangeText();
    }

    private void UpdateDateRangeText()
    {
        var (from, to) = GetDateRange();
        if (from == null && to == null)
        {
            DateRangeText = "";
            return;
        }

        var lang = LanguageManager.Instance;
        var fromStr = from?.ToString("yyyy-MM-dd") ?? "";
        var toStr = to?.ToString("yyyy-MM-dd") ?? (lang.IsArabic ? "\u0627\u0644\u0622\u0646" : "Now");
        DateRangeText = $"\U0001f4c5 {fromStr} \u2192 {toStr}";
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // Load categories from DB
        try
        {
            _incomeCategories = await _lookupService.GetByCategoryAsync("IncomeCategory");
            _expenseCategories = await _lookupService.GetByCategoryAsync("ExpenseCategory");
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }

        RebuildCategories();
        await LoadAsync();
    }

    private void RebuildCategories()
    {
        var previousKey = SelectedCategory?.EnKey;
        Categories.Clear();
        var lang = LanguageManager.Instance;
        Categories.Add(new CategoryItem(lang.FinAll, null)); // "All"

        if (SelectedFilterIndex != 2) // Not Expense-only → show income categories
        {
            foreach (var item in _incomeCategories)
            {
                var display = lang.IsArabic && !string.IsNullOrWhiteSpace(item.NameAr) ? item.NameAr : item.Name;
                Categories.Add(new CategoryItem(display, item.Name));
            }
        }

        if (SelectedFilterIndex != 1) // Not Income-only → show expense categories
        {
            foreach (var item in _expenseCategories)
            {
                var display = lang.IsArabic && !string.IsNullOrWhiteSpace(item.NameAr) ? item.NameAr : item.Name;
                Categories.Add(new CategoryItem(display, item.Name));
            }
        }

        // Try to keep previous selection, fall back to "All"
        SelectedCategory = Categories.FirstOrDefault(c => c.EnKey == previousKey)
                           ?? Categories.FirstOrDefault()!;
    }

    private (DateTime? From, DateTime? To) GetDateRange()
    {
        var today = DateTime.Today;
        return SelectedPeriodIndex switch
        {
            1 => (today, today.AddDays(1).AddTicks(-1)),                                          // Today
            2 => (today.AddDays(-1), today.AddTicks(-1)),                                         // Yesterday
            3 => (today.AddDays(-((int)today.DayOfWeek + 1) % 7), null),                                    // This Week (Saturday start)
            4 => (today.AddDays(-((int)today.DayOfWeek + 1) % 7 - 7), today.AddDays(-((int)today.DayOfWeek + 1) % 7).AddTicks(-1)), // Last Week
            5 => (new DateTime(today.Year, today.Month, 1), null),                                // This Month
            6 => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                  new DateTime(today.Year, today.Month, 1).AddTicks(-1)),                         // Last Month
            7 => (today.AddMonths(-3), null),                                                     // Last 3 Months
            8 => (today.AddMonths(-6), null),                                                     // Last 6 Months
            9 => (new DateTime(today.Year, 1, 1), null),                                          // This Year
            _ => (null, null)                                                                     // All
        };
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            TransactionType? typeFilter = SelectedFilterIndex switch
            {
                1 => TransactionType.Income,
                2 => TransactionType.Expense,
                _ => null
            };

            var (dateFrom, dateTo) = GetDateRange();

            // Get category filter from selected item
            string? category = SelectedCategory?.EnKey;

            var (items, totalCount) = await _cashFlowService.GetPagedAsync(
                CurrentPage, PageSize, typeFilter, dateFrom, dateTo, null, category);

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            PaginationText = $"{CurrentPage} / {TotalPages}";

            Transactions.Clear();
            foreach (var t in items)
                Transactions.Add(t);
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

    [RelayCommand]
    private async Task FilterChangedAsync()
    {
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task AddExpenseAsync()
    {
        var dialog = new Views.AddExpenseDialog(_lookupService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _cashFlowService.AddExpenseAsync(dialog.SelectedCategory, dialog.Amount, dialog.Description);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task AddIncomeAsync()
    {
        var dialog = new Views.AddIncomeDialog(_lookupService);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _cashFlowService.AddIncomeAsync(dialog.SelectedCategory, dialog.Amount, dialog.Description);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }
}
