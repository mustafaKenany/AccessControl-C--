using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class FinanceViewModel : ObservableObject
{
    private readonly IFinanceService _financeService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    // Permission-based action visibility
    public bool CanManage => _currentUser.HasPermission(AppPermission.FinanceManage);

    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _unpaidBalances;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _selectedPeriodIndex; // 0 = All
    [ObservableProperty] private string _dateRangeText = "";

    public ObservableCollection<TransactionDto> RecentTransactions { get; } = new();
    public ObservableCollection<OutstandingPlayerDto> OutstandingPlayers { get; } = new();

    private bool _isInitialized;

    public FinanceViewModel(IFinanceService financeService, CurrentUserService currentUser)
    {
        _financeService = financeService;
        _currentUser = currentUser;
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

        var fromStr = from?.ToString("yyyy-MM-dd") ?? "";
        var toStr = to?.ToString("yyyy-MM-dd") ?? (Lang.IsArabic ? "\u0627\u0644\u0622\u0646" : "Now");
        DateRangeText = $"\U0001f4c5 {fromStr} \u2192 {toStr}";
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        ActivityLogger.LogNavigation("Finance");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SelectedPeriodIndex = 0;
        SearchText = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PeriodChangedAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PayOutstandingAsync(OutstandingPlayerDto? player)
    {
        if (player == null) return;

        var dialog = new Views.PayOutstandingDialog(player.Remaining);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        var amount = dialog.Amount;
        if (amount <= 0) return;

        try
        {
            await _financeService.PayOutstandingAsync(player.Id, amount);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
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

    private async Task LoadAsync()
    {
        ActivityLogger.LogAction("Finance", "Load", $"period={SelectedPeriodIndex}");
        IsLoading = true;
        try
        {
            var (from, to) = GetDateRange();
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

            var summary = await _financeService.GetSummaryAsync(from, to, search);
            TotalRevenue = summary.TotalRevenue;
            TotalExpenses = summary.TotalExpenses;
            NetProfit = summary.NetProfit;
            UnpaidBalances = summary.UnpaidBalances;

            RecentTransactions.Clear();
            foreach (var t in summary.RecentTransactions)
                RecentTransactions.Add(t);

            OutstandingPlayers.Clear();
            foreach (var p in summary.OutstandingPlayers)
                OutstandingPlayers.Add(p);
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
}
