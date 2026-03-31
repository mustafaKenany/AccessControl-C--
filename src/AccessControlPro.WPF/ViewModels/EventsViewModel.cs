using System.Collections.ObjectModel;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class EventsViewModel : ObservableObject
{
    private readonly IAccessEventService _eventService;
    private readonly IDeviceService _deviceService;
    private const int PageSize = 100;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _isDataLoaded;
    [ObservableProperty] private int _selectedPeriodIndex;     // 0=All, 1=Today, ...
    [ObservableProperty] private int _selectedEventTypeIndex;  // 0=All, 1=Card, 2=Button, ...
    [ObservableProperty] private int _selectedDeviceIndex;     // 0=All, then device list
    [ObservableProperty] private string _dateRangeText = "";

    public ObservableCollection<AccessEventDto> Events { get; } = new();

    // Device items for filter: display name + id
    public class DeviceFilterItem
    {
        public string Display { get; }
        public int? DeviceId { get; }
        public DeviceFilterItem(string display, int? deviceId) { Display = display; DeviceId = deviceId; }
        public override string ToString() => Display;
    }

    public ObservableCollection<DeviceFilterItem> DeviceFilters { get; } = new();

    // EventType index → RecordType mapping
    private static RecordType? EventTypeFromIndex(int index) => index switch
    {
        1 => RecordType.Card,
        2 => RecordType.Button,
        3 => RecordType.DoorSensor,
        4 => RecordType.Software,
        5 => RecordType.Alarm,
        6 => RecordType.System,
        _ => null
    };

    public EventsViewModel(IAccessEventService eventService, IDeviceService deviceService)
    {
        _eventService = eventService;
        _deviceService = deviceService;
    }

    partial void OnSelectedPeriodIndexChanged(int value)
    {
        UpdateDateRangeText();
    }

    private void UpdateDateRangeText()
    {
        var (from, to) = GetDateRange();
        if (from == null && to == null)
        {
            DateRangeText = "";
            return;
        }

        var fromStr = from?.ToString("yyyy-MM-dd") ?? "";
        var toStr = to?.ToString("yyyy-MM-dd") ?? (Lang.IsArabic ? "\u0627\u0644\u0622\u0646" : "Now");
        DateRangeText = $"\U0001f4c5 {fromStr} \u2192 {toStr}";
    }

    public async Task LoadDevicesAsync()
    {
        ActivityLogger.LogNavigation("Events");
        DeviceFilters.Clear();
        DeviceFilters.Add(new DeviceFilterItem(Lang.FinAll, null));
        var devices = await _deviceService.GetAllDevicesAsync();
        foreach (var d in devices)
            DeviceFilters.Add(new DeviceFilterItem(d.Name ?? d.IP, d.Id));
        SelectedDeviceIndex = 0;
    }

    private (DateTime? From, DateTime? To) GetDateRange()
    {
        var today = DateTime.UtcNow.Date;
        return SelectedPeriodIndex switch
        {
            1 => (today, today.AddDays(1).AddTicks(-1)),
            2 => (today.AddDays(-1), today.AddTicks(-1)),
            3 => (today.AddDays(-(int)today.DayOfWeek), null),
            4 => (today.AddDays(-(int)today.DayOfWeek - 7), today.AddDays(-(int)today.DayOfWeek).AddTicks(-1)),
            5 => (new DateTime(today.Year, today.Month, 1), null),
            6 => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                  new DateTime(today.Year, today.Month, 1).AddTicks(-1)),
            7 => (today.AddMonths(-3), null),
            8 => (today.AddMonths(-6), null),
            9 => (new DateTime(today.Year, 1, 1), null),
            _ => (null, null)
        };
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            await LoadPagedAsync();
    }

    [RelayCommand]
    private async Task FilterChangedAsync()
    {
        CurrentPage = 1;
        if (IsDataLoaded)
            await LoadPagedAsync();
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
    private void ClearFilter()
    {
        SelectedPeriodIndex = 0;
        SelectedEventTypeIndex = 0;
        SelectedDeviceIndex = 0;
        SearchText = string.Empty;
        CurrentPage = 1;
        if (IsDataLoaded)
            _ = LoadPagedAsync();
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
        ActivityLogger.LogAction("Events", "Load", $"page={CurrentPage} period={SelectedPeriodIndex} type={SelectedEventTypeIndex}");
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var (dateFrom, dateTo) = GetDateRange();
            var eventType = EventTypeFromIndex(SelectedEventTypeIndex);
            int? deviceId = (SelectedDeviceIndex > 0 && SelectedDeviceIndex < DeviceFilters.Count)
                ? DeviceFilters[SelectedDeviceIndex].DeviceId : null;
            var (items, totalCount) = await _eventService.GetEventsPagedAsync(
                CurrentPage, PageSize, dateFrom, dateTo, null, search, eventType, deviceId);
            var list = items.ToList();

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            Events.Clear();
            foreach (var evt in list)
            {
                // Localize action label and direction based on current language
                evt.EventDescription = Lang.GetEventActionLabel(evt.EventType);
                evt.Direction = Lang.GetDirectionLabel(evt.Direction);

                // Append localized card status to description if available
                if (!string.IsNullOrEmpty(evt.CardStatus))
                {
                    var localizedStatus = Lang.GetCardStatusLabel(evt.CardStatus);
                    evt.EventDescription = $"{evt.EventDescription} ({localizedStatus})";
                }

                Events.Add(evt);
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
