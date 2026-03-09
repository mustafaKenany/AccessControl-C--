namespace AccessControlPro.Domain.Entities;

public class AccessCard
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string CardPassword { get; set; } = string.Empty;
    public string CardType { get; set; } = "Standard";
    public int OpenMode { get; set; }              // 0=Ordinary, 1=FirstCard, 2=AlwaysOpen, 3=Patrol, 4=AntiTheft
    public string DoorPermissions { get; set; } = "01000000"; // 8-char: "01"=enabled per door
    public int EffectiveTimes { get; set; } = 65535; // 0=immediate, 1-1000=count, 65535=unlimited
    public int TimePeriodIndex { get; set; } = 1;   // 1-64
    public bool HolidayEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSyncedToDevice { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = null!;
    public ICollection<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();
}
