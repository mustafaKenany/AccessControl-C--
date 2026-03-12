using System.Net;

namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Validates device network parameters before they're sent to SDK
/// Prevents crashes from invalid IPs, ports, or gateway settings
/// </summary>
public interface IDeviceParameterValidator
{
    /// <summary>
    /// Validates IP address format. Returns (isValid, errorMessage)
    /// </summary>
    (bool isValid, string? error) ValidateIPAddress(string ipAddress);

    /// <summary>
    /// Validates subnet mask format (must be valid CIDR netmask)
    /// </summary>
    (bool isValid, string? error) ValidateSubnetMask(string subnetMask);

    /// <summary>
    /// Validates TCP/UDP port range (1-65535)
    /// </summary>
    (bool isValid, string? error) ValidatePort(int port, string portType = "Port");

    /// <summary>
    /// Validates MAC address format (XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX)
    /// </summary>
    (bool isValid, string? error) ValidateMACAddress(string macAddress);

    /// <summary>
    /// Comprehensive validation of all device network parameters
    /// </summary>
    (bool isValid, List<string> errors) ValidateDeviceNetworkConfig(string ip, int tcpPort, int udpPort, string gateway, string subnetMask, string mac);
}

public class DeviceParameterValidator : IDeviceParameterValidator
{
    public (bool isValid, string? error) ValidateIPAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return (false, "IP address cannot be empty.");

        if (!IPAddress.TryParse(ipAddress, out var parsedIp))
            return (false, $"Invalid IP address format: {ipAddress}");

        // Reject loopback in device context (device should have real IP)
        if (IPAddress.IsLoopback(parsedIp))
            return (false, "Loopback address (127.x.x.x) not allowed for devices.");

        // Reject invalid ranges
        if (parsedIp.ToString() == "0.0.0.0")
            return (false, "0.0.0.0 is not a valid device IP address.");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateSubnetMask(string subnetMask)
    {
        if (string.IsNullOrWhiteSpace(subnetMask))
            return (false, "Subnet mask cannot be empty.");

        var validMasks = new[]
        {
            "255.255.255.0", "255.255.255.128", "255.255.255.192", "255.255.255.224",
            "255.255.255.240", "255.255.255.248", "255.255.255.252", "255.255.255.255",
            "255.255.254.0", "255.255.252.0", "255.255.248.0", "255.255.240.0",
            "255.255.0.0", "255.254.0.0", "255.0.0.0"
        };

        if (!validMasks.Contains(subnetMask))
            return (false, $"Invalid subnet mask: {subnetMask}");

        return (true, null);
    }

    public (bool isValid, string? error) ValidatePort(int port, string portType = "Port")
    {
        if (port < 1 || port > 65535)
            return (false, $"{portType} must be between 1 and 65535 (got {port})");

        // Reject reserved ports below 1024
        if (port < 1024)
            return (false, $"{portType} {port} is reserved (< 1024). Use port 1024 or higher.");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateMACAddress(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return (false, "MAC address cannot be empty.");

        // Allow XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX format
        var normalizedMac = macAddress.Replace(":", "-").ToUpper();

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedMac, @"^([0-9A-F]{2}-){5}([0-9A-F]{2})$"))
            return (false, $"Invalid MAC address format: {macAddress}. Use XX:XX:XX:XX:XX:XX format.");

        return (true, null);
    }

    public (bool isValid, List<string> errors) ValidateDeviceNetworkConfig(string ip, int tcpPort, int udpPort, string gateway, string subnetMask, string mac)
    {
        var errors = new List<string>();

        var (ipValid, ipError) = ValidateIPAddress(ip);
        if (!ipValid) errors.Add(ipError!);

        var (tcpValid, tcpError) = ValidatePort(tcpPort, "TCP Port");
        if (!tcpValid) errors.Add(tcpError!);

        var (udpValid, udpError) = ValidatePort(udpPort, "UDP Port");
        if (!udpValid) errors.Add(udpError!);

        // Gateway can be 0.0.0.0 for DHCP, but if set should be valid IP
        if (gateway != "0.0.0.0")
        {
            var (gwValid, gwError) = ValidateIPAddress(gateway);
            if (!gwValid) errors.Add($"Gateway: {gwError}");
        }

        var (maskValid, maskError) = ValidateSubnetMask(subnetMask);
        if (!maskValid) errors.Add(maskError!);

        if (!string.IsNullOrEmpty(mac))
        {
            var (macValid, macError) = ValidateMACAddress(mac);
            if (!macValid) errors.Add(macError!);
        }

        return (errors.Count == 0, errors);
    }
}
