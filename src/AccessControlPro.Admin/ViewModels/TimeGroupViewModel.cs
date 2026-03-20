using System.Collections.ObjectModel;
using System.Text.Json;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class TimeGroupViewModel : ObservableObject
{
    private readonly ITimeGroupService _timeGroupService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAccessControlSdk _sdk;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private TimeGroupDisplayItem? _selectedTimeGroup;

    // Edit panel
    [ObservableProperty] private string _editNameEn = "";
    [ObservableProperty] private string _editNameAr = "";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private int? _editingId;

    // Schedule editor - 7 days, each with enabled toggle and up to 3 segments
    public ObservableCollection<DayScheduleItem> DaySchedules { get; } = new();

    public ObservableCollection<TimeGroupDisplayItem> TimeGroups { get; } = new();

    private static readonly string[] DayKeysEn = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public TimeGroupViewModel(ITimeGroupService timeGroupService, IDeviceRepository deviceRepository, IAccessControlSdk sdk)
    {
        _timeGroupService = timeGroupService;
        _deviceRepository = deviceRepository;
        _sdk = sdk;
        InitDaySchedules();
    }

    private void InitDaySchedules()
    {
        DaySchedules.Clear();
        var dayNames = new[]
        {
            (Lang.Monday, "Mon"), (Lang.Tuesday, "Tue"), (Lang.Wednesday, "Wed"),
            (Lang.Thursday, "Thu"), (Lang.Friday, "Fri"), (Lang.Saturday, "Sat"), (Lang.Sunday, "Sun")
        };

        foreach (var (name, key) in dayNames)
        {
            DaySchedules.Add(new DayScheduleItem
            {
                DayName = name,
                DayKey = key,
                IsEnabled = true,
                Segments = new ObservableCollection<TimeSegmentItem>
                {
                    new() { StartHour = "00", StartMinute = "00", EndHour = "23", EndMinute = "59" }
                }
            });
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var groups = await _timeGroupService.GetAllAsync();
            TimeGroups.Clear();
            foreach (var g in groups)
            {
                TimeGroups.Add(new TimeGroupDisplayItem
                {
                    Id = g.Id,
                    NameEn = g.NameEn,
                    NameAr = g.NameAr,
                    HardwareIndex = g.HardwareIndex,
                    IsDefault = g.IsDefault,
                    ScheduleJson = g.ScheduleJson,
                    ScheduleSummary = BuildScheduleSummary(g.ScheduleJson)
                });
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

    partial void OnSelectedTimeGroupChanged(TimeGroupDisplayItem? value)
    {
        if (value != null)
        {
            EditNameEn = value.NameEn;
            EditNameAr = value.NameAr;
            EditingId = value.Id;
            IsEditing = true;
            LoadScheduleIntoEditor(value.ScheduleJson);
        }
        else
        {
            ClearEditFields();
        }
    }

    private void LoadScheduleIntoEditor(string scheduleJson)
    {
        Dictionary<string, List<string>>? schedule = null;
        if (!string.IsNullOrWhiteSpace(scheduleJson) && scheduleJson != "{}")
        {
            try { schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleJson); }
            catch { schedule = null; }
        }

        foreach (var day in DaySchedules)
        {
            day.Segments.Clear();

            if (schedule != null && schedule.TryGetValue(day.DayKey, out var segs) && segs.Count > 0)
            {
                day.IsEnabled = true;
                foreach (var seg in segs)
                {
                    var parts = seg.Split('-');
                    if (parts.Length == 2)
                    {
                        var start = parts[0].Trim().Split(':');
                        var end = parts[1].Trim().Split(':');
                        day.Segments.Add(new TimeSegmentItem
                        {
                            StartHour = start.Length > 0 ? start[0] : "00",
                            StartMinute = start.Length > 1 ? start[1] : "00",
                            EndHour = end.Length > 0 ? end[0] : "23",
                            EndMinute = end.Length > 1 ? end[1] : "59"
                        });
                    }
                }
                if (day.Segments.Count == 0)
                    day.Segments.Add(new TimeSegmentItem { StartHour = "00", StartMinute = "00", EndHour = "23", EndMinute = "59" });
            }
            else
            {
                // No entry = 24 hours (full access for that day)
                day.IsEnabled = true;
                day.Segments.Add(new TimeSegmentItem { StartHour = "00", StartMinute = "00", EndHour = "23", EndMinute = "59" });
            }
        }
    }

    private string BuildScheduleJson()
    {
        var dict = new Dictionary<string, List<string>>();
        bool hasCustomSchedule = false;

        foreach (var day in DaySchedules)
        {
            if (!day.IsEnabled)
            {
                dict[day.DayKey] = new List<string>();
                hasCustomSchedule = true;
            }
            else
            {
                var segs = new List<string>();
                foreach (var seg in day.Segments)
                {
                    var s = $"{seg.StartHour.PadLeft(2, '0')}:{seg.StartMinute.PadLeft(2, '0')}-{seg.EndHour.PadLeft(2, '0')}:{seg.EndMinute.PadLeft(2, '0')}";
                    segs.Add(s);
                }

                // Check if it's NOT default 24h
                if (segs.Count != 1 || segs[0] != "00:00-23:59")
                    hasCustomSchedule = true;

                dict[day.DayKey] = segs;
            }
        }

        if (!hasCustomSchedule) return "{}";
        return JsonSerializer.Serialize(dict);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditNameEn) && string.IsNullOrWhiteSpace(EditNameAr))
        {
            CustomMessageBox.Show(Lang.TgNameRequired, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        try
        {
            var scheduleJson = BuildScheduleJson();

            if (IsEditing && EditingId.HasValue)
            {
                await _timeGroupService.UpdateAsync(EditingId.Value, EditNameEn, EditNameAr, scheduleJson);
            }
            else
            {
                await _timeGroupService.CreateAsync(EditNameEn, EditNameAr, scheduleJson);
            }

            CustomMessageBox.Show(Lang.TgSaved, Lang.TgTitle, MsgType.Success);
            ClearEditFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedTimeGroup == null) return;

        if (SelectedTimeGroup.IsDefault)
        {
            CustomMessageBox.Show(Lang.TgCannotDeleteDefault, Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        if (!CustomMessageBox.Confirm(Lang.TgDeleteConfirm,
            Lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            await _timeGroupService.DeleteAsync(SelectedTimeGroup.Id);
            CustomMessageBox.Show(Lang.TgDeleted, Lang.TgTitle, MsgType.Success);
            ClearEditFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task SyncToDeviceAsync()
    {
        if (SelectedTimeGroup == null) return;

        try
        {
            var devices = await _deviceRepository.GetAllAsync();
            if (!devices.Any())
            {
                CustomMessageBox.Show(
                    Lang.IsArabic ? "لا توجد أجهزة." : "No devices found.",
                    Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            var timePieces = _timeGroupService.BuildTimePiecesString(SelectedTimeGroup.ScheduleJson);

            int synced = 0, failed = 0;
            foreach (var device in devices)
            {
                try
                {
                    var devInfo = new DeviceInfo
                    {
                        IP = device.IP,
                        TCPPort = device.TCPPort,
                        SerialNumber = device.SerialNumber,
                        Password = device.Password,
                        MAC = device.MAC,
                    };
                    _sdk.SetOpeningHours(devInfo, SelectedTimeGroup.HardwareIndex, timePieces);
                    synced++;
                }
                catch
                {
                    failed++;
                }
            }

            var msg = Lang.IsArabic
                ? $"تمت المزامنة: {synced}، فشل: {failed}"
                : $"Synced: {synced}, Failed: {failed}";
            CustomMessageBox.Show(msg, Lang.TgSyncToDevice,
                failed > 0 ? MsgType.Warning : MsgType.Success);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearEditFields();
    }

    [RelayCommand]
    private void AddNewTimeGroup()
    {
        ClearEditFields();
        EditNameEn = "";
        EditNameAr = "";
        IsEditing = false;
        EditingId = null;
        InitDaySchedules();
    }

    private void ClearEditFields()
    {
        EditNameEn = "";
        EditNameAr = "";
        SelectedTimeGroup = null;
        IsEditing = false;
        EditingId = null;
        InitDaySchedules();
    }

    private string BuildScheduleSummary(string scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson) || scheduleJson == "{}")
            return Lang.TgFullAccess;

        try
        {
            var schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleJson);
            if (schedule == null) return Lang.TgFullAccess;

            int activeDays = 0;
            int totalSegments = 0;
            foreach (var day in schedule)
            {
                if (day.Value.Count > 0)
                {
                    activeDays++;
                    totalSegments += day.Value.Count;
                }
            }

            if (activeDays == 0) return Lang.TgFullAccess;
            return $"{activeDays} {(Lang.IsArabic ? "أيام" : "days")}, {totalSegments} {Lang.TgSegments}";
        }
        catch
        {
            return Lang.TgFullAccess;
        }
    }
}

public class TimeGroupDisplayItem
{
    public int Id { get; set; }
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public int HardwareIndex { get; set; }
    public bool IsDefault { get; set; }
    public string ScheduleJson { get; set; } = "{}";
    public string ScheduleSummary { get; set; } = "";

    public string DisplayName => LanguageManager.Instance.IsArabic && !string.IsNullOrWhiteSpace(NameAr)
        ? NameAr : NameEn;
}

public partial class DayScheduleItem : ObservableObject
{
    [ObservableProperty] private string _dayName = "";
    [ObservableProperty] private string _dayKey = "";
    [ObservableProperty] private bool _isEnabled = true;

    public ObservableCollection<TimeSegmentItem> Segments { get; set; } = new();

    [RelayCommand]
    private void AddSegment()
    {
        if (Segments.Count < 3)
            Segments.Add(new TimeSegmentItem { StartHour = "09", StartMinute = "00", EndHour = "17", EndMinute = "00" });
    }

    [RelayCommand]
    private void RemoveSegment(TimeSegmentItem? segment)
    {
        if (segment != null && Segments.Count > 1)
            Segments.Remove(segment);
    }
}

public partial class TimeSegmentItem : ObservableObject
{
    [ObservableProperty] private string _startHour = "00";
    [ObservableProperty] private string _startMinute = "00";
    [ObservableProperty] private string _endHour = "23";
    [ObservableProperty] private string _endMinute = "59";

    public static List<string> Hours { get; } =
        Enumerable.Range(0, 24).Select(h => h.ToString("00")).ToList();

    public static List<string> Minutes { get; } = new() { "00", "15", "30", "45" };
}
