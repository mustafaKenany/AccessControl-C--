using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Entities;

public class AccessEvent
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public int? CardId { get; set; }
    public RecordType EventType { get; set; }
    public EventCode EventCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Details { get; set; } = string.Empty;

    public Door Door { get; set; } = null!;
    public AccessCard? Card { get; set; }
}
