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
        ActivityLogger.LogNavigation("Devices");
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

        // Single Device mode: block if already have 1 device
        if (Helpers.DeviceModeHelper.IsSingleDevice && _allDevices.Count >= 1)
        {
            CustomMessageBox.Show(
                "Single Device Mode: Only 1 device is allowed.\n\nTo add a different device, delete the existing one first.\nOr change to Multi Device mode in settings.",
                "Device Limit", MsgType.Warning, System.Windows.Application.Current.MainWindow);
            return;
        }

        IsLoading = true;
        StatusMessage = "Searching network...";
        try
        {
            var found = await _deviceService.SearchNetworkAsync();
            if (found != null)
            {
                // Single Device mode: verify only 1 device on network
                if (Helpers.DeviceModeHelper.IsSingleDevice)
                {
                    // Check if there are other devices already in DB
                    var existingDevices = _allDevices.Where(d => d.SerialNumber != found.SerialNumber).ToList();
                    if (existingDevices.Count > 0)
                    {
                        CustomMessageBox.Show(
                            $"Single Device Mode: Another device already exists ({existingDevices[0].Name}).\n\nOnly 1 device is allowed to prevent fraud.\nDelete the existing device first or switch to Multi Device mode.",
                            "Device Limit", MsgType.Warning, System.Windows.Application.Current.MainWindow);
                        IsLoading = false;
                        return;
                    }
                }

                // Check if device already exists by serial number
                var existing = _allDevices.FirstOrDefault(d =>
                    d.SerialNumber == found.SerialNumber);

                if (existing == null)
                {
                    await _deviceService.AddDeviceAsync(found);
                    ActivityLogger.LogAction("Devices", "AddDevice", $"{found.IP} ({found.SerialNumber})");
                    StatusMessage = $"Found: {found.IP} ({found.SerialNumber})";
                }
                else
                {
                    StatusMessage = $"Device already exists: {existing.IP}";
                }

                await LoadDevicesAsync();

                // A brand-new serial means an empty controller (e.g. a replaced/broken device) — offer
                // to repopulate it with the existing active members + QR pool so it works immediately.
                if (existing == null)
                {
                    var newDevice = _allDevices.FirstOrDefault(d => d.SerialNumber == found.SerialNumber);
                    if (newDevice != null)
                        await LoadDataOntoDeviceAsync(newDevice, askConfirm: true);
                }
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
            StatusMessage = $"Connecting to {device!.IP}...";
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
            var info = await _deviceService.GetDeviceInfoAsync(device!.Id);
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
            if (device!.DoorCount <= 1)
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

            ActivityLogger.LogAction("Devices", "OpenDoor", $"{device!.Name} door {doorToOpen}");
            IsLoading = true;
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
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncTimeAsync(DeviceDto? device)
    {
        if (!await ValidateDeviceReadyAsync(device, Lang.SyncTime)) return;
        IsLoading = true;
        try
        {
            ActivityLogger.LogAction("Devices", "SyncTime", $"{device!.Name} ({device.IP})");
            StatusMessage = "Syncing time...";
            var success = await _deviceService.SyncTimeAsync(device!.Id);
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
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FactoryResetAsync(DeviceDto? device)
    {
        if (!await ValidateDeviceReadyAsync(device, "Factory Reset")) return;

        var confirmed = Views.CustomMessageBox.Confirm(
            $"WARNING: This will erase ALL data on device '{device!.Name}' ({device.IP}) and restore factory defaults.\n\nAll cards, settings, and records on this device will be permanently deleted.\n\nAre you sure?",
            "Factory Reset",
            Views.MsgType.Warning,
            System.Windows.Application.Current.MainWindow);
        if (!confirmed) return;

        var doubleConfirm = Views.CustomMessageBox.Confirm(
            $"FINAL CONFIRMATION:\n\nDevice: {device.Name} ({device.IP})\n\nThis action CANNOT be undone. Continue?",
            "Factory Reset",
            Views.MsgType.Warning,
            System.Windows.Application.Current.MainWindow);
        if (!doubleConfirm) return;

        IsLoading = true;
        try
        {
            ActivityLogger.LogAction("Devices", "FactoryReset", $"{device.Name} ({device.IP})");
            StatusMessage = "Factory resetting device...";
            await _deviceService.FactoryResetAsync(device.Id);
            StatusMessage = "Factory reset complete";
            Views.CustomMessageBox.Show(
                $"Device '{device.Name}' has been reset to factory defaults.\nAll cards and settings erased.\nRe-assign cards to this device.",
                "Factory Reset", Views.MsgType.Success,
                System.Windows.Application.Current.MainWindow);
        }
        catch (Exception ex)
        {
            StatusMessage = "Factory reset failed";
            Views.CustomMessageBox.Show($"Factory reset failed: {ex.Message}",
                "Factory Reset", Views.MsgType.Error,
                System.Windows.Application.Current.MainWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ChangeIPAsync(DeviceDto? device)
    {
        if (!await ValidateDeviceReadyAsync(device, Lang.ChangeIP)) return;

        var dialog = new Views.NetworkSettingsDialog(
            device!.IP, device.SubnetMask, device.Gateway);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() != true) return;

        var newIP = dialog.IpAddress;
        var newSubnet = dialog.SubnetMask;
        var newGateway = dialog.Gateway;

        // Skip if nothing changed
        if (newIP == device.IP && newSubnet == device.SubnetMask && newGateway == device.Gateway) return;

        IsLoading = true;
        try
        {
            ActivityLogger.LogAction("Devices", "ChangeIP", $"{device.Name}: {device.IP} -> {newIP}");
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
        finally
        {
            IsLoading = false;
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
            IsLoading = true;
            try
            {
                ActivityLogger.LogAction("Devices", "DeleteDevice", $"{device.Name} ({device.IP})");
                StatusMessage = $"Deleting {device.Name}...";
                await _deviceService.DeleteDeviceAsync(device.Id);
                await LoadDevicesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed";
                CustomMessageBox.Show($"Failed to delete device: {ex.Message}", Lang.Delete,
                    MsgType.Error, System.Windows.Application.Current.MainWindow);
            }
            finally
            {
                IsLoading = false;
            }
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
            StatusMessage = string.Format(Lang.DownloadingLogs, device!.Name, months);

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
        await LoadDataOntoDeviceAsync(device!, askConfirm: true);
    }

    /// <summary>
    /// Repopulates a device with active-subscription members FIRST (gym usable within seconds),
    /// then the full QR pool (Daily Pass + Visitor) in the background, with phased progress.
    /// Used by the manual "sync all" button AND by the new-device auto-prompt.
    /// </summary>
    private async Task LoadDataOntoDeviceAsync(DeviceDto device, bool askConfirm)
    {
        var (members, pool) = await _employeeService.GetDeviceSyncCountsAsync();

        if (askConfirm)
        {
            var prompt = Lang.IsArabic
                ? $"تحميل البيانات على الجهاز \"{device.Name}\"؟\n\n• الأعضاء الفعّالون: {members}\n• رموز QR (دخول يومي + زوار): {pool}\n\nيبدأ بالأعضاء (ثوانٍ) ثم رموز QR بالخلفية."
                : $"Load data onto \"{device.Name}\"?\n\n• Active members: {members}\n• QR passes (daily + visitor): {pool}\n\nMembers load first (seconds), then the QR pool in the background.";
            if (!CustomMessageBox.Confirm(prompt, Lang.DevSyncAllPlayers, MsgType.Warning,
                    System.Windows.Application.Current.MainWindow))
                return;
        }

        try
        {
            IsLoading = true;
            var progress = new Progress<DeviceSyncProgress>(p =>
            {
                var phase = p.Phase == "Members"
                    ? (Lang.IsArabic ? "تحميل الأعضاء" : "Loading members")
                    : (Lang.IsArabic ? "تحميل رموز QR" : "Loading QR passes");
                StatusMessage = $"{phase}: {p.Done}/{p.Total}";
            });

            var (mSynced, mFailed, pPushed, pFailed) = await Task.Run(() =>
                _employeeService.SyncAllDataToDeviceAsync(device.Id, progress));

            // Persist a device-sync report (pairs with the import report for a full picture).
            var reportPath = AccessControlPro.Application.Services.ImportReportLog.WriteDeviceSyncReport(
                device.Name, device.IP, mSynced, mFailed, pPushed, pFailed);

            StatusMessage = Lang.IsArabic
                ? $"اكتمل: أعضاء {mSynced}، رموز {pPushed}"
                : $"Done: {mSynced} members, {pPushed} passes";

            var reportLine = string.IsNullOrEmpty(reportPath)
                ? ""
                : "\n\n" + (Lang.IsArabic ? "تقرير المزامنة: " : "Sync report: ") + reportPath;
            var result = (Lang.IsArabic
                ? $"اكتمل التحميل على {device.Name}.\n\nالأعضاء: {mSynced}{(mFailed > 0 ? $" (فشل {mFailed})" : "")}\nرموز QR: {pPushed}{(pFailed > 0 ? $" (فشل {pFailed})" : "")}"
                : $"Load complete on {device.Name}.\n\nMembers: {mSynced}{(mFailed > 0 ? $" ({mFailed} failed)" : "")}\nQR passes: {pPushed}{(pFailed > 0 ? $" ({pFailed} failed)" : "")}")
                + reportLine;
            CustomMessageBox.Show(result, Lang.DevSyncAllPlayers,
                (mFailed + pFailed) > 0 ? MsgType.Warning : MsgType.Success,
                System.Windows.Application.Current.MainWindow);
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
