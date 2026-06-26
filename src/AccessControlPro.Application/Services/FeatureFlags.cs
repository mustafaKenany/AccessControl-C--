using System.Text.Json;

namespace AccessControlPro.Application.Services;

/// <summary>
/// SuperAdmin-controlled feature flags (POS module, online subscription), cached next to the app
/// so the POS process and offline launches can read them without a live cloud call. Updated from
/// the cloud lock-status poll (authoritative for online gyms) or set by the SuperAdmin step-up
/// password (offline gyms). Stored as <c>.feature_flags</c> in the install folder shared by all
/// three apps (WPF / Admin / POS).
/// </summary>
public static class FeatureFlags
{
    private static readonly string FilePath =
        System.IO.Path.Combine(AppContext.BaseDirectory, ".feature_flags");

    private class Flags
    {
        public bool PosEnabled { get; set; }              // default OFF — a new install needs SuperAdmin to enable POS
        public bool OnlineEnabled { get; set; } = true;   // default ON
        public string Source { get; set; } = "";          // "cloud" or "local"
    }

    private static Flags Load()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
                return JsonSerializer.Deserialize<Flags>(System.IO.File.ReadAllText(FilePath)) ?? new Flags();
        }
        catch { }
        return new Flags();
    }

    private static void Save(Flags f)
    {
        try { System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(f)); } catch { }
    }

    public static bool IsPosEnabled() => Load().PosEnabled;
    public static bool IsOnlineEnabled() => Load().OnlineEnabled;
    public static bool HasState() => System.IO.File.Exists(FilePath);

    /// <summary>Authoritative update from a successful cloud poll (online gyms).</summary>
    public static void UpdateFromCloud(bool posEnabled, bool onlineEnabled)
        => Save(new Flags { PosEnabled = posEnabled, OnlineEnabled = onlineEnabled, Source = "cloud" });

    /// <summary>SuperAdmin step-up override (offline gyms). Null leaves a flag unchanged.</summary>
    public static void SetLocal(bool? posEnabled = null, bool? onlineEnabled = null)
    {
        var f = Load();
        if (posEnabled.HasValue) f.PosEnabled = posEnabled.Value;
        if (onlineEnabled.HasValue) f.OnlineEnabled = onlineEnabled.Value;
        f.Source = "local";
        Save(f);
    }
}
