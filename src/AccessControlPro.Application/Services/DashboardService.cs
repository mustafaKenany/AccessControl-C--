using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAccessEventRepository _eventRepository;

    public DashboardService(IDeviceRepository deviceRepository, IAccessEventRepository eventRepository)
    {
        _deviceRepository = deviceRepository;
        _eventRepository = eventRepository;
    }

    public async Task<DashboardDto> GetDashboardDataAsync()
    {
        // Parallel fetch — all 5 queries are independent (each gets its own DbContext)
        var devicesTask = _deviceRepository.GetAllAsync();
        var recentEventsTask = _eventRepository.GetRecentAsync(20);
        var todayCountTask = _eventRepository.GetTodayCountAsync();
        var alarmCountTask = _eventRepository.GetActiveAlarmCountAsync();
        var onlineCountTask = _deviceRepository.GetOnlineCountAsync();

        await Task.WhenAll(devicesTask, recentEventsTask, todayCountTask, alarmCountTask, onlineCountTask);

        var devices = await devicesTask;
        var recentEvents = await recentEventsTask;
        var todayCount = await todayCountTask;
        var alarmCount = await alarmCountTask;
        var onlineCount = await onlineCountTask;

        var allDoors = devices.SelectMany(d => d.Doors).ToList();

        return new DashboardDto
        {
            TotalDevices = devices.Count(),
            OnlineDevices = onlineCount,
            TotalDoors = allDoors.Count,
            TodayEvents = todayCount,
            ActiveAlarms = alarmCount,
            RecentEvents = recentEvents.Select(e => new AccessEventDto
            {
                Id = e.Id,
                DoorName = e.Door?.Name ?? "Unknown",
                CardNumber = e.Card?.CardNumber ?? "-",
                EventType = e.EventType.ToString(),
                EventDescription = e.EventCode.ToString(),
                Timestamp = e.Timestamp
            }).ToList(),
            DoorStatuses = allDoors.Select(d => new DoorStatusDto
            {
                DoorId = d.Id,
                DoorName = d.Name,
                DeviceName = d.Device?.Name ?? "Unknown",
                Status = d.Status.ToString(),
                IsLocked = d.IsLocked
            }).ToList()
        };
    }
}
