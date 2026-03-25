namespace AccessControlPro.Domain.Entities;

public class QrPoolEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int Status { get; set; } // 0=Available, 1=Assigned, 2=Used, 3=Expired
    public string Source { get; set; } = "Local"; // Local or Cloud
    public string GuestName { get; set; } = "";
    public string GuestPhone { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime? AssignedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public int MaxUses { get; set; } = 2;
    public int UsedCount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string DoorPermissions { get; set; } = "01010000";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsUploadedToDevice { get; set; }
}
