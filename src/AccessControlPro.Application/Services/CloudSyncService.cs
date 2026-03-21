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

            var payload = new Dictionary<string, List<Dictionary<string, object?>>>();

            // Read each table from local DB into dictionaries
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

            // Log table counts
            foreach (var kvp in payload)
                Log($"  {kvp.Key}: {kvp.Value.Count} rows read");

            // POST to cloud API
            var json = JsonSerializer.Serialize(payload);
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", "HMTech-Sync-2026");
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
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
}
