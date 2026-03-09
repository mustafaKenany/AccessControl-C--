using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DeletedRecordsViewModel : ObservableObject
{
    private readonly IDeletedEmployeeService _deletedEmployeeService;
    private const int PageSize = 100;

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

    public ObservableCollection<DeletedEmployeeDto> DeletedRecords { get; } = new();

    public DeletedRecordsViewModel(IDeletedEmployeeService deletedEmployeeService)
    {
        _deletedEmployeeService = deletedEmployeeService;
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            _ = LoadPagedAsync();
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
            var (items, totalCount) = await _deletedEmployeeService.GetPagedAsync(CurrentPage, PageSize, search);
            var list = items.ToList();

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            DeletedRecords.Clear();
            foreach (var rec in list)
                DeletedRecords.Add(rec);
        }
        catch
        {
            // Empty state on first run or DB not ready
        }
        finally
        {
            IsLoading = false;
        }
    }
}
