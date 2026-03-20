using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Entities;

public class Door
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public int DoorNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DoorStatus Status { get; set; } = DoorStatus.Closed;
    public bool IsLocked { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // Working schedule
    public TimeSpan WorkStartTime { get; set; } = TimeSpan.Zero; // 00:00 = 24h mode
    public TimeSpan WorkEndTime { get; set; } = new TimeSpan(23, 59, 59); // 23:59:59
    public bool Is24Hours { get; set; } = true;
    public string WorkingDays { get; set; } = "1,2,3,4,5,6,7"; // 1=Mon...7=Sun, comma separated

    public Device Device { get; set; } = null!;
    public ICollection<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();
}
