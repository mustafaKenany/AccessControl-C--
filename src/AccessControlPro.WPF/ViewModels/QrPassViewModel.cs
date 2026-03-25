using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class QrPassViewModel : ObservableObject
{
    private readonly IQrPassService _qrPassService;
    private readonly IDeviceService _deviceService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _activeTodayCount;
    [ObservableProperty] private bool _showActiveOnly;

    public ObservableCollection<QrPassDto> Passes { get; } = new();

    private bool _isInitialized;
    private const int PageSize = 20;

    public QrPassViewModel(IQrPassService qrPassService, IDeviceService deviceService, CurrentUserService currentUser)
    {
        _qrPassService = qrPassService;
        _deviceService = deviceService;
        _currentUser = currentUser;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var (items, total) = await _qrPassService.GetPassesPagedAsync(
                CurrentPage, PageSize,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                ShowActiveOnly ? true : null);

            Passes.Clear();
            foreach (var item in items)
                Passes.Add(item);

            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            ActiveTodayCount = await _qrPassService.GetActiveTodayCountAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, "Error", MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreatePassAsync()
    {
        // Load devices for auto-selection (no user selection needed)
        var devices = await _deviceService.GetAllDevicesAsync();
        var dialog = new CreateQrPassDialog(devices);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        IsLoading = true;
        try
        {
            // Hardcoded: 365 days, 2 uses, auto-select first device
            var pass = await _qrPassService.CreatePassAsync(
                dialog.PlayerName, dialog.Phone, dialog.Fee,
                dialog.MaxUses, dialog.ValidDays,
                dialog.SelectedDeviceId, dialog.SelectedDoorNumber, dialog.SelectedDeviceName);

            // Show QR code for printing
            var qrDialog = new QrCodeDisplayDialog(pass);
            qrDialog.Owner = System.Windows.Application.Current.MainWindow;
            qrDialog.ShowDialog();

            CurrentPage = 1;
            await LoadAsync();
            StatusMessage = Lang.QrPassCreated;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, "Error", MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeactivatePassAsync(QrPassDto? pass)
    {
        if (pass == null) return;

        var confirmed = CustomMessageBox.Confirm(
            $"{Lang.QrConfirmDeactivate}\n{pass.PlayerName} - {pass.PassCode}",
            Lang.QrDeactivate,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);
        if (!confirmed) return;

        try
        {
            await _qrPassService.DeactivatePassAsync(pass.Id);
            await LoadAsync();
            StatusMessage = Lang.QrPassDeactivated;
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, "Error", MsgType.Error);
        }
    }

    [RelayCommand]
    private void ViewQrCode(QrPassDto? pass)
    {
        if (pass == null) return;
        var qrDialog = new QrCodeDisplayDialog(pass);
        qrDialog.Owner = System.Windows.Application.Current.MainWindow;
        qrDialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenScanWindow()
    {
        var scanWindow = new QrScanWindow(_qrPassService, _deviceService);
        scanWindow.Owner = System.Windows.Application.Current.MainWindow;
        scanWindow.ShowDialog();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchText = string.Empty;
        ShowActiveOnly = false;
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
    private async Task PrevPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleActiveFilterAsync()
    {
        ShowActiveOnly = !ShowActiveOnly;
        CurrentPage = 1;
        await LoadAsync();
    }
}
