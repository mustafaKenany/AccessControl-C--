namespace AccessControlPro.Domain.Entities;

public class QrPass
{
    public int Id { get; set; }
    public string PassCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int MaxUses { get; set; } = 5;
    public int UsedCount { get; set; }
    public decimal Fee { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? DeviceId { get; set; }
    public int DoorNumber { get; set; } = 1;
    public string DeviceName { get; set; } = string.Empty;
}
