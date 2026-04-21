namespace AccessControlPro.Application.DTOs;

public class DeviceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public string MAC { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public int TCPPort { get; set; }
    public string Gateway { get; set; } = "0.0.0.0";
    public string SubnetMask { get; set; } = "255.255.255.0";
    public bool IsOnline { get; set; }
    public int DoorCount { get; set; }
    public int CardCount { get; set; }
}
