using System.IO;
using System.Text.Json;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Reads DeviceMode from appsettings.json.
/// "Single" = one device (MaxVisits enabled, effectiveTimes from MaxVisits)
/// "Multi"  = multiple devices (MaxVisits hidden, effectiveTimes = 65535)
/// </summary>
public static class DeviceModeHelper
{
    private static string? _cachedMode;

    /// <summary>True if Single Device mode (MaxVisits features enabled)</summary>
    public static bool IsSingleDevice => GetMode() == "Single";

    /// <summary>True if Multi Device mode (MaxVisits hidden, date-only)</summary>
    public static bool IsMultiDevice => GetMode() == "Multi";

    public static string GetMode()
    {
        if (_cachedMode != null) return _cachedMode;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("DeviceMode", out var mode))
                {
                    _cachedMode = mode.GetString() ?? "Single";
                    return _cachedMode;
                }
            }
        }
        catch { }

        _cachedMode = "Single"; // Default
        return _cachedMode;
    }

    /// <summary>
    /// Save DeviceMode to appsettings.json
    /// </summary>
    public static void SetMode(string mode)
    {
        _cachedMode = mode;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            root["DeviceMode"] = mode;
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));
        }
        catch { }
    }

    /// <summary>Clear cache (for testing or after settings change)</summary>
    public static void ClearCache() => _cachedMode = null;
}
