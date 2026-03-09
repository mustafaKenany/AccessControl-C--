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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Device Device { get; set; } = null!;
    public ICollection<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();
}
