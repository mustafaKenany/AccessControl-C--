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
        var devices = await _deviceRepository.GetAllAsync();
        var recentEvents = await _eventRepository.GetRecentAsync(20);
        var todayCount = await _eventRepository.GetTodayCountAsync();
        var alarmCount = await _eventRepository.GetActiveAlarmCountAsync();
        var onlineCount = await _deviceRepository.GetOnlineCountAsync();

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
