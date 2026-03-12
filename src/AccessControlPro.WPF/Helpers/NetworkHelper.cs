using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AccessControlPro.WPF.Helpers;

public static class NetworkHelper
{
    /// <summary>
    /// Returns true if WiFi and Ethernet are both active AND on the same subnet.
    /// Different subnets = safe, no warning needed.
    /// </summary>
    public static bool IsWifiOnSameSubnetAsEthernet()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            var wifiAddresses = interfaces
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            var ethernetAddresses = interfaces
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            if (wifiAddresses.Count == 0 || ethernetAddresses.Count == 0)
                return false; // One of them is not active — no conflict

            // Compare network addresses (IP & SubnetMask)
            foreach (var wifi in wifiAddresses)
            {
                var wifiNetwork = GetNetworkAddress(wifi.Address, wifi.IPv4Mask);
                foreach (var eth in ethernetAddresses)
                {
                    var ethNetwork = GetNetworkAddress(eth.Address, eth.IPv4Mask);
                    if (wifiNetwork.Equals(ethNetwork))
                        return true; // Same subnet — conflict!
                }
            }

            return false; // Different subnets — safe
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pings a device IP with a short timeout.
    /// Returns true if device responds.
    /// </summary>
    public static async Task<bool> PingDeviceAsync(string ip, int timeoutMs = 2000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static IPAddress GetNetworkAddress(IPAddress address, IPAddress mask)
    {
        var ipBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var networkBytes = new byte[ipBytes.Length];

        for (int i = 0; i < ipBytes.Length; i++)
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);

        return new IPAddress(networkBytes);
    }
}
