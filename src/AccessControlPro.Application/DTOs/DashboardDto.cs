namespace AccessControlPro.Application.DTOs;

public class DashboardDto
{
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int TotalDoors { get; set; }
    public int TodayEvents { get; set; }
    public int ActiveAlarms { get; set; }
    public List<AccessEventDto> RecentEvents { get; set; } = new();
    public List<DoorStatusDto> DoorStatuses { get; set; } = new();
}

public class DoorStatusDto
{
    public int DoorId { get; set; }
    public string DoorName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Status { get; set; } = "Closed";
    public bool IsLocked { get; set; } = true;
}
