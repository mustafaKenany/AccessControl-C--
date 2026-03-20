namespace AccessControlPro.Application.DTOs;

public class QrPassDto
{
    public int Id { get; set; }
    public string PassCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int MaxUses { get; set; } = 5;
    public int UsedCount { get; set; }
    public int RemainingUses => Math.Max(0, MaxUses - UsedCount);
    public decimal Fee { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? DeviceId { get; set; }
    public int DoorNumber { get; set; } = 1;
    public string DeviceName { get; set; } = string.Empty;

    public string StatusLabel => IsActive && ValidTo >= DateTime.Now && UsedCount < MaxUses
        ? "Active" : "Expired";
    public string StatusColor => StatusLabel == "Active" ? "#2ED47A" : "#F7685B";
}
