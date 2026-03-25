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

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); } catch { }
    }

    public bool IsCloudEnabled()
    {
        var url = LoadCloudSyncUrl();
        return !string.IsNullOrEmpty(url);
    }

    public async Task<string> SyncToCloudAsync()
    {
        var cloudUrl = LoadCloudSyncUrl();
        if (string.IsNullOrEmpty(cloudUrl))
            return "Cloud sync disabled";

        try
        {
            Log("Starting cloud sync via API...");

            using var local = new SqlConnection(_localConnectionString);
            await local.OpenAsync();

            // Step 0: Pull new users and QR assignments from cloud BEFORE push
            try
            {
                await PullCloudUsersAsync(local, cloudUrl);
            }
            catch (Exception pullEx)
            {
                Log($"Pre-pull users error (non-critical): {pullEx.Message}");
            }

            try
            {
                await PullCloudQrAssignmentsAsync(local, cloudUrl);
            }
            catch (Exception pullEx)
            {
                Log($"Pre-pull QR assignments error (non-critical): {pullEx.Message}");
            }

            var payload = new Dictionary<string, List<Dictionary<string, object?>>>();

            // Read each table from local DB into dictionaries
            // NOTE: Users are read AFTER pull, so newly pulled users are included in push
            payload["players"] = await ReadTableAsync(local,
                "SELECT Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType, StartDate, EndDate, " +
                "SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, IsFrozen, FreezeStartDate, " +
                "CAST(0 AS BIT) AS IsDeleted, CreatedAt, '' AS PhotoPath, Height, Weight, Notes FROM Employees");

            payload["accessEvents"] = await ReadTableAsync(local,
                "SELECT TOP 2000 Id, DoorId, CardId, EventType AS RecordType, EventCode, " +
                "[Timestamp] AS EventDate, Details FROM AccessEvents ORDER BY [Timestamp] DESC");

            payload["devices"] = await ReadTableAsync(local,
                "SELECT Id, Name, SerialNumber, IP, MAC FROM Devices");

            payload["doors"] = await ReadTableAsync(local,
                "SELECT Id, DeviceId, Name, DoorNumber FROM Doors");

            payload["transactions"] = await ReadTableAsync(local,
                "SELECT Id, Type, Category, Amount, Description, RelatedEmployeeId, " +
                "CreatedAt AS TransactionDate, CreatedBy AS RecordedBy FROM Transactions");

            payload["users"] = await ReadTableAsync(local,
                "SELECT Id, Username, PasswordHash, DisplayName, Role, IsActive, Permissions FROM Users");

            payload["auditLogs"] = await ReadTableAsync(local,
                "SELECT TOP 1000 Id, Action, EntityType, EntityId, Details, DetailsAr, PerformedBy, " +
                "[Timestamp] FROM AuditLogs ORDER BY [Timestamp] DESC");

            payload["deletedEmployees"] = await ReadTableAsync(local,
                "SELECT Id, OriginalId, FullNameEn, FullNameAr, CardNo, Phone, DeleteReason, DeletedBy, DeletedAt " +
                "FROM DeletedEmployees");

            payload["appSettings"] = await ReadTableAsync(local,
                "SELECT TOP 1 Id, GymName FROM AppSettings");

            payload["accessCards"] = await ReadTableAsync(local,
                "SELECT Id, EmployeeId, CardNumber, IsActive, ValidFrom, ValidTo, EffectiveTimes, CreatedAt FROM AccessCards");

            payload["qrPool"] = await ReadTableAsync(local,
                "SELECT Id, Code, Status, Source, GuestName, GuestPhone, Reason, " +
                "AssignedAt, UsedAt, ExpiredAt, MaxUses, UsedCount, ValidFrom, ValidTo, " +
                "DoorPermissions, CreatedAt, IsUploadedToDevice FROM QrPool");

            payload["subscriptionPlans"] = await ReadTableAsync(local,
                "SELECT Id, NameEn, NameAr, Duration, DurationType, Price, MaxVisits, " +
                "EffectiveTimes, IsActive, SortOrder, CreatedAt FROM SubscriptionPlans");

            payload["posShifts"] = await ReadTableAsync(local,
                "SELECT Id, OpenedBy, OpenedAt, ClosedAt, OpeningCash, ClosingCash, " +
                "TotalSales, TotalCashSales, TotalCardSales, Variance, Status FROM PosShifts");

            payload["freezeHistories"] = await ReadTableAsync(local,
                "SELECT Id, EmployeeId, FreezeStart, FreezeEnd, FreezeDays, Reason, CreatedAt FROM FreezeHistories");

            payload["products"] = await ReadTableAsync(local,
                "SELECT Id, Name, NameAr, Price, Stock, Barcode, Category, IsActive, CreatedAt FROM Products");

            payload["timeGroups"] = await ReadTableAsync(local,
                "SELECT Id, NameEn, NameAr, HardwareIndex, IsDefault, ScheduleJson, CreatedAt FROM TimeGroups");

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
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var content = new ByteArrayContent(compressedBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");
            var response = await httpClient.PostAsync(cloudUrl, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Log($"Cloud sync completed via API: {responseBody}");
                return $"Synced via API: {responseBody}";
            }
            else
            {
                Log($"Cloud sync API error: {response.StatusCode} - {responseBody}");
                return $"Sync API error: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            Log($"Cloud sync FAILED: {ex.Message}");
            return $"Sync failed: {ex.Message}";
        }
    }

    private async Task<List<Dictionary<string, object?>>> ReadTableAsync(SqlConnection conn, string sql)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 60;
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
            Log($"  Read error: {ex.Message}");
        }
        return rows;
    }

    private async Task PullCloudUsersAsync(SqlConnection local, string cloudUrl)
    {
        Log("Pull users: starting...");

        using var pullClient = new HttpClient();
        pullClient.DefaultRequestHeaders.Add("X-Api-Key", "HMTech-Sync-2026");
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
                    // INSERT new user from cloud — never update existing users (local is master)
                    using var insertCmd = new SqlCommand(
                        @"INSERT INTO Users (Username, PasswordHash, DisplayName, Role, IsActive, Permissions, CreatedAt)
                          VALUES (@u, @p, @d, @r, @a, @perm, GETUTCDATE())", local);
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
                Log($"Pull users: error adding user: {ex.Message}");
            }
        }

        if (added > 0)
            Log($"Pull users: {added} new user(s) added from cloud");
        else
            Log("Pull users: no new users to add (all exist locally)");
    }

    private async Task PullCloudQrAssignmentsAsync(SqlConnection local, string cloudUrl)
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
                    // INSERT cloud-assigned QR code into local pool
                    using var insertCmd = new SqlCommand(
                        @"INSERT INTO QrPool (Code, Status, Source, GuestName, GuestPhone, Reason,
                          DoorPermissions, MaxUses, UsedCount, ValidFrom, ValidTo, AssignedAt, CreatedAt, IsUploadedToDevice)
                          VALUES (@code, @status, 'Cloud', @name, @phone, @reason,
                          @perms, @max, @used, GETUTCDATE(), @validTo, @assigned, GETUTCDATE(), 0)", local);
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
                Log($"Pull QR: error adding entry: {ex.Message}");
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
        return "HMTech-Sync-2026"; // Default fallback for single gym
    }
}
