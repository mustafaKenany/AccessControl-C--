using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class MonitorViewModel : ObservableObject
{
    private readonly IAccessControlSdk _sdk;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDoorRepository _doorRepository;
    private readonly IAccessCardRepository _cardRepository;
    private readonly IAccessEventService _eventService;
    private readonly IDoorService _doorService;
    private readonly IEmployeeService _employeeService;
    private readonly IMonitorLockService _monitorLockService;
    private readonly CurrentUserService _currentUser;
    private readonly Application.Helpers.DeviceOperationHelper _opHelper;
    private System.Windows.Threading.DispatcherTimer? _heartbeatTimer;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _connectedDevices;

    [ObservableProperty]
    private int _todayEventCount;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isDisplayOpen;

    public ObservableCollection<AccessEventDto> LiveEvents { get; } = new();

    private Views.MonitorDisplayWindow? _displayWindow;

    // Selected doors for projector display filtering (multi-select)
    private readonly HashSet<(string DeviceSN, int DoorNumber, string DoorName)> _selectedDisplayDoors = new();

    public MonitorViewModel(
        IAccessControlSdk sdk,
        IDeviceRepository deviceRepository,
        IDoorRepository doorRepository,
        IAccessCardRepository cardRepository,
        IAccessEventService eventService,
        IDoorService doorService,
        IEmployeeService employeeService,
        IMonitorLockService monitorLockService,
        CurrentUserService currentUser,
        Application.Helpers.DeviceOperationHelper opHelper)
    {
        _sdk = sdk;
        _deviceRepository = deviceRepository;
        _doorRepository = doorRepository;
        _cardRepository = cardRepository;
        _eventService = eventService;
        _doorService = doorService;
        _employeeService = employeeService;
        _monitorLockService = monitorLockService;
        _currentUser = currentUser;
        _opHelper = opHelper;
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;
        ActivityLogger.LogAction("Monitor", "StartMonitoring");

        try
        {
            // Check if another PC already has the monitor running
            StatusMessage = "Checking monitor lock...";
            var (acquired, holder) = await _monitorLockService.TryAcquireAsync();
            if (!acquired)
            {
                StatusMessage = $"Monitor is already running on: {holder}";
                Views.CustomMessageBox.Show(
                    $"Real-Time Monitor is already running on another PC:\n\n{holder}\n\nOnly one monitor can run at a time.",
                    "Monitor Locked", Views.MsgType.Warning,
                    System.Windows.Application.Current.MainWindow);
                return;
            }

            StatusMessage = "Checking device connectivity...";
            var allDevices = (await _deviceRepository.GetAllAsync()).ToList();
            if (allDevices.Count == 0)
            {
                StatusMessage = "No devices configured";
                return;
            }

            // Ping all devices in parallel to find which are actually reachable
            var pingTasks = allDevices.Select(async d =>
            {
                try
                {
                    using var ping = new System.Net.NetworkInformation.Ping();
                    var reply = await ping.SendPingAsync(d.IP, 2000);
                    return (Device: d, IsReachable: reply.Status == System.Net.NetworkInformation.IPStatus.Success);
                }
                catch
                {
                    return (Device: d, IsReachable: false);
                }
            });

            var results = await Task.WhenAll(pingTasks);
            var devices = results.Where(r => r.IsReachable).Select(r => r.Device).ToList();

            if (devices.Count == 0)
            {
                StatusMessage = $"No online devices found (checked {allDevices.Count} device(s))";
                return;
            }

            var deviceInfos = devices.Select(d => new DeviceInfo
            {
                IP = d.IP,
                MAC = d.MAC,
                SerialNumber = d.SerialNumber,
                TCPPort = d.TCPPort,
                UDPPort = d.UDPPort,
                Password = d.Password,
                Gateway = d.Gateway,
                SubnetMask = d.SubnetMask
            }).ToList();

            // Auto-sync device time before monitoring
            StatusMessage = "Syncing device time...";
            foreach (var di in deviceInfos)
            {
                try
                {
                    _sdk.CalibrateTime(di);
                }
                catch { /* non-critical, continue */ }
            }

            // Track monitoring state so DeviceOperationHelper can restart after SDK reset
            _opHelper.SetMonitoringState(deviceInfos, OnMonitorEvent);
            _sdk.StartMonitoring(deviceInfos, OnMonitorEvent);
            ConnectedDevices = devices.Count;
            IsMonitoring = true;
            StatusMessage = $"Monitoring {devices.Count} device(s)...";

            // Heartbeat timer — keeps the DB lock alive every 10 seconds
            _heartbeatTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _heartbeatTimer.Tick += async (_, _) =>
            {
                try { await _monitorLockService.HeartbeatAsync(); }
                catch { /* ignore heartbeat errors */ }
            };
            _heartbeatTimer.Start();
        }
        catch (Exception ex)
        {
            await _monitorLockService.ReleaseAsync();
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopMonitoringAsync()
    {
        // Only restart if monitoring was actually running
        bool wasMonitoring = IsMonitoring;

        ActivityLogger.LogAction("Monitor", "StopMonitoring");
        _opHelper.SetMonitoringState(null, null); // Clear monitoring state
        _sdk.StopMonitoring();
        _heartbeatTimer?.Stop();
        _heartbeatTimer = null;
        await _monitorLockService.ReleaseAsync();
        IsMonitoring = false;
        ConnectedDevices = 0;
        StatusMessage = "Stopped";

        // Restart app to get fresh SDK state after monitoring session
        if (wasMonitoring)
        {
            // Close the projector display window before restarting
            CloseDisplay();

            // Save auto-login marker for seamless restart
            var pending = new Helpers.PendingOperationHelper.PendingOperation
            {
                Type = "MonitorRestart",
                Username = _currentUser?.Username ?? "admin"
            };
            Helpers.PendingOperationHelper.Save(pending);

            // Show restart message before restarting
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Views.CustomMessageBox.Show(
                    LanguageManager.Instance.IsArabic
                        ? "سيتم إعادة تشغيل التطبيق لتحديث اتصال الأجهزة..."
                        : "Application will restart to refresh device connection...",
                    LanguageManager.Instance.IsArabic ? "إعادة تشغيل" : "Restarting",
                    Views.MsgType.Info,
                    System.Windows.Application.Current.MainWindow);
            });

            // Release mutex before restart so new process can acquire it
            App.ReleaseSingleInstanceMutex();

            // Brief delay to ensure cleanup completes
            await Task.Delay(1000);

            // Restart app to get fresh SDK state
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                System.Diagnostics.Process.Start(exePath);
            Environment.Exit(0);
        }
    }

    [RelayCommand]
    private async Task ToggleDisplayAsync()
    {
        ActivityLogger.LogAction("Monitor", "ToggleDisplay", _displayWindow is { IsLoaded: true } ? "close" : "open");
        if (_displayWindow is { IsLoaded: true })
        {
            _displayWindow.Close();
            _displayWindow = null;
            IsDisplayOpen = false;
        }
        else
        {
            // Get all doors for selection
            var doors = (await _doorService.GetAllDoorsAsync()).ToList();
            if (doors.Count == 0)
            {
                Views.CustomMessageBox.Show(Lang.DispNoDoors, Lang.DispOpenDisplay,
                    Views.MsgType.Warning, System.Windows.Application.Current.MainWindow);
                return;
            }

            // Show door picker dialog (multi-select)
            var dialog = new Views.SelectDoorDialog(doors)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.SelectedDoors.Count == 0)
                return;

            // Store selected doors info for filtering
            _selectedDisplayDoors.Clear();
            var allDevices = (await _deviceRepository.GetAllAsync()).ToList();
            foreach (var d in dialog.SelectedDoors)
            {
                var device = allDevices.FirstOrDefault(dev => dev.Id == d.DeviceId);
                var sn = device?.SerialNumber ?? string.Empty;
                _selectedDisplayDoors.Add((sn, d.DoorNumber, d.Name));
            }

            var doorNames = string.Join(", ", dialog.SelectedDoors.Select(d => d.Name));

            _displayWindow = new Views.MonitorDisplayWindow();
            _displayWindow.SetDoorName(doorNames);
            _displayWindow.Closed += (_, _) =>
            {
                _displayWindow = null;
                IsDisplayOpen = false;
            };
            _displayWindow.Show();
            IsDisplayOpen = true;
        }
    }

    public void CloseDisplay()
    {
        if (_displayWindow is { IsLoaded: true })
        {
            _displayWindow.Close();
            _displayWindow = null;
            IsDisplayOpen = false;
        }
    }

    private async void OnMonitorEvent(MonitorEvent evt)
    {
        try
        {
            // Find door and card in DB
            var doors = await _doorRepository.GetByDeviceSerialAsync(evt.DeviceSN);
            var door = doors?.FirstOrDefault(d => d.DoorNumber == evt.DoorNumber);
            int doorId = door?.Id ?? 0;

            int? cardId = null;
            string playerName = "";
            Domain.Entities.AccessCard? cardEntity = null;

            // Determine if this is an ENTRY event (only entries count as visits)
            bool isEntryEvent = door != null
                ? !door.Name.Contains("خروج") && !door.Name.ToLower().Contains("exit")
                : evt.ReaderType == 1;

            // Step 1: Validate card FIRST (increments visits on every swipe)
            bool validationDenied = false;
            string validationReason = "";
            if (!string.IsNullOrEmpty(evt.CardNumber))
            {
                try
                {
                    var (isValid, reason) = await _employeeService.ValidateCardOnSwipeAsync(evt.CardNumber, true);
                    validationDenied = !isValid;
                    validationReason = reason;
                    if (!isValid)
                        System.Diagnostics.Debug.WriteLine($"[Monitor] Card {evt.CardNumber} DENIED: {reason}");
                }
                catch (Exception valEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Monitor] Validation error: {valEx.Message}");
                }

                // Step 2: Re-read card entity AFTER validation (UsedVisits now updated)
                cardEntity = await _cardRepository.GetByCardNumberAsync(evt.CardNumber);
                if (cardEntity != null)
                {
                    cardId = cardEntity.Id;
                    var emp = cardEntity.Employee;
                    if (emp != null)
                        playerName = $"{emp.FullNameEn} | {emp.FullNameAr}";
                }
            }

            var lang = LanguageManager.Instance;

            // Direction = door name (e.g. "دخول"/"خروج") — matches door label exactly as user expects
            string direction = door?.Name ?? (evt.ReaderType == 1 ? lang.DispEntry : lang.DispExit);
            bool isEntry = door != null
                ? !door.Name.Contains("خروج") && !door.Name.ToLower().Contains("exit")
                : evt.ReaderType == 1;

            // Card status: from validation result + employee state (AFTER visit increment)
            string cardStatus;
            string cardStatusKey;
            if (cardEntity == null)
            {
                cardStatus = lang.DispNotRegistered;
                cardStatusKey = "NotRegistered";
            }
            else if (cardEntity.Employee == null)
            {
                cardStatus = lang.DispNotRegistered;
                cardStatusKey = "NotRegistered";
            }
            else if (cardEntity.Employee.IsFrozen)
            {
                cardStatus = lang.CardFrozen;
                cardStatusKey = "Frozen";
            }
            else if (validationDenied ||
                     cardEntity.Employee.EndDate < DateTime.Now ||
                     (cardEntity.Employee.MaxVisits > 0 && cardEntity.Employee.UsedVisits >= cardEntity.Employee.MaxVisits))
            {
                cardStatus = lang.CardExpired;
                cardStatusKey = "Expired";
            }
            else
            {
                // Show visit counter for all events in Single Device mode
                if (DeviceModeHelper.IsSingleDevice && cardEntity.Employee.MaxVisits > 0)
                    cardStatus = $"{lang.CardActive} ({cardEntity.Employee.UsedVisits}/{cardEntity.Employee.MaxVisits})";
                else
                    cardStatus = lang.CardActive;
                cardStatusKey = "Active";
            }

            string eventDesc = GetEventDescription(evt.EventCode);
            string details = $"{direction} | {eventDesc} | @{cardStatusKey} | SN:{evt.DeviceSN}";

            // Save to DB
            if (doorId > 0)
            {
                await _eventService.SaveEventAsync(doorId, cardId, evt.RecordType, evt.EventCode, evt.EventDate, details);
            }

            // Build device + door display name
            var deviceName = door?.Device?.Name ?? evt.DeviceSN;
            var doorName = door?.Name ?? $"Door {evt.DoorNumber}";

            // Detect QR code vs regular card (QR codes start with "5000")
            bool isQrCode = evt.CardNumber?.StartsWith("5000") ?? false;

            // For QR events, override player name to show "QR Guest"
            if (isQrCode && string.IsNullOrEmpty(playerName))
            {
                var lang2 = LanguageManager.Instance;
                playerName = lang2.IsArabic ? "ضيف QR" : "QR Guest";
            }

            // Combine event description with card status for display
            // Format: "Card Open (Active 3/50)" or "Card Not Found (Unregistered)"
            string eventDescription = GetEventDescription(evt.EventCode);
            string combinedStatus = !string.IsNullOrEmpty(cardStatus)
                ? $"{eventDescription} ({cardStatus})"
                : eventDescription;

            // Update UI on dispatcher thread
            var dto = new AccessEventDto
            {
                DeviceName = deviceName,
                DoorName = doorName,
                CardNumber = evt.CardNumber,
                PlayerName = playerName,
                EventType = isQrCode ? "QR" : ((Domain.Enums.RecordType)evt.RecordType).ToString(),
                EventDescription = eventDescription,
                Direction = direction,
                IsEntry = isEntry,
                CardStatus = combinedStatus,
                CardStatusKey = cardStatusKey,
                Timestamp = evt.EventDate
            };

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                LiveEvents.Insert(0, dto);
                if (LiveEvents.Count > 500) LiveEvents.RemoveAt(LiveEvents.Count - 1);
                TodayEventCount++;

                // Send to projector display window (only for selected doors)
                if (_displayWindow is { IsLoaded: true } && !string.IsNullOrEmpty(evt.CardNumber)
                    && _selectedDisplayDoors.Any(d => d.DeviceSN == evt.DeviceSN && d.DoorNumber == evt.DoorNumber))
                {
                    var matchedDoor = _selectedDisplayDoors.First(d => d.DeviceSN == evt.DeviceSN && d.DoorNumber == evt.DoorNumber);
                    _displayWindow.SetDoorName($"{deviceName} - {matchedDoor.DoorName}");

                    if (cardEntity?.Employee != null)
                        _displayWindow.ShowCardEvent(cardEntity.Employee, cardEntity, evt.CardNumber, direction, validationDenied);
                    else
                        _displayWindow.ShowUnregistered();
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorViewModel] Event processing error: {ex.Message}");
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusMessage = $"Event error: {ex.Message}";
            });
        }
    }

    private static string GetEventDescription(int code)
    {
        var lang = LanguageManager.Instance;
        return code switch
        {
            1 => lang.DispCardOpen,
            2 => lang.DispPasswordOpen,
            3 => lang.DispCardPassword,
            4 => lang.DispCardRepeat,
            5 => lang.DispExpiredCard,
            6 => lang.DispInvalidCard,
            10 => lang.DispButtonOpen,
            11 => lang.DispCardOpenAlt,
            19 => lang.DispCardRejected,
            20 => lang.DispRemoteOpen,
            21 => lang.DispRemoteClose,
            22 => lang.DispCardNotFound,
            25 => lang.DispCardExpiredHW,
            30 => lang.DispDoorOpened,
            31 => lang.DispDoorClosed,
            _ => $"Event {code}"
        };
    }
}
