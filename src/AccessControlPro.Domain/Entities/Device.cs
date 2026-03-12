using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Entities;

public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public string MAC { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public int TCPPort { get; set; } = 8000;
    public int UDPPort { get; set; } = 8101;
    public string Password { get; set; } = "FFFFFFFF";
    public string Gateway { get; set; } = "0.0.0.0";
    public string SubnetMask { get; set; } = "255.255.255.0";
    public DeviceType DeviceType { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Door> Doors { get; set; } = new List<Door>();
}
