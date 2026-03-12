namespace AccessControlPro.Application.DTOs;

public class AccessEventDto
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DoorName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // Entry / Exit
    public string CardStatus { get; set; } = string.Empty; // Registered / Expired / Unregistered
    public DateTime Timestamp { get; set; }
}
