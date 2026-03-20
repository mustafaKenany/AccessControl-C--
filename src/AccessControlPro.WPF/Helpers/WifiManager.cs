using System.Diagnostics;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Disables WiFi adapter to prevent routing conflicts with Ethernet.
/// SDK communicates with devices via TCP on Ethernet subnet.
/// If WiFi is on the same subnet, Windows may route SDK packets through WiFi → SDK fails.
/// </summary>
public static class WifiManager
{
    /// <summary>
    /// Disable all WiFi adapters using netsh.
    /// </summary>
    public static void DisableWifi()
    {
        try
        {
            // Get all WiFi adapter names
            var adapters = GetWifiAdapterNames();
            foreach (var adapter in adapters)
            {
                RunNetsh($"interface set interface \"{adapter}\" admin=disable");
                ActivityLogger.LogAction("System", "DisableWifi", $"Disabled: {adapter}");
            }
        }
        catch (Exception ex)
        {
            ActivityLogger.LogError("System", "DisableWifi", ex.Message);
        }
    }

    /// <summary>
    /// Enable all WiFi adapters.
    /// </summary>
    public static void EnableWifi()
    {
        try
        {
            var adapters = GetWifiAdapterNames();
            foreach (var adapter in adapters)
            {
                RunNetsh($"interface set interface \"{adapter}\" admin=enable");
                ActivityLogger.LogAction("System", "EnableWifi", $"Enabled: {adapter}");
            }
        }
        catch (Exception ex)
        {
            ActivityLogger.LogError("System", "EnableWifi", ex.Message);
        }
    }

    /// <summary>
    /// Check if any WiFi adapter is currently enabled.
    /// </summary>
    public static bool IsWifiEnabled()
    {
        try
        {
            var output = RunNetshOutput("wlan show interfaces");
            return output.Contains("State") && output.Contains("connected");
        }
        catch
        {
            return false;
        }
    }

    private static List<string> GetWifiAdapterNames()
    {
        var names = new List<string>();
        try
        {
            var output = RunNetshOutput("interface show interface");
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                // Look for wireless/WiFi adapters
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // netsh output: "Enabled  Connected  Dedicated  Wi-Fi"
                // Parse adapter name from the last column
                if (trimmed.Contains("Wi-Fi") || trimmed.Contains("WiFi") ||
                    trimmed.Contains("Wireless") || trimmed.Contains("WLAN") ||
                    trimmed.Contains("واي فاي"))
                {
                    // Extract adapter name (last column after whitespace groups)
                    var parts = trimmed.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                        names.Add(parts[^1].Trim());
                    else if (parts.Length >= 1)
                        names.Add(parts[^1].Trim());
                }
            }
        }
        catch { }

        // Fallback: common WiFi adapter names
        if (names.Count == 0)
            names.Add("Wi-Fi");

        return names;
    }

    private static void RunNetsh(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi);
        proc?.WaitForExit(5000);
    }

    private static string RunNetshOutput(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi);
        if (proc == null) return "";
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return output;
    }
}
