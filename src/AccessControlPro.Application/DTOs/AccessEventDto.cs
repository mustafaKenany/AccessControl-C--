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
    public string Direction { get; set; } = string.Empty; // Door name (e.g. دخول / خروج)
    public bool IsEntry { get; set; } = true;              // True = entry, False = exit (for color coding)
    public string CardStatus { get; set; } = string.Empty; // Bilingual display: Active/فعال, Expired/منتهي, etc.
    public string CardStatusKey { get; set; } = string.Empty; // English key for color triggers: Active/Expired/Frozen/NotRegistered
    public DateTime Timestamp { get; set; }
}
