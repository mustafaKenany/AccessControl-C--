using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly IDeviceService _deviceService;
    private List<DeviceDto> _allDevices = new();

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DeviceDto? _selectedDevice;

    [ObservableProperty]
    private int _onlineCount;

    [ObservableProperty]
    private int _offlineCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<DeviceDto> Devices { get; } = new();

    public DevicesViewModel(IDeviceService deviceService)
    {
        _deviceService = deviceService;
        _ = LoadDevicesAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDevicesAsync();
    }

    [RelayCommand]
    private async Task SearchNetworkAsync()
    {
        IsLoading = true;
        StatusMessage = "Searching network...";
        try
        {
            var found = await _deviceService.SearchNetworkAsync();
            if (found != null)
            {
                // Check if device already exists by serial number
                var existing = _allDevices.FirstOrDefault(d =>
                    d.SerialNumber == found.SerialNumber);

                if (existing == null)
                {
                    await _deviceService.AddDeviceAsync(found);
                    StatusMessage = $"Found: {found.IP} ({found.SerialNumber})";
                }
                else
                {
                    StatusMessage = $"Device already exists: {existing.IP}";
                }

                await LoadDevicesAsync();
            }
            else
            {
                StatusMessage = "No device found on network";
                CustomMessageBox.Show("No device found.\nMake sure the controller is connected and powered on.",
                    "Search Network", MsgType.Info, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Search failed";
            var msg = ex.InnerException != null
                ? $"{ex.Message}\n\nDetails: {ex.InnerException.Message}"
                : ex.Message;
            CustomMessageBox.Show($"Search failed: {msg}",
                "Error", MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync(DeviceDto? device)
    {
        if (device == null) return;
        try
        {
            StatusMessage = $"Connecting to {device.IP}...";
            var success = await _deviceService.ConnectDeviceAsync(device.Id);
            if (success)
            {
                StatusMessage = Lang.ConnectSuccess;
                await LoadDevicesAsync();
                CustomMessageBox.Show(Lang.ConnectSuccess, Lang.Connect,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Connection failed";
                CustomMessageBox.Show("Failed to connect to device.", Lang.Connect,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Connection failed";
            CustomMessageBox.Show($"Connection failed: {ex.Message}", Lang.Connect,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task GetDeviceInfoAsync(DeviceDto? device)
    {
        if (device == null) return;
        try
        {
            StatusMessage = "Reading device info...";
            var info = await _deviceService.GetDeviceInfoAsync(device.Id);
            StatusMessage = "Device info retrieved";
            var displayInfo = string.IsNullOrWhiteSpace(info) ? "No info returned from device." : info;
            CustomMessageBox.Show($"{device.Name} ({device.IP})\nSN: {device.SerialNumber}\n\n{displayInfo}",
                Lang.DeviceInfo, MsgType.Info, System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to get device info";
            CustomMessageBox.Show($"Failed to get device info: {ex.Message}", Lang.DeviceInfo,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task RemoteOpenDoorAsync(DeviceDto? device)
    {
        if (device == null) return;
        try
        {
            int doorToOpen;
            if (device.DoorCount <= 1)
            {
                doorToOpen = 1;
            }
            else
            {
                // Build door selection options
                var options = new System.Text.StringBuilder();
                options.AppendLine($"{Lang.SelectDoor} (1-{device.DoorCount}):");
                for (int i = 1; i <= device.DoorCount; i++)
                    options.AppendLine($"  {i} - {Lang.Door} {i}");
                options.AppendLine($"  0 - All Doors");

                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    options.ToString(), Lang.OpenDoor, "1");

                if (string.IsNullOrWhiteSpace(input)) return;
                if (!int.TryParse(input, out doorToOpen) || doorToOpen < 0 || doorToOpen > device.DoorCount)
                {
                    CustomMessageBox.Show($"Invalid door number. Enter 0-{device.DoorCount}.",
                        Lang.OpenDoor, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                    return;
                }
            }

            StatusMessage = "Opening door...";
            bool success;
            if (doorToOpen == 0)
            {
                // Open all doors in a single SDK call
                success = await _deviceService.RemoteOpenAllDoorsAsync(device.Id);
            }
            else
            {
                success = await _deviceService.RemoteOpenDoorAsync(device.Id, doorToOpen);
            }

            if (success)
            {
                var doorLabel = doorToOpen == 0 ? "All Doors" : $"{Lang.Door} {doorToOpen}";
                StatusMessage = Lang.OpenDoorSuccess;
                CustomMessageBox.Show($"{Lang.OpenDoorSuccess}\n{doorLabel}", Lang.OpenDoor,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to open door";
                CustomMessageBox.Show("Failed to open door.", Lang.OpenDoor,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to open door";
            CustomMessageBox.Show($"Failed to open door: {ex.Message}", Lang.OpenDoor,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SyncTimeAsync(DeviceDto? device)
    {
        if (device == null) return;
        try
        {
            StatusMessage = "Syncing time...";
            var success = await _deviceService.SyncTimeAsync(device.Id);
            if (success)
            {
                StatusMessage = Lang.SyncTimeSuccess;
                CustomMessageBox.Show(Lang.SyncTimeSuccess, Lang.SyncTime,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to sync time";
                CustomMessageBox.Show("Failed to sync time.", Lang.SyncTime,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to sync time";
            CustomMessageBox.Show($"Failed to sync time: {ex.Message}", Lang.SyncTime,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task ChangeIPAsync(DeviceDto? device)
    {
        if (device == null) return;

        var dialog = new Views.NetworkSettingsDialog(
            device.IP, device.SubnetMask, device.Gateway);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        var newIP = dialog.IpAddress;
        var newSubnet = dialog.SubnetMask;
        var newGateway = dialog.Gateway;

        // Skip if nothing changed
        if (newIP == device.IP && newSubnet == device.SubnetMask && newGateway == device.Gateway) return;

        try
        {
            StatusMessage = $"Changing network settings to {newIP}...";
            var success = await _deviceService.ChangeIPAsync(device.Id, newIP, newSubnet, newGateway);
            if (success)
            {
                StatusMessage = Lang.ChangeIPSuccess;
                await LoadDevicesAsync();
                CustomMessageBox.Show($"{Lang.ChangeIPSuccess}\nIP: {newIP}\nSubnet: {newSubnet}\nGateway: {newGateway}",
                    Lang.ChangeIP, MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to change IP";
                CustomMessageBox.Show("Failed to change network settings.", Lang.ChangeIP,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to change IP";
            CustomMessageBox.Show($"Failed to change network settings: {ex.Message}", Lang.ChangeIP,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task RenameDeviceAsync(DeviceDto? device)
    {
        if (device == null) return;
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            Lang.EnterName, Lang.Edit, device.Name);

        if (string.IsNullOrWhiteSpace(input) || input == device.Name) return;

        var success = await _deviceService.RenameDeviceAsync(device.Id, input);
        if (success)
        {
            await LoadDevicesAsync();
            CustomMessageBox.Show($"{Lang.RenameSuccess}\n{input}", Lang.Edit,
                MsgType.Success, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task DeleteDeviceAsync(DeviceDto? device)
    {
        if (device == null) return;

        var confirmed = CustomMessageBox.Confirm(
            Lang.ConfirmDelete,
            Lang.Delete,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);

        if (confirmed)
        {
            await _deviceService.DeleteDeviceAsync(device.Id);
            await LoadDevicesAsync();
        }
    }

    private async Task LoadDevicesAsync()
    {
        IsLoading = true;
        try
        {
            var devices = await _deviceService.GetAllDevicesAsync();
            _allDevices = devices.ToList();

            OnlineCount = _allDevices.Count(d => d.IsOnline);
            OfflineCount = _allDevices.Count(d => !d.IsOnline);
            TotalCount = _allDevices.Count;

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
        Devices.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allDevices
            : _allDevices.Where(d =>
                d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.IP.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.SerialNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceType.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var device in filtered)
            Devices.Add(device);
    }
}
