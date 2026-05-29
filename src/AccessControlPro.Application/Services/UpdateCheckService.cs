using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessControlPro.Application.Services;

public class VersionManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("sizeBytes")] public long SizeBytes { get; set; }
    [JsonPropertyName("releaseNotes_en")] public string ReleaseNotesEn { get; set; } = "";
    [JsonPropertyName("releaseNotes_ar")] public string ReleaseNotesAr { get; set; } = "";
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("minVersion")] public string MinVersion { get; set; } = "";
    [JsonPropertyName("releasedAt")] public DateTime? ReleasedAt { get; set; }
}

public class UpdateCheckResult
{
    public bool UpdateAvailable { get; set; }
    public bool IsMandatory { get; set; }
    public Version? CurrentVersion { get; set; }
    public Version? LatestVersion { get; set; }
    public VersionManifest? Manifest { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
    bool IsSnoozed(string version);
    void SnoozeVersion(string version, TimeSpan duration);
    void SkipVersion(string version);
}

// Checks the VPS for a new release manifest and decides whether to surface a
// prompt to the customer. Pure read-only — never installs anything (that's
// UpdateInstallerService's job).
public class UpdateCheckService : IUpdateCheckService
{
    private static readonly string SnoozePath = Path.Combine(AppContext.BaseDirectory, ".update_snooze");
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var result = new UpdateCheckResult();
        try
        {
            var url = LoadVersionCheckUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                result.ErrorMessage = "No CloudSyncUrl configured — cannot check for updates";
                return result;
            }

            using var http = new HttpClient { Timeout = HttpTimeout };
            using var resp = await http.GetAsync(url, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // VPS hasn't published a manifest yet — not an error, just no updates
                return result;
            }
            if (!resp.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"Version check returned HTTP {(int)resp.StatusCode}";
                return result;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var manifest = JsonSerializer.Deserialize<VersionManifest>(json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                result.ErrorMessage = "Manifest missing version field";
                return result;
            }

            // Reject HTTP downloads — only allow HTTPS to prevent a man-in-the-middle
            // from injecting a malicious update.
            if (!manifest.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "Manifest downloadUrl must be HTTPS";
                return result;
            }

            var current = GetCurrentAppVersion();
            var latest = ParseVersion(manifest.Version);
            result.CurrentVersion = current;
            result.LatestVersion = latest;
            result.Manifest = manifest;

            if (latest == null)
            {
                result.ErrorMessage = $"Unparseable manifest version: {manifest.Version}";
                return result;
            }

            // Update available if latest > current
            if (latest > current)
            {
                result.UpdateAvailable = true;

                // Forced upgrade if mandatory flag set OR if customer's current version
                // is below the manifest's minVersion floor.
                var minVer = ParseVersion(manifest.MinVersion);
                result.IsMandatory = manifest.Mandatory || (minVer != null && current < minVer);
            }

            return result;
        }
        catch (TaskCanceledException)
        {
            result.ErrorMessage = "Version check timed out";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Version check failed: {ex.Message}";
            return result;
        }
    }

    // Snooze: the customer clicked "Later" — don't prompt again for `duration`
    public void SnoozeVersion(string version, TimeSpan duration)
    {
        try
        {
            var data = LoadSnoozeData();
            data[$"snooze:{version}"] = DateTime.UtcNow.Add(duration).ToString("O");
            File.WriteAllText(SnoozePath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    // Skip: the customer clicked "Skip this version" — never prompt for this version again
    public void SkipVersion(string version)
    {
        try
        {
            var data = LoadSnoozeData();
            data[$"skip:{version}"] = "true";
            File.WriteAllText(SnoozePath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    public bool IsSnoozed(string version)
    {
        try
        {
            var data = LoadSnoozeData();
            if (data.TryGetValue($"skip:{version}", out var skipFlag) && skipFlag == "true")
                return true;
            if (data.TryGetValue($"snooze:{version}", out var snoozeUntilStr)
                && DateTime.TryParse(snoozeUntilStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var snoozeUntil)
                && snoozeUntil > DateTime.UtcNow)
                return true;
        }
        catch { }
        return false;
    }

    private static Dictionary<string, string> LoadSnoozeData()
    {
        try
        {
            if (File.Exists(SnoozePath))
            {
                var json = File.ReadAllText(SnoozePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch { }
        return new Dictionary<string, string>();
    }

    private static Version? ParseVersion(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Accept "4.5.0" or "4.5.0.1234"
        return Version.TryParse(s, out var v) ? v : null;
    }

    public static Version GetCurrentAppVersion()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetEntryAssembly()
                ?? System.Reflection.Assembly.GetExecutingAssembly();
            return asm.GetName().Version ?? new Version(0, 0, 0, 0);
        }
        catch
        {
            return new Version(0, 0, 0, 0);
        }
    }

    private static string LoadVersionCheckUrl()
    {
        // Same pattern as DiagnosticsService: derive from CloudSyncUrl by swapping suffix.
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return "";
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("CloudSyncUrl", out var u))
            {
                var url = u.GetString() ?? "";
                if (string.IsNullOrEmpty(url)) return "";
                var idx = url.LastIndexOf("/api/sync", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return url.Substring(0, idx) + "/api/version/latest";
                return url.TrimEnd('/') + "/api/version/latest";
            }
        }
        catch { }
        return "";
    }
}
