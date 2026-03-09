namespace AccessControlPro.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;       // "Create", "Update", "Delete", "SyncCard"
    public string EntityType { get; set; } = string.Empty;   // "Employee", "AccessCard", "Device"
    public int? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
