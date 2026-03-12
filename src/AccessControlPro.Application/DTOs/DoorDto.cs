namespace AccessControlPro.Application.DTOs;

public class DoorDto
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIP { get; set; } = string.Empty;
    public int DoorNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Closed";
    public bool IsLocked { get; set; } = true;
}
