namespace AccessControlPro.Application.DTOs;

public class AccessCardDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string CardPassword { get; set; } = string.Empty;
    public string CardType { get; set; } = "Standard";
    public int OpenMode { get; set; }                  // 0=Ordinary, 1=FirstCard, 2=AlwaysOpen, 3=Patrol, 4=AntiTheft
    public string DoorPermissions { get; set; } = "01000000";
    public int EffectiveTimes { get; set; } = 65535;
    public int TimePeriodIndex { get; set; } = 1;
    public bool HolidayEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSyncedToDevice { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}
