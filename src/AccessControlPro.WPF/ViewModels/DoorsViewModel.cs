using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DoorsViewModel : ObservableObject
{
    private readonly IDoorService _doorService;
    private readonly IDeviceService _deviceService;
    private readonly CurrentUserService _currentUser;
    private List<DoorDto> _allDoors = new();

    public LanguageManager Lang => LanguageManager.Instance;

    // Permission-based action visibility
    public bool CanOpenClose => _currentUser.HasPermission(AppPermission.DoorsOpenClose);
    public bool CanSettings => _currentUser.HasPermission(AppPermission.DoorsSettings);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _openCount;

    [ObservableProperty]
    private int _closedCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<DoorDto> Doors { get; } = new();

    private bool _isInitialized;

    public DoorsViewModel(IDoorService doorService, IDeviceService deviceService, CurrentUserService currentUser)
    {
        _doorService = doorService;
        _deviceService = deviceService;
        _currentUser = currentUser;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        ActivityLogger.LogNavigation("Doors");
        try
        {
            await LoadDoorsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DoorsViewModel] InitializeAsync error: {ex}");
            Views.CustomMessageBox.Show($"Error loading doors: {ex.Message}", "Error",
                Views.MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDoorsAsync();
    }

    /// <summary>
    /// Validates device for this door: exists, no subnet conflict, ping reachable.
    /// </summary>
    private async Task<bool> ValidateDoorDeviceReadyAsync(DoorDto? door, string action)
    {
        if (door == null) return false;

        // 1. Check device still exists in DB
        var dbDevice = await _deviceService.GetDeviceByIdAsync(door.DeviceId);
        if (dbDevice == null)
        {
            CustomMessageBox.Show(
                string.Format(Lang.DeviceNotExist, door.DeviceName),
                action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            await LoadDoorsAsync();
            return false;
        }

        // 2. Check if WiFi and Ethernet are on the same subnet
        if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
        {
            CustomMessageBox.Show(Lang.WifiWarning,
                action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            return false;
        }

        // 3. Ping device to check if reachable
        IsLoading = true;
        StatusMessage = Lang.CheckingConnection;
        try
        {
            var reachable = await NetworkHelper.PingDeviceAsync(door.DeviceIP);
            if (!reachable)
            {
                IsLoading = false;
                CustomMessageBox.Show(
                    string.Format(Lang.DeviceUnreachable, door.DeviceName, door.DeviceIP),
                    action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                await LoadDoorsAsync();
                return false;
            }
        }
        catch
        {
            IsLoading = false;
            return false;
        }
        IsLoading = false;

        return true;
    }

    [RelayCommand]
    private async Task OpenDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        if (!await ValidateDoorDeviceReadyAsync(door, Lang.OpenDoor)) return;

        ActivityLogger.LogAction("Doors", "OpenDoor", door.Name);
        IsLoading = true;
        try
        {
            StatusMessage = string.Format(Lang.OpeningDoor, door.Name);
            var success = await _doorService.OpenDoorAsync(door.Id);
            if (success)
            {
                StatusMessage = Lang.OpenDoorSuccess;
                CustomMessageBox.Show($"{Lang.OpenDoorSuccess}\n{door.Name}", Lang.OpenDoor,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = Lang.DoorOpenFailed;
                CustomMessageBox.Show(Lang.DoorOpenFailed, Lang.OpenDoor,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = Lang.DoorOpenFailed;
            CustomMessageBox.Show($"{Lang.DoorOpenFailed}\n{ex.Message}", Lang.OpenDoor,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CloseDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        if (!await ValidateDoorDeviceReadyAsync(door, Lang.CloseDoor)) return;

        ActivityLogger.LogAction("Doors", "CloseDoor", door.Name);
        IsLoading = true;
        try
        {
            StatusMessage = string.Format(Lang.ClosingDoor, door.Name);
            var success = await _doorService.CloseDoorAsync(door.Id);
            if (success)
            {
                StatusMessage = Lang.CloseDoorSuccess;
                CustomMessageBox.Show($"{Lang.CloseDoorSuccess}\n{door.Name}", Lang.CloseDoor,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = Lang.DoorCloseFailed;
                CustomMessageBox.Show(Lang.DoorCloseFailed, Lang.CloseDoor,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = Lang.DoorCloseFailed;
            CustomMessageBox.Show($"{Lang.DoorCloseFailed}\n{ex.Message}", Lang.CloseDoor,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SetDelayAsync(DoorDto? door)
    {
        if (door == null) return;
        if (!await ValidateDoorDeviceReadyAsync(door, Lang.SetDelay)) return;

        try
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                Lang.EnterDelay, Lang.SetDelay, "5");

            if (string.IsNullOrWhiteSpace(input)) return;
            if (!int.TryParse(input, out var seconds) || seconds < 1 || seconds > 99)
            {
                CustomMessageBox.Show(Lang.DoorDelayInvalid,
                    Lang.SetDelay, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                return;
            }

            ActivityLogger.LogAction("Doors", "SetDelay", $"{door.Name}: {seconds}s");
            IsLoading = true;
            StatusMessage = string.Format(Lang.SettingDelay, door.Name);
            var success = await _doorService.SetDoorDelayAsync(door.Id, seconds);
            if (success)
            {
                StatusMessage = Lang.SetDelaySuccess;
                CustomMessageBox.Show($"{Lang.SetDelaySuccess}\n{door.Name}: {seconds}s", Lang.SetDelay,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = Lang.DoorDelayFailed;
                CustomMessageBox.Show(Lang.DoorDelayFailed, Lang.SetDelay,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = Lang.DoorDelayFailed;
            CustomMessageBox.Show($"{Lang.DoorDelayFailed}\n{ex.Message}", Lang.SetDelay,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SetPasswordAsync(DoorDto? door)
    {
        if (door == null) return;
        if (!await ValidateDoorDeviceReadyAsync(door, Lang.SetPassword)) return;

        try
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                Lang.EnterPassword, Lang.SetPassword, "");

            if (string.IsNullOrWhiteSpace(input)) return;

            ActivityLogger.LogAction("Doors", "SetPassword", door.Name);
            IsLoading = true;
            StatusMessage = string.Format(Lang.SettingPassword, door.Name);
            var success = await _doorService.SetDoorPasswordAsync(door.Id, input);
            if (success)
            {
                StatusMessage = Lang.SetPasswordSuccess;
                CustomMessageBox.Show($"{Lang.SetPasswordSuccess}\n{door.Name}", Lang.SetPassword,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = Lang.DoorPasswordFailed;
                CustomMessageBox.Show(Lang.DoorPasswordFailed, Lang.SetPassword,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = Lang.DoorPasswordFailed;
            CustomMessageBox.Show($"{Lang.DoorPasswordFailed}\n{ex.Message}", Lang.SetPassword,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenameDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            Lang.EnterName, Lang.Edit, door.Name);

        if (string.IsNullOrWhiteSpace(input) || input == door.Name) return;

        ActivityLogger.LogAction("Doors", "RenameDoor", $"{door.Name} -> {input}");
        var success = await _doorService.RenameDoorAsync(door.Id, input);
        if (success)
        {
            await LoadDoorsAsync();
            CustomMessageBox.Show($"{Lang.RenameSuccess}\n{input}", Lang.Edit,
                MsgType.Success, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SetScheduleAsync(DoorDto? door)
    {
        if (door == null) return;

        var dialog = new SetDoorScheduleDialog(door)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (dialog.ShowDialog() == true)
        {
            ActivityLogger.LogAction("Doors", "SetSchedule", door.Name);
            IsLoading = true;
            try
            {
                var success = await _doorService.UpdateDoorScheduleAsync(
                    door.Id, dialog.StartTime, dialog.EndTime, dialog.Is24Hours, dialog.WorkingDaysResult);
                if (success)
                {
                    await LoadDoorsAsync();
                    CustomMessageBox.Show($"{Lang.SetSchedule}\n{door.Name}", Lang.SetSchedule,
                        MsgType.Success, System.Windows.Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, Lang.SetSchedule,
                    MsgType.Error, System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadDoorsAsync()
    {
        IsLoading = true;
        try
        {
            var doors = await _doorService.GetAllDoorsAsync();
            _allDoors = doors.ToList();

            TotalCount = _allDoors.Count;
            OpenCount = _allDoors.Count(d => d.Status == "Open");
            ClosedCount = _allDoors.Count(d => d.Status == "Closed");

            ApplyFilter();
        }
        catch
        {
            // Empty state on first run
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Doors.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allDoors
            : _allDoors.Where(d =>
                d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceIP.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var door in filtered)
            Doors.Add(door);
    }
}
