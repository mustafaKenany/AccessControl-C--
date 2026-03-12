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

    [ObservableProperty]
    private object? _currentView;

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
}
