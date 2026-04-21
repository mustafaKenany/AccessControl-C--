using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly IAuditLogService _auditLogService;
    private const int PageSize = 100;
    private CancellationTokenSource? _searchCts;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _isDataLoaded;

    // Period filter: 0=All, 1=Today, 2=Yesterday, 3=This Week, 4=Last Week, 5=This Month, 6=Last Month, 7=Last 3 Months
    [ObservableProperty]
    private int _selectedPeriodIndex;

    [ObservableProperty]
    private string _dateRangeText = "";

    public ObservableCollection<AuditLogDto> Logs { get; } = new();

    public LogsViewModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    partial void OnSelectedPeriodIndexChanged(int value)
    {
        UpdateDateRangeText();
    }

    private void UpdateDateRangeText()
    {
        var (from, to) = GetPeriodRange();
        if (from == null && to == null)
        {
            DateRangeText = "";
            return;
        }

        var fromStr = from?.ToString("yyyy-MM-dd") ?? "";
        var toStr = to?.ToString("yyyy-MM-dd") ?? (Lang.IsArabic ? "\u0627\u0644\u0622\u0646" : "Now");
        DateRangeText = $"\U0001f4c5 {fromStr} \u2192 {toStr}";
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            _ = DebouncedSearchAsync();
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
                await LoadPagedAsync();
        }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        CurrentPage = 1;
        IsDataLoaded = true;
        await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsDataLoaded)
            await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task SetPeriodAsync(string indexStr)
    {
        if (!int.TryParse(indexStr, out var index)) return;
        SelectedPeriodIndex = index;
        CurrentPage = 1;
        IsDataLoaded = true;
        await LoadPagedAsync();
    }

    private (DateTime? from, DateTime? to) GetPeriodRange()
    {
        var now = DateTime.UtcNow;
        return SelectedPeriodIndex switch
        {
            1 => (now.Date, now.Date.AddDays(1).AddSeconds(-1)),
            2 => (now.Date.AddDays(-1), now.Date.AddSeconds(-1)),
            3 => (now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7), now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7).AddDays(7).AddSeconds(-1)),
            4 => (now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7 - 7), now.Date.AddDays(-((int)now.DayOfWeek + 1) % 7 - 7).AddDays(7).AddSeconds(-1)),
            5 => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(-1)),
            6 => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1).AddSeconds(-1)),
            7 => (new DateTime(now.Year, now.Month, 1).AddMonths(-3), now.Date.AddDays(1).AddSeconds(-1)),
            _ => (null, null)
        };
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPagedAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPagedAsync();
        }
    }

    private async Task LoadPagedAsync()
    {
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var (from, to) = GetPeriodRange();
            var (items, totalCount) = await _auditLogService.GetPagedAsync(CurrentPage, PageSize, search, from, to);
            var list = items.ToList();

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            Logs.Clear();
            foreach (var log in list)
                Logs.Add(log);
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
