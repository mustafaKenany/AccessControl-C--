namespace AccessControlPro.Application.DTOs;

public class DoorDto
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIP { get; set; } = string.Empty;
    public int DoorNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Closed";
    public bool IsLocked { get; set; } = true;

    // Working schedule
    public TimeSpan WorkStartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan WorkEndTime { get; set; } = new TimeSpan(23, 59, 59);
    public bool Is24Hours { get; set; } = true;
    public string WorkingDays { get; set; } = "1,2,3,4,5,6,7";
}
