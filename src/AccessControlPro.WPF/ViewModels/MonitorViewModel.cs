using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
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
        IEmployeeService employeeService)
    {
        _sdk = sdk;
        _deviceRepository = deviceRepository;
        _doorRepository = doorRepository;
        _cardRepository = cardRepository;
        _eventService = eventService;
        _doorService = doorService;
        _employeeService = employeeService;
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        try
        {
            var devices = (await _deviceRepository.GetAllAsync()).Where(d => d.IsOnline).ToList();
            if (devices.Count == 0)
            {
                StatusMessage = "No online devices found";
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

            _sdk.StartMonitoring(deviceInfos, OnMonitorEvent);
            ConnectedDevices = devices.Count;
            IsMonitoring = true;
            StatusMessage = $"Monitoring {devices.Count} device(s)...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _sdk.StopMonitoring();
        IsMonitoring = false;
        ConnectedDevices = 0;
        StatusMessage = "Stopped";
    }

    [RelayCommand]
    private async Task ToggleDisplayAsync()
    {
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
            if (!string.IsNullOrEmpty(evt.CardNumber))
            {
                cardEntity = await _cardRepository.GetByCardNumberAsync(evt.CardNumber);
                if (cardEntity != null)
                {
                    cardId = cardEntity.Id;
                    playerName = cardEntity.Employee?.FullNameEn ?? "";
                }
            }

            string direction = evt.ReaderType == 1 ? "Entry" : "Exit";
            string details = $"{direction} | SN:{evt.DeviceSN}";

            // Save to DB
            if (doorId > 0)
            {
                await _eventService.SaveEventAsync(doorId, cardId, evt.RecordType, evt.EventCode, evt.EventDate, details);
            }

            // Increment visit count for successful card entry events
            if (evt.EventCode == 1 && !string.IsNullOrEmpty(evt.CardNumber))
            {
                try
                {
                    await _employeeService.IncrementVisitAsync(evt.CardNumber);
                }
                catch (Exception visitEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MonitorViewModel] Visit increment error: {visitEx.Message}");
                }
            }

            // Update UI on dispatcher thread
            var dto = new AccessEventDto
            {
                DeviceName = evt.DeviceSN,
                DoorName = door?.Name ?? $"Door {evt.DoorNumber}",
                CardNumber = evt.CardNumber,
                PlayerName = playerName,
                EventType = ((Domain.Enums.RecordType)evt.RecordType).ToString(),
                EventDescription = GetEventDescription(evt.EventCode),
                Direction = direction,
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
                    // Show the matching door name on the display
                    var matchedDoor = _selectedDisplayDoors.First(d => d.DeviceSN == evt.DeviceSN && d.DoorNumber == evt.DoorNumber);
                    _displayWindow.SetDoorName(matchedDoor.DoorName);

                    if (cardEntity?.Employee != null)
                        _displayWindow.ShowCardEvent(cardEntity.Employee, cardEntity, evt.CardNumber);
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

    private static string GetEventDescription(int code) => code switch
    {
        1 => "Card Open",
        2 => "Password Open",
        3 => "Card + Password",
        4 => "Card Repeat",
        5 => "Card Expired",
        6 => "Invalid Card",
        10 => "Button Open",
        20 => "Remote Open",
        21 => "Remote Close",
        30 => "Door Opened",
        31 => "Door Closed",
        40 => "Fire Alarm",
        41 => "Police Alarm",
        50 => "System Startup",
        51 => "System Restart",
        _ => $"Event {code}"
    };
}
