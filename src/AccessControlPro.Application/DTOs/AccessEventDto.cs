namespace AccessControlPro.Application.DTOs;

public class AccessEventDto
{
    public int Id { get; set; }
    public string DoorName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
