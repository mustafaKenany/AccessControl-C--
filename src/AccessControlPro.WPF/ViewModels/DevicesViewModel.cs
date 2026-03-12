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

public partial class DevicesViewModel : ObservableObject
{
    private readonly IDeviceService _deviceService;
    private readonly IAccessEventService _eventService;
    private readonly IEmployeeService _employeeService;
    private readonly CurrentUserService _currentUser;
    private List<DeviceDto> _allDevices = new();

    public LanguageManager Lang => LanguageManager.Instance;

    // Permission-based action visibility
    public bool CanAdd => _currentUser.HasPermission(AppPermission.DevicesAdd);
    public bool CanEdit => _currentUser.HasPermission(AppPermission.DevicesEdit);
    public bool CanDelete => _currentUser.HasPermission(AppPermission.DevicesDelete);
    public bool CanConnect => _currentUser.HasPermission(AppPermission.DevicesConnect);

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

    private bool _isInitialized;

    public DevicesViewModel(IDeviceService deviceService, IAccessEventService eventService, IEmployeeService employeeService, CurrentUserService currentUser)
    {
        _deviceService = deviceService;
        _eventService = eventService;
        _employeeService = employeeService;
        _currentUser = currentUser;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadDevicesAsync();
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
        // Check if WiFi and Ethernet are on the same subnet — causes conflicts
        if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
        {
            CustomMessageBox.Show(Lang.WifiWarningSearch,
                Lang.SearchNetwork, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            return;
        }

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
        if (!await ValidateDeviceReadyAsync(device, Lang.Connect)) return;
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
        if (!await ValidateDeviceReadyAsync(device, Lang.DeviceInfo)) return;
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
        if (!await ValidateDeviceReadyAsync(device, Lang.OpenDoor)) return;
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
        if (!await ValidateDeviceReadyAsync(device, Lang.SyncTime)) return;
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
        if (!await ValidateDeviceReadyAsync(device, Lang.ChangeIP)) return;

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

    [RelayCommand]
    private async Task DownloadLogsAsync(DeviceDto? device)
    {
        if (!await ValidateDeviceReadyAsync(device, Lang.DownloadLogs)) return;

        // Show period selection dialog
        var dialog = new Views.DownloadPeriodDialog();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() != true) return;

        var months = dialog.SelectedMonths;
        var cutoffDate = DateTime.UtcNow.AddMonths(-months);

        try
        {
            IsLoading = true;
            StatusMessage = string.Format(Lang.DownloadingLogs, device.Name, months);

            var count = await Task.Run(() => _eventService.FetchAndSaveRecordsAsync(device.Id, cutoffDate));

            IsLoading = false;

            if (count > 0)
            {
                StatusMessage = string.Format(Lang.DownloadedRecords, count, device.Name);
                CustomMessageBox.Show(
                    string.Format(Lang.DownloadedRecords, count, device.Name),
                    Lang.DownloadLogs,
                    MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = string.Format(Lang.NoRecordsDevice, device.Name);
                CustomMessageBox.Show(
                    string.Format(Lang.NoRecordsDevice, device.Name),
                    Lang.DownloadLogs,
                    MsgType.Info,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            IsLoading = false;
            StatusMessage = "Download failed";
            CustomMessageBox.Show(
                $"Download failed: {ex.Message}",
                Lang.DownloadLogs,
                MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SyncAllPlayersAsync(DeviceDto? device)
    {
        if (!await ValidateDeviceReadyAsync(device, Lang.DevSyncAllPlayers)) return;

        var confirmed = CustomMessageBox.Confirm(
            Lang.DevSyncAllConfirm,
            Lang.DevSyncAllPlayers,
            MsgType.Warning,
            System.Windows.Application.Current.MainWindow);

        if (!confirmed) return;

        try
        {
            IsLoading = true;
            StatusMessage = Lang.DevSyncAllProgress;

            var progress = new Progress<(int current, int total, string cardNumber)>(p =>
            {
                StatusMessage = $"{Lang.DevSyncAllProgress} ({p.current}/{p.total}) - {p.cardNumber}";
            });

            var (synced, failed, total) = await Task.Run(() =>
                _employeeService.SyncAllCardsToDeviceAsync(device.Id, progress));

            if (total == 0)
            {
                StatusMessage = Lang.DevSyncAllNoCards;
                CustomMessageBox.Show(Lang.DevSyncAllNoCards, Lang.DevSyncAllPlayers,
                    MsgType.Info, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                var msg = $"{Lang.DevSyncAllDone}\n\n{Lang.Total}: {total}\n{Lang.DevSynced}: {synced}\n{Lang.DevFailed}: {failed}";
                StatusMessage = $"Synced {synced}/{total} cards";
                CustomMessageBox.Show(msg, Lang.DevSyncAllPlayers,
                    failed > 0 ? MsgType.Warning : MsgType.Success,
                    System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Sync failed";
            CustomMessageBox.Show($"Sync failed: {ex.Message}", Lang.DevSyncAllPlayers,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Validates device exists in DB, no subnet conflict, and device is reachable (ping).
    /// Returns true if safe to proceed.
    /// </summary>
    private async Task<bool> ValidateDeviceReadyAsync(DeviceDto? device, string action)
    {
        if (device == null) return false;

        // 1. Check device still exists in DB
        var dbDevice = await _deviceService.GetDeviceByIdAsync(device.Id);
        if (dbDevice == null)
        {
            CustomMessageBox.Show(
                string.Format(Lang.DeviceNotExist, device.Name),
                action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            await LoadDevicesAsync();
            return false;
        }

        // 2. Check if WiFi and Ethernet are on the same subnet — causes conflicts
        if (NetworkHelper.IsWifiOnSameSubnetAsEthernet())
        {
            CustomMessageBox.Show(Lang.WifiWarning,
                action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
            return false;
        }

        // 3. Ping device to check if reachable — show spinner
        IsLoading = true;
        StatusMessage = Lang.CheckingConnection;
        try
        {
            var reachable = await NetworkHelper.PingDeviceAsync(dbDevice.IP);
            if (!reachable)
            {
                IsLoading = false;
                CustomMessageBox.Show(
                    string.Format(Lang.DeviceUnreachable, device.Name, device.IP),
                    action, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                device.IsOnline = false;
                dbDevice.IsOnline = false;
                await LoadDevicesAsync();
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
