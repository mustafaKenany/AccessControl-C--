using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

public interface ICloudSyncService
{
    Task<string> SyncToCloudAsync();
    bool IsCloudEnabled();
}

public class CloudSyncService : ICloudSyncService
{
    private readonly string _localConnectionString;
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "cloud_sync_log.txt");

    public CloudSyncService(string localConnectionString)
    {
        _localConnectionString = localConnectionString;
    }

    private static void Log(string msg, string level = "info")
    {
        RollingLogFile.Append(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}\n");
    }

    public bool IsCloudEnabled()
    {
        // An offline / local-only install (operator unchecked "cloud" at setup) makes no cloud
        // calls at all — so a site with no internet never logs recurring sync errors.
        if (!IsCloudSyncEnabledFlag()) return false;
        var url = LoadCloudSyncUrl();
        return !string.IsNullOrEmpty(url);
    }

    /// <summary>
    /// Reads the optional "CloudSyncEnabled" flag from appsettings.json. Defaults to TRUE when the
    /// key is absent, so existing installs keep syncing. The setup wizard writes false for sites
    /// that opt out of the cloud.
    /// </summary>
    public static bool IsCloudSyncEnabledFlag()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return true;
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("CloudSyncEnabled", out var flag))
            {
                if (flag.ValueKind == JsonValueKind.False) return false;
                if (flag.ValueKind == JsonValueKind.True) return true;
                if (flag.ValueKind == JsonValueKind.String && bool.TryParse(flag.GetString(), out var b)) return b;
            }
        }
        catch { }
        return true;
    }

    public async Task<string> SyncToCloudAsync()
    {
        var cloudUrl = LoadCloudSyncUrl();
        if (string.IsNullOrEmpty(cloudUrl))
            return "Cloud sync disabled";

        // Check if the cloud has requested a forced full resync (admin clicked the button
        // on the web portal). Read-and-clear: if the flag was set, we reset our state so
        // this sync sends everything from scratch.
        try
        {
            if (await CheckForceFullSyncAsync(cloudUrl))
            {
                SyncStateManager.Reset();
                Log("Cloud requested force full sync — local delta state reset.");
            }
        }
        catch (Exception ex)
        {
            Log($"Sync control check failed (non-critical): {ex.Message}", "warn");
        }

        // Capture the sync start time BEFORE reading any data — this becomes the next
        // lastSyncAt watermark if the sync succeeds. Rows updated AT OR AFTER this moment
        // are still sent this round (safe: ">" filter means they re-send next round too,
        // worst-case one extra upsert — no data loss).
        var syncStartedAt = DateTime.UtcNow;
        var lastSync = SyncStateManager.LoadLastSyncAt();
        var isFullSync = lastSync == null;
        var sinceFilter = lastSync ?? DateTime.MinValue;

        try
        {
            Log($"Starting cloud sync via API... mode={(isFullSync ? "full" : "delta")} since={(lastSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(first-ever)")}");

            using var local = new SqlConnection(_localConnectionString);
            await local.OpenAsync();

            // Pulled rows get UpdatedAt set to just-before the delta watermark so the same
            // sync doesn't then push them back up as "new local changes" (they came from
            // the cloud, so the cloud already has them — wasted upsert).
            // First-ever sync has lastSync = null; use a sentinel older timestamp.
            var pulledRowTimestamp = (lastSync ?? DateTime.UtcNow.AddYears(-1)).AddMilliseconds(-1);

            // Step 0: Pull new users and QR assignments from cloud BEFORE push
            try
            {
                await PullCloudUsersAsync(local, cloudUrl, pulledRowTimestamp);
            }
            catch (Exception pullEx)
            {
                Log($"Pre-pull users error (non-critical): {pullEx.Message}", "warn");
            }

            try
            {
                await PullCloudQrAssignmentsAsync(local, cloudUrl, pulledRowTimestamp);
            }
            catch (Exception pullEx)
            {
                Log($"Pre-pull QR assignments error (non-critical): {pullEx.Message}", "warn");
            }

            var payload = new Dictionary<string, List<Dictionary<string, object?>>>();

            // Delta filter: only rows changed since last sync. Full-sync path omits the filter.
            // Tables that support delta use UpdatedAt (mutable) or an existing timestamp (append-only).
            string DeltaWhere(string col) => isFullSync ? "" : $" WHERE {col} > @since";

            // Players (Employees): mutable, has UpdatedAt from v4.5 migration
            payload["players"] = await ReadTableAsync(local,
                "SELECT Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType, StartDate, EndDate, " +
                "SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, IsFrozen, FreezeStartDate, " +
                "CAST(0 AS BIT) AS IsDeleted, CreatedAt, '' AS PhotoPath, Height, Weight, Notes FROM Employees" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // AccessEvents: append-only, use existing Timestamp (keep TOP 2000 cap as safety limit)
            payload["accessEvents"] = await ReadTableAsync(local,
                "SELECT TOP 2000 Id, DoorId, CardId, EventType AS RecordType, EventCode, " +
                "[Timestamp] AS EventDate, Details FROM AccessEvents" +
                DeltaWhere("[Timestamp]") + " ORDER BY [Timestamp] DESC", sinceFilter);

            // Small static tables — always full sync (few rows, cheap)
            payload["devices"] = await ReadTableAsync(local,
                "SELECT Id, Name, SerialNumber, IP, MAC FROM Devices");
            payload["doors"] = await ReadTableAsync(local,
                "SELECT Id, DeviceId, Name, DoorNumber FROM Doors");
            payload["appSettings"] = await ReadTableAsync(local,
                "SELECT TOP 1 Id, GymName FROM AppSettings");
            payload["timeGroups"] = await ReadTableAsync(local,
                "SELECT Id, NameEn, NameAr, HardwareIndex, IsDefault, ScheduleJson, CreatedAt FROM TimeGroups");

            // Transactions: append-only in practice, use CreatedAt
            payload["transactions"] = await ReadTableAsync(local,
                "SELECT Id, Type, Category, Amount, Description, RelatedEmployeeId, " +
                "CreatedAt AS TransactionDate, CreatedBy AS RecordedBy FROM Transactions" +
                DeltaWhere("CreatedAt"), sinceFilter);

            // Users: mutable, has UpdatedAt
            payload["users"] = await ReadTableAsync(local,
                "SELECT Id, Username, PasswordHash, DisplayName, Role, IsActive, Permissions FROM Users" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // AuditLogs: append-only, use Timestamp (keep TOP 1000 cap)
            payload["auditLogs"] = await ReadTableAsync(local,
                "SELECT TOP 1000 Id, Action, EntityType, EntityId, Details, DetailsAr, PerformedBy, " +
                "[Timestamp] FROM AuditLogs" +
                DeltaWhere("[Timestamp]") + " ORDER BY [Timestamp] DESC", sinceFilter);

            // DeletedEmployees: tombstones, append-only, use DeletedAt
            payload["deletedEmployees"] = await ReadTableAsync(local,
                "SELECT Id, OriginalId, FullNameEn, FullNameAr, CardNo, Phone, DeleteReason, DeletedBy, DeletedAt " +
                "FROM DeletedEmployees" + DeltaWhere("DeletedAt"), sinceFilter);

            // AccessCards: mutable, has UpdatedAt
            payload["accessCards"] = await ReadTableAsync(local,
                "SELECT Id, EmployeeId, CardNumber, IsActive, ValidFrom, ValidTo, EffectiveTimes, CreatedAt FROM AccessCards" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // QrPool: mutable, has UpdatedAt
            payload["qrPool"] = await ReadTableAsync(local,
                "SELECT Id, Code, Status, Source, GuestName, GuestPhone, Reason, " +
                "AssignedAt, UsedAt, ExpiredAt, MaxUses, UsedCount, ValidFrom, ValidTo, " +
                "DoorPermissions, CreatedAt, IsUploadedToDevice FROM QrPool" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // SubscriptionPlans: mutable, has UpdatedAt
            payload["subscriptionPlans"] = await ReadTableAsync(local,
                "SELECT Id, NameEn, NameAr, Duration, DurationType, Price, MaxVisits, " +
                "EffectiveTimes, IsActive, SortOrder, CreatedAt FROM SubscriptionPlans" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // PosShifts: mutable, has UpdatedAt
            payload["posShifts"] = await ReadTableAsync(local,
                "SELECT Id, OpenedBy, OpenedAt, ClosedAt, OpeningCash, ClosingCash, " +
                "TotalSales, TotalCashSales, TotalCardSales, Variance, Status FROM PosShifts" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // FreezeHistories: mutable, has UpdatedAt
            payload["freezeHistories"] = await ReadTableAsync(local,
                "SELECT Id, EmployeeId, FreezeStart, FreezeEnd, FreezeDays, Reason, CreatedAt FROM FreezeHistories" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // Products: mutable, has UpdatedAt
            payload["products"] = await ReadTableAsync(local,
                "SELECT Id, Name, NameAr, Price, Stock, Barcode, Category, IsActive, CreatedAt FROM Products" +
                DeltaWhere("UpdatedAt"), sinceFilter);

            // Log table counts
            foreach (var kvp in payload)
                Log($"  {kvp.Key}: {kvp.Value.Count} rows read");

            // POST to cloud API with gzip compression
            var json = JsonSerializer.Serialize(payload);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            using var memoryStream = new System.IO.MemoryStream();
            using (var gzipStream = new System.IO.Compression.GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Fastest))
            {
                gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
            }
            var compressedBytes = memoryStream.ToArray();

            Log($"  JSON size: {jsonBytes.Length / 1024}KB -> compressed: {compressedBytes.Length / 1024}KB");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
            httpClient.DefaultRequestHeaders.Add("X-Sync-Mode", isFullSync ? "full" : "delta");
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            using var content = new ByteArrayContent(compressedBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");
            using var response = await httpClient.PostAsync(cloudUrl, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Only advance the watermark on success — failures will retry everything next round.
                SyncStateManager.SaveLastSyncAt(syncStartedAt);

                var summary = SummarizeSyncResponse(responseBody);
                Log($"Cloud sync completed: {summary}");
                LogResponseSnippet(responseBody);
                return $"Synced: {summary}";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // 401 = the cloud doesn't recognise this gym's API key. Almost always a key
                // mismatch after a reinstall (a fresh key was generated instead of the gym's
                // real one). Spell it out so support isn't left guessing at a bare "Unauthorized".
                Log("Cloud sync API error: Unauthorized — the cloud did not recognise this gym's " +
                    "CloudApiKey. Check that CloudApiKey in appsettings.json matches the gym's key " +
                    "in Super Admin (this usually breaks after a reinstall generated a new key).", "error");
                return "Cloud rejected the key (Unauthorized). The CloudApiKey doesn't match this gym in Super Admin — fix it in appsettings.json.";
            }
            else
            {
                var truncated = responseBody.Length > 500 ? responseBody.Substring(0, 500) + "...(truncated)" : responseBody;
                Log($"Cloud sync API error: {response.StatusCode} - {truncated}", "error");
                return $"Sync API error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            Log($"Cloud sync FAILED: {ex.Message}", "error");
            return $"Sync failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Polls the cloud for this gym's remote-lock status (payment enforcement). Returns
    /// reachable=false if the cloud couldn't be contacted (caller applies the offline grace).
    /// Side-effect free (uses /api/lock-status, not /api/sync-control).
    /// </summary>
    public static async Task<(bool reachable, bool locked, string message)> CheckRemoteLockAsync()
    {
        var cloudUrl = LoadCloudSyncUrl();
        if (string.IsNullOrEmpty(cloudUrl)) return (false, false, "");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
            var url = cloudUrl.Replace("/api/sync", "/api/lock-status");
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return (false, false, "");

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            bool locked = root.TryGetProperty("locked", out var l) && l.ValueKind == JsonValueKind.True;
            string msg = root.TryGetProperty("lockMessage", out var m) ? (m.GetString() ?? "") : "";
            return (true, locked, msg);
        }
        catch { return (false, false, ""); }
    }

    private static async Task<bool> CheckForceFullSyncAsync(string cloudUrl)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
        client.Timeout = TimeSpan.FromSeconds(10);

        var controlUrl = cloudUrl.Replace("/api/sync", "/api/sync-control");
        var resp = await client.GetAsync(controlUrl);
        if (!resp.IsSuccessStatusCode) return false;

        var body = await resp.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("forceFullSync", out var v)
                   && v.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    private static string SummarizeSyncResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            var total = root.TryGetProperty("total", out var t) && t.TryGetInt32(out var ti) ? ti : 0;
            var errCount = 0;
            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
                errCount = errs.GetArrayLength();
            return $"ok={success}, total={total}, errors={errCount}";
        }
        catch
        {
            return body.Length > 200 ? body.Substring(0, 200) + "...(truncated)" : body;
        }
    }

    private static void LogResponseSnippet(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
            {
                int shown = 0;
                foreach (var err in errs.EnumerateArray())
                {
                    if (shown >= 3) break;
                    var text = err.GetString() ?? "";
                    if (text.Length > 300) text = text.Substring(0, 300) + "...";
                    Log($"  [error sample] {text}", "warn");
                    shown++;
                }
                if (errs.GetArrayLength() > 3)
                    Log($"  ({errs.GetArrayLength() - 3} more error(s) suppressed — see server logs for full list)", "warn");
            }
        }
        catch { }
    }

    private async Task<List<Dictionary<string, object?>>> ReadTableAsync(SqlConnection conn, string sql, DateTime? since = null)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 60;
            if (since.HasValue && sql.Contains("@since", StringComparison.OrdinalIgnoreCase))
                cmd.Parameters.AddWithValue("@since", since.Value);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    // Skip binary data
                    if (val is byte[]) val = null;
                    // Convert TimeSpan to string
                    if (val is TimeSpan ts) val = ts.ToString(@"hh\:mm\:ss");

                    row[name] = val;
                }
                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            Log($"  Read error: {ex.Message}", "warn");
        }
        return rows;
    }

    private async Task PullCloudUsersAsync(SqlConnection local, string cloudUrl, DateTime pulledRowTimestamp)
    {
        Log("Pull users: starting...");

        using var pullClient = new HttpClient();
        pullClient.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
        pullClient.Timeout = TimeSpan.FromSeconds(30);

        var pullUrl = cloudUrl.Replace("/api/sync", "/api/users");
        var pullResponse = await pullClient.GetAsync(pullUrl);

        if (!pullResponse.IsSuccessStatusCode)
        {
            Log($"Pull users: API returned {pullResponse.StatusCode}");
            return;
        }

        var json = await pullResponse.Content.ReadAsStringAsync();
        var cloudUsers = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

        if (cloudUsers == null || cloudUsers.Count == 0)
        {
            Log("Pull users: no users from cloud");
            return;
        }

        Log($"Pull users: received {cloudUsers.Count} users from cloud");
        int added = 0;

        foreach (var cloudUser in cloudUsers)
        {
            try
            {
                var username = cloudUser.ContainsKey("Username") ? cloudUser["Username"].GetString() ?? "" : "";
                if (string.IsNullOrEmpty(username)) continue;

                // Check if user already exists locally
                using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @u", local);
                checkCmd.Parameters.AddWithValue("@u", username);
                var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

                if (!exists)
                {
                    // INSERT new user from cloud — never update existing users (local is master).
                    // UpdatedAt is set to the pulled-row sentinel (older than the delta watermark)
                    // so this user isn't re-pushed to the cloud on the very same sync cycle.
                    using var insertCmd = new SqlCommand(
                        @"INSERT INTO Users (Username, PasswordHash, DisplayName, Role, IsActive, Permissions, CreatedAt, UpdatedAt)
                          VALUES (@u, @p, @d, @r, @a, @perm, GETUTCDATE(), @ts)", local);
                    insertCmd.Parameters.AddWithValue("@ts", pulledRowTimestamp);
                    insertCmd.Parameters.AddWithValue("@u", username);
                    insertCmd.Parameters.AddWithValue("@p", cloudUser.ContainsKey("PasswordHash") ? cloudUser["PasswordHash"].GetString() ?? "" : "");
                    insertCmd.Parameters.AddWithValue("@d", cloudUser.ContainsKey("DisplayName") ? cloudUser["DisplayName"].GetString() ?? "" : "");
                    insertCmd.Parameters.AddWithValue("@r", cloudUser.ContainsKey("Role") ? cloudUser["Role"].GetString() ?? "User" : "User");
                    insertCmd.Parameters.AddWithValue("@a", cloudUser.ContainsKey("IsActive") && cloudUser["IsActive"].ValueKind == JsonValueKind.True);
                    insertCmd.Parameters.AddWithValue("@perm", cloudUser.ContainsKey("Permissions") ? cloudUser["Permissions"].GetString() ?? "" : "");
                    await insertCmd.ExecuteNonQueryAsync();
                    added++;
                    Log($"Pull users: added '{username}' from cloud");
                }
            }
            catch (Exception ex)
            {
                Log($"Pull users: error adding user: {ex.Message}", "warn");
            }
        }

        if (added > 0)
            Log($"Pull users: {added} new user(s) added from cloud");
        else
            Log("Pull users: no new users to add (all exist locally)");
    }

    private async Task PullCloudQrAssignmentsAsync(SqlConnection local, string cloudUrl, DateTime pulledRowTimestamp)
    {
        Log("Pull QR assignments: starting...");

        using var pullClient = new HttpClient();
        pullClient.DefaultRequestHeaders.Add("X-Api-Key", LoadApiKey());
        pullClient.Timeout = TimeSpan.FromSeconds(30);

        var pullUrl = cloudUrl.Replace("/api/sync", "/api/qr-pool");
        var pullResponse = await pullClient.GetAsync(pullUrl);

        if (!pullResponse.IsSuccessStatusCode)
        {
            Log($"Pull QR assignments: API returned {pullResponse.StatusCode}");
            return;
        }

        var json = await pullResponse.Content.ReadAsStringAsync();
        var cloudEntries = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

        if (cloudEntries == null || cloudEntries.Count == 0)
        {
            Log("Pull QR assignments: no entries from cloud");
            return;
        }

        Log($"Pull QR assignments: received {cloudEntries.Count} entries from cloud");
        int added = 0;

        foreach (var entry in cloudEntries)
        {
            try
            {
                var code = entry.ContainsKey("Code") ? entry["Code"].GetString() ?? "" : "";
                if (string.IsNullOrEmpty(code)) continue;

                // Check if code already exists locally
                using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM QrPool WHERE Code = @c", local);
                checkCmd.Parameters.AddWithValue("@c", code);
                var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

                if (!exists)
                {
                    // INSERT cloud-assigned QR code into local pool. UpdatedAt = pulled-row
                    // sentinel so delta push doesn't re-send it on the same sync cycle.
                    using var insertCmd = new SqlCommand(
                        @"INSERT INTO QrPool (Code, Status, Source, GuestName, GuestPhone, Reason,
                          DoorPermissions, MaxUses, UsedCount, ValidFrom, ValidTo, AssignedAt, CreatedAt, IsUploadedToDevice, UpdatedAt)
                          VALUES (@code, @status, 'Cloud', @name, @phone, @reason,
                          @perms, @max, @used, GETUTCDATE(), @validTo, @assigned, GETUTCDATE(), 0, @ts)", local);
                    insertCmd.Parameters.AddWithValue("@ts", pulledRowTimestamp);
                    insertCmd.Parameters.AddWithValue("@code", code);
                    insertCmd.Parameters.AddWithValue("@status", entry.ContainsKey("Status") ? entry["Status"].GetInt32() : 1);
                    insertCmd.Parameters.AddWithValue("@name", entry.ContainsKey("GuestName") ? entry["GuestName"].GetString() ?? "" : "");
                    insertCmd.Parameters.AddWithValue("@phone", entry.ContainsKey("GuestPhone") ? entry["GuestPhone"].GetString() ?? "" : "");
                    insertCmd.Parameters.AddWithValue("@reason", entry.ContainsKey("Reason") ? entry["Reason"].GetString() ?? "" : "");
                    insertCmd.Parameters.AddWithValue("@perms", entry.ContainsKey("DoorPermissions") ? entry["DoorPermissions"].GetString() ?? "01010000" : "01010000");
                    insertCmd.Parameters.AddWithValue("@max", entry.ContainsKey("MaxUses") ? entry["MaxUses"].GetInt32() : 2);
                    insertCmd.Parameters.AddWithValue("@used", entry.ContainsKey("UsedCount") ? entry["UsedCount"].GetInt32() : 0);

                    if (entry.ContainsKey("ValidTo") && entry["ValidTo"].ValueKind == JsonValueKind.String
                        && DateTime.TryParse(entry["ValidTo"].GetString(), out var vt))
                        insertCmd.Parameters.AddWithValue("@validTo", vt);
                    else
                        insertCmd.Parameters.AddWithValue("@validTo", DateTime.UtcNow.AddYears(1));

                    if (entry.ContainsKey("AssignedAt") && entry["AssignedAt"].ValueKind == JsonValueKind.String
                        && DateTime.TryParse(entry["AssignedAt"].GetString(), out var aa))
                        insertCmd.Parameters.AddWithValue("@assigned", aa);
                    else
                        insertCmd.Parameters.AddWithValue("@assigned", DBNull.Value);

                    await insertCmd.ExecuteNonQueryAsync();
                    added++;
                    Log($"Pull QR: added code '{code}' from cloud");
                }
            }
            catch (Exception ex)
            {
                Log($"Pull QR: error adding entry: {ex.Message}", "warn");
            }
        }

        if (added > 0)
            Log($"Pull QR assignments: {added} new code(s) added from cloud");
        else
            Log("Pull QR assignments: no new codes to add (all exist locally)");
    }

    private static string? LoadCloudSyncUrl()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);

            // Try CloudSyncUrl first
            if (doc.RootElement.TryGetProperty("CloudSyncUrl", out var url))
            {
                var val = url.GetString();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Fallback: build from CloudConnection (for backward compatibility)
            // Not used anymore since we use HTTPS API
            return null;
        }
        catch { }
        return null;
    }

    private static string LoadApiKey()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("CloudApiKey", out var key))
                {
                    var val = key.GetString();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
        }
        catch { }
        return ""; // No hardcoded fallback — API key must be configured in appsettings.json
    }
}
