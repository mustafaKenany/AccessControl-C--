using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.POS.ViewModels;

public partial class PosMainViewModel : ObservableObject
{
    private readonly PosViewModel _posViewModel;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    public string CurrentUserDisplayName => _currentUser.DisplayName ?? _currentUser.Username ?? "";

    public string AppVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return v == null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedTab;

    public PosViewModel PosVm => _posViewModel;

    public PosMainViewModel(PosViewModel posViewModel, CurrentUserService currentUser)
    {
        _posViewModel = posViewModel;
        _currentUser = currentUser;
        CurrentView = posViewModel;
    }

    public async Task InitializeAsync()
    {
        await _posViewModel.InitializeAsync();
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Lang.SwitchLanguage();
    }

    [RelayCommand]
    private void ShowSales()
    {
        SelectedTab = 0;
        CurrentView = _posViewModel;
    }

    [RelayCommand]
    private async Task ShowSummaryAsync()
    {
        SelectedTab = 1;
        if (_posViewModel.LoadDailySummaryCommand.CanExecute(null))
            await _posViewModel.LoadDailySummaryCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowShift()
    {
        SelectedTab = 2;
    }
}
