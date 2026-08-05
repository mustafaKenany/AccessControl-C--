using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

/// <summary>
/// Collects diagnostic files (logs + system snapshot), zips them, and POSTs the
/// bundle to the cloud's <c>/api/diagnostics/upload</c> endpoint. Used both by
/// the manual "Send Diagnostics" button and the 15-day auto-uploader.
///
/// The endpoint reuses the same X-Api-Key the sync client uses, so the cloud
/// resolves which gym the bundle belongs to automatically.
/// </summary>
public interface IDiagnosticsService
{
    Task<DiagnosticsResult> UploadAsync(string trigger, string userNote = "");
    Task<DiagnosticsResult> UploadIfDueAsync(int intervalDays = 15);
    Task<DiagnosticsResult> UploadIfPreviousRunCrashedAsync(Func<bool> wasPreviousRunACrash);
}

public class DiagnosticsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public long BytesUploaded { get; set; }
}

public class DiagnosticsService : IDiagnosticsService
{
    private readonly string _localConnectionString;
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "diagnostics_log.txt");
    private const long MaxBundleBytes = 20L * 1024 * 1024; // 20 MB hard cap

    public DiagnosticsService(string localConnectionString)
    {
        _localConnectionString = localConnectionString;
    }

    private static void Log(string msg, string level = "info")
    {
        RollingLogFile.Append(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}\n");
    }

    /// <summary>
    /// Runs only if 15+ days have passed since the last successful upload. Designed
    /// for fire-and-forget on app startup — failures are swallowed and logged.
    /// </summary>
    public async Task<DiagnosticsResult> UploadIfDueAsync(int intervalDays = 15)
    {
        var last = LoadLastUpload();
        if (last != null && (DateTime.UtcNow - last.Value).TotalDays < intervalDays)
        {
            return new DiagnosticsResult { Success = true, Message = $"Skipped (last upload {(DateTime.UtcNow - last.Value).TotalDays:F1} days ago)" };
        }
        return await UploadAsync("auto");
    }

    /// <summary>
    /// Fire immediately if the caller-supplied probe says the previous run died without
    /// a clean shutdown. Adds a small dedupe guard so we don't upload twice for the same
    /// crash window (e.g. crash → restart → crash again would only upload once per day).
    /// </summary>
    public async Task<DiagnosticsResult> UploadIfPreviousRunCrashedAsync(Func<bool> wasPreviousRunACrash)
    {
        try
        {
            if (!wasPreviousRunACrash())
                return new DiagnosticsResult { Success = true, Message = "Previous run was clean — no crash upload" };

            // Dedupe: only one crash-recovery upload per 4 hours.
            var last = LoadLastUpload();
            if (last != null && (DateTime.UtcNow - last.Value).TotalHours < 4)
                return new DiagnosticsResult { Success = true, Message = "Crash detected but recent upload exists" };

            return await UploadAsync("crash-recovery");
        }
        catch (Exception ex)
        {
            Log($"crash-recovery probe failed: {ex.Message}", "error");
            return new DiagnosticsResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<DiagnosticsResult> UploadAsync(string trigger, string userNote = "")
    {
        try
        {
            var cloudUrl = LoadDiagnosticsUrl();
            if (string.IsNullOrEmpty(cloudUrl))
            {
                Log("Skipped: CloudSyncUrl not configured");
                return new DiagnosticsResult { Success = false, Message = "Cloud URL not configured" };
            }

            Log($"Building diagnostics bundle (trigger={trigger}, note={(string.IsNullOrEmpty(userNote) ? "(none)" : "yes")})...");
            var bundle = await BuildBundleAsync(trigger, userNote);
            Log($"Bundle ready: {bundle.Length / 1024} KB");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            http.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
            http.DefaultRequestHeaders.Add("X-Trigger", trigger);
            http.DefaultRequestHeaders.Add("X-App-Version", GetAppVersion());

            using var content = new ByteArrayContent(bundle);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

            using var resp = await http.PostAsync(cloudUrl, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                SaveLastUpload(DateTime.UtcNow);
                Log($"Uploaded OK: {resp.StatusCode}, server replied: {Truncate(body, 200)}");
                return new DiagnosticsResult { Success = true, Message = "Uploaded", BytesUploaded = bundle.Length };
            }

            Log($"Upload failed: {resp.StatusCode}, body: {Truncate(body, 300)}", "error");
            return new DiagnosticsResult { Success = false, Message = $"Server returned {resp.StatusCode}" };
        }
        catch (Exception ex)
        {
            Log($"Upload exception: {ex.Message}", "error");
            return new DiagnosticsResult { Success = false, Message = ex.Message };
        }
    }

    // ------------------------------------------------------------------ bundle

    private async Task<byte[]> BuildBundleAsync(string trigger, string userNote)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Customer's own description first — the single highest-signal file
            AddText(zip, "user-note.txt", BuildUserNote(trigger, userNote));

            // System + app info next — small and always wanted
            AddText(zip, "system-info.json", BuildSystemInfo());
            AddText(zip, "app-info.json", BuildAppInfo());
            AddText(zip, "gym-info.json", BuildGymInfo());
            AddText(zip, "settings-sanitized.json", BuildSanitizedSettings());
            AddText(zip, "network-test.txt", await BuildNetworkTestAsync());
            AddText(zip, "db-snapshot.json", await BuildDbSnapshotAsync());
            AddText(zip, "event-tail.csv", await BuildEventTailAsync());

            // Logs: flat .txt files in app base directory (per the existing logger).
            // Order matters — we cap the bundle at 20 MB, so add the most-recent /
            // most-important files first; if we run out of room we just stop adding.
            var logsAdded = 0;
            foreach (var file in EnumerateLogFiles())
            {
                if (ms.Length >= MaxBundleBytes) break;
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    AddBinary(zip, "logs/" + Path.GetFileName(file), bytes);
                    logsAdded++;
                }
                catch (Exception ex)
                {
                    Log($"Skipped {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            AddText(zip, "manifest.txt", $"Logs included: {logsAdded}\nBundle built: {DateTime.UtcNow:O}\n");
        }
        return ms.ToArray();
    }

    private static IEnumerable<string> EnumerateLogFiles()
    {
        var dir = AppContext.BaseDirectory;
        // Prioritise the high-signal files, then anything else ending in .txt that
        // looks like a log. The first matches dominate the 20 MB budget.
        // heap_log/memory_log are the leak-hunt files — always include them FIRST so a bloated log
        // folder can never push them out of the 20 MB budget.
        var priorityNames = new[]
        {
            "crash_log.txt", "startup_log.txt", "heap_log.txt", "memory_log.txt",
            "cloud_sync_log.txt", "diagnostics_log.txt", "session.log"
        };
        foreach (var name in priorityNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) yield return path;
        }

        // Then any other *log*.txt — NEWEST first, EXCLUDING the rotated "legacy" archives
        // (RollingLogFile keeps dozens of auth_log_legacy_*.txt that bloated bundles to ~1 MB and
        // pushed out the useful logs). Cap to the 15 most recent.
        if (Directory.Exists(dir))
        {
            var extra = Directory.EnumerateFiles(dir, "*log*.txt", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).IndexOf("legacy", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(f => !priorityNames.Any(n => string.Equals(Path.GetFileName(f), n, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .Take(15);
            foreach (var file in extra) yield return file;
        }

        // And the Logs/ subfolder if it exists (legacy / future location)
        var logsSub = Path.Combine(dir, "Logs");
        if (Directory.Exists(logsSub))
        {
            foreach (var file in Directory.EnumerateFiles(logsSub, "*.*", SearchOption.AllDirectories)
                                          .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                                                   || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                                          .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc))
            {
                yield return file;
            }
        }
    }

    // ------------------------------------------------------------------ snapshots

    private static string BuildSystemInfo()
    {
        var info = new
        {
            os = Environment.OSVersion.ToString(),
            machine = Environment.MachineName,
            user = Environment.UserName,
            dotnet = Environment.Version.ToString(),
            processorCount = Environment.ProcessorCount,
            workingSet = Environment.WorkingSet,
            currentDirectory = Environment.CurrentDirectory,
            timezone = TimeZoneInfo.Local.Id,
            utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).ToString(),
            localTime = DateTime.Now.ToString("O"),
            utcTime = DateTime.UtcNow.ToString("O")
        };
        return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildAppInfo()
    {
        var info = new
        {
            appVersion = GetAppVersion(),
            buildDate = File.GetLastWriteTimeUtc(typeof(DiagnosticsService).Assembly.Location).ToString("O"),
            assemblyLocation = typeof(DiagnosticsService).Assembly.Location,
            baseDirectory = AppContext.BaseDirectory,
            lastSyncAt = SyncStateManager.LoadLastSyncAt()?.ToString("O") ?? "(never)",
            lastDiagnosticsUpload = LoadLastUpload()?.ToString("O") ?? "(never)"
        };
        return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildGymInfo()
    {
        // The API key + URL are read separately so the bundle doesn't repeat the
        // full key (server already knows which gym this is by the request header).
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return "{}";
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);

            string GetStr(string k) => doc.RootElement.TryGetProperty(k, out var v) ? (v.GetString() ?? "") : "";
            var key = GetStr("CloudApiKey");
            var keyTail = key.Length > 4 ? key.Substring(key.Length - 4) : key;

            var info = new
            {
                cloudUrl = GetStr("CloudSyncUrl"),
                apiKeySuffix = "...".PadRight(0) + keyTail,
                hasApiKey = !string.IsNullOrEmpty(key)
            };
            return JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return "{ \"error\": \"" + ex.Message.Replace("\"", "'") + "\" }";
        }
    }

    private static string BuildSanitizedSettings()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return "{}";
            var raw = File.ReadAllText(path);
            // Brute-force redaction: blank out anything that looks like a connection password
            // or an API key. We never want to ship these even though the cloud already knows.
            var sb = new StringBuilder(raw);
            sb.Replace("\"CloudApiKey\":", "\"CloudApiKey-REDACTED\":");
            // Common SQL password keys
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                sb.ToString(),
                @"(Password|Pwd)\s*=\s*[^;""]+",
                "$1=***REDACTED***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return sanitized;
        }
        catch (Exception ex)
        {
            return "{ \"error\": \"" + ex.Message.Replace("\"", "'") + "\" }";
        }
    }

    private static string BuildUserNote(string trigger, string userNote)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Trigger:   {trigger}");
        sb.AppendLine($"BundledAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("User note:");
        sb.AppendLine("----------");
        sb.AppendLine(string.IsNullOrWhiteSpace(userNote) ? "(no note provided)" : userNote.Trim());
        return sb.ToString();
    }

    private static async Task<string> BuildNetworkTestAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Network self-test — generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Pull the cloud host from settings so we test the actual configured endpoint
        string host = "hmtech.solutions";
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("CloudSyncUrl", out var u))
                {
                    var url = u.GetString() ?? "";
                    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        host = uri.Host;
                }
            }
        }
        catch { }

        sb.AppendLine($"Target host: {host}");
        sb.AppendLine();

        // DNS resolve
        try
        {
            var addrs = await System.Net.Dns.GetHostAddressesAsync(host);
            sb.AppendLine($"DNS resolve: OK ({addrs.Length} address(es))");
            foreach (var a in addrs) sb.AppendLine($"  - {a}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"DNS resolve: FAILED — {ex.Message}");
        }
        sb.AppendLine();

        // Ping (best-effort; firewalls may block ICMP — that's OK, HTTPS is what we care about)
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            sb.AppendLine($"Ping: {reply.Status} ({reply.RoundtripTime} ms)");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ping: FAILED (often firewalled — not necessarily bad) — {ex.Message}");
        }
        sb.AppendLine();

        // HTTPS HEAD — actual connectivity test
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Head, $"https://{host}/");
            using var resp = await http.SendAsync(req);
            sw.Stop();
            sb.AppendLine($"HTTPS HEAD https://{host}/: {(int)resp.StatusCode} {resp.StatusCode} ({sw.ElapsedMilliseconds} ms)");
            sb.AppendLine($"  Server: {string.Join(", ", resp.Headers.Where(h => h.Key.Equals("Server", StringComparison.OrdinalIgnoreCase)).SelectMany(h => h.Value))}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"HTTPS HEAD: FAILED — {ex.Message}");
            if (ex.InnerException != null) sb.AppendLine($"  Inner: {ex.InnerException.Message}");
        }
        sb.AppendLine();

        // Sync API reachability — POST a tiny invalid body and look for HTTP-level response.
        // We don't care about 200/401 — we care that we can REACH the endpoint at all.
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/api/sync-control");
            using var resp = await http.SendAsync(req);
            sw.Stop();
            sb.AppendLine($"Sync API reachability: HTTP {(int)resp.StatusCode} ({sw.ElapsedMilliseconds} ms)  — endpoint is alive");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Sync API reachability: FAILED — {ex.Message}");
        }

        return sb.ToString();
    }

    private async Task<string> BuildDbSnapshotAsync()
    {
        // High-signal one-shot snapshot: row counts + recent audit log + last sync result.
        // We swallow per-section errors so one missing table doesn't blank the whole file.
        var rowCounts = new Dictionary<string, object?>();
        var lastSync = new Dictionary<string, object?>();
        var auditTail = new List<Dictionary<string, object?>>();
        var subscriptionPlans = new List<Dictionary<string, object?>>();
        string? error = null;

        try
        {
            using var conn = new SqlConnection(_localConnectionString);
            await conn.OpenAsync();

            // Row counts (one round-trip via UNION ALL — cheap)
            var countQueries = new (string name, string sql)[]
            {
                ("employees",        "SELECT COUNT(*) FROM Employees"),
                ("employees_active", "SELECT COUNT(*) FROM Employees WHERE EndDate >= GETDATE()"),
                ("employees_frozen", "SELECT COUNT(*) FROM Employees WHERE IsFrozen = 1"),
                ("deleted_employees","SELECT COUNT(*) FROM DeletedEmployees"),
                ("access_cards",     "SELECT COUNT(*) FROM AccessCards"),
                ("access_events",    "SELECT COUNT(*) FROM AccessEvents"),
                ("doors",            "SELECT COUNT(*) FROM Doors"),
                ("devices",          "SELECT COUNT(*) FROM Devices"),
                ("transactions",     "SELECT COUNT(*) FROM Transactions"),
                ("users",            "SELECT COUNT(*) FROM Users"),
                ("audit_logs",       "SELECT COUNT(*) FROM AuditLogs"),
                ("qr_pool",          "SELECT COUNT(*) FROM QrPool"),
                ("subscription_plans","SELECT COUNT(*) FROM SubscriptionPlans"),
                ("products",         "SELECT COUNT(*) FROM Products"),
                ("freeze_histories", "SELECT COUNT(*) FROM FreezeHistories"),
                ("time_groups",      "SELECT COUNT(*) FROM TimeGroups"),
                ("pos_shifts",       "SELECT COUNT(*) FROM PosShifts"),
            };
            foreach (var (name, sql) in countQueries)
            {
                try
                {
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.CommandTimeout = 5;
                    var v = await cmd.ExecuteScalarAsync();
                    rowCounts[name] = v;
                }
                catch (Exception ex) { rowCounts[name] = $"error: {ex.Message}"; }
            }

            // Last audit log entries — what was the user doing right before something broke?
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT TOP 100 Id, Action, EntityType, EntityId, PerformedBy, [Timestamp], Details " +
                    "FROM AuditLogs ORDER BY [Timestamp] DESC", conn);
                cmd.CommandTimeout = 10;
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    auditTail.Add(new Dictionary<string, object?>
                    {
                        ["id"] = r["Id"],
                        ["action"] = r["Action"]?.ToString(),
                        ["entityType"] = r["EntityType"]?.ToString(),
                        ["entityId"] = r["EntityId"],
                        ["performedBy"] = r["PerformedBy"]?.ToString(),
                        ["timestamp"] = r["Timestamp"] is DateTime dt ? dt.ToString("O") : null,
                        ["details"] = Truncate(r["Details"]?.ToString() ?? "", 300)
                    });
                }
            }
            catch (Exception ex) { lastSync["audit_error"] = ex.Message; }

            // Subscription plans — to diagnose "Fitness doesn't exist in admin" type issues
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT Id, NameEn, NameAr, Duration, Price, IsActive FROM SubscriptionPlans", conn);
                cmd.CommandTimeout = 5;
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    subscriptionPlans.Add(new Dictionary<string, object?>
                    {
                        ["id"] = r["Id"],
                        ["nameEn"] = r["NameEn"]?.ToString(),
                        ["nameAr"] = r["NameAr"]?.ToString(),
                        ["duration"] = r["Duration"],
                        ["price"] = r["Price"],
                        ["isActive"] = r["IsActive"]
                    });
                }
            }
            catch (Exception ex) { lastSync["plans_error"] = ex.Message; }

            // Last successful sync info — from SyncStateManager
            try
            {
                var lastSyncAt = SyncStateManager.LoadLastSyncAt();
                lastSync["lastSyncAt"] = lastSyncAt?.ToString("O") ?? "(never)";
                lastSync["minutesAgo"] = lastSyncAt.HasValue ? (DateTime.UtcNow - lastSyncAt.Value).TotalMinutes : (object?)null;
            }
            catch (Exception ex) { lastSync["sync_state_error"] = ex.Message; }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        var payload = new Dictionary<string, object?>
        {
            ["generatedAt"] = DateTime.UtcNow.ToString("O"),
            ["error"] = error,
            ["rowCounts"] = rowCounts,
            ["lastSync"] = lastSync,
            ["subscriptionPlans"] = subscriptionPlans,
            ["auditTail"] = auditTail
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> BuildEventTailAsync()
    {
        try
        {
            using var conn = new SqlConnection(_localConnectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT TOP 200 Id, DoorId, CardId, EventType, EventCode, [Timestamp], Details " +
                "FROM AccessEvents ORDER BY [Timestamp] DESC", conn);
            using var r = await cmd.ExecuteReaderAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Id,DoorId,CardId,EventType,EventCode,Timestamp,Details");
            while (await r.ReadAsync())
            {
                sb.Append(r["Id"]).Append(',');
                sb.Append(r["DoorId"]).Append(',');
                sb.Append(r["CardId"]).Append(',');
                sb.Append(EscapeCsv(r["EventType"]?.ToString())).Append(',');
                sb.Append(EscapeCsv(r["EventCode"]?.ToString())).Append(',');
                sb.Append(r["Timestamp"] is DateTime dt ? dt.ToString("O") : "").Append(',');
                sb.AppendLine(EscapeCsv(r["Details"]?.ToString()));
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "error: " + ex.Message;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void AddText(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        using var w = new StreamWriter(s, Encoding.UTF8);
        w.Write(text);
    }

    private static void AddBinary(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }

    private static string LoadDiagnosticsUrl()
    {
        // Derive from the existing CloudSyncUrl so we don't need a second config key.
        // Replace the trailing /api/sync (any case) with /api/diagnostics/upload.
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
                if (idx >= 0) return url.Substring(0, idx) + "/api/diagnostics/upload";
                return url.TrimEnd('/') + "/api/diagnostics/upload";
            }
        }
        catch { }
        return "";
    }

    private static string LoadApiKey()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return "";
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("CloudApiKey", out var k))
                return k.GetString() ?? "";
        }
        catch { }
        return "";
    }

    private static DateTime? LoadLastUpload()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("LastDiagnosticsUpload", out var t))
            {
                var s = t.GetString();
                if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
            }
        }
        catch { }
        return null;
    }

    private static void SaveLastUpload(DateTime utc)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return;
            var raw = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(raw);
            var dict = new Dictionary<string, JsonElement>();
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.Clone();
            // Overwrite (or insert) the LastDiagnosticsUpload key
            var stamp = JsonSerializer.SerializeToElement(utc.ToString("O"));
            dict["LastDiagnosticsUpload"] = stamp;

            var opts = new JsonSerializerOptions { WriteIndented = true };
            using var outMs = new MemoryStream();
            using (var w = new Utf8JsonWriter(outMs, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                foreach (var kv in dict)
                {
                    w.WritePropertyName(kv.Key);
                    kv.Value.WriteTo(w);
                }
                w.WriteEndObject();
            }
            File.WriteAllBytes(path, outMs.ToArray());
        }
        catch (Exception ex)
        {
            Log($"Failed to persist LastDiagnosticsUpload: {ex.Message}");
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            return typeof(DiagnosticsService).Assembly.GetName().Version?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? (s ?? "") : s.Substring(0, max) + "...";
}
