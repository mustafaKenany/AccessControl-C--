using System.IO;
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
        var cloudConn = LoadCloudConnectionString();
        return !string.IsNullOrEmpty(cloudConn) && !cloudConn.Contains("xxxx");
    }

    public async Task<string> SyncToCloudAsync()
    {
        var cloudConn = LoadCloudConnectionString();
        if (string.IsNullOrEmpty(cloudConn) || cloudConn.Contains("xxxx"))
            return "Cloud sync disabled";

        var errors = new List<string>();
        try
        {
            Log("Starting cloud sync...");

            using var local = new SqlConnection(_localConnectionString);
            await local.OpenAsync();
            Log("Local DB connected.");

            using var cloud = new Npgsql.NpgsqlConnection(cloudConn);
            await cloud.OpenAsync();
            Log("Cloud DB connected.");

            int total = 0;

            // Sync Players (Employees → Players)
            // Local Employees table columns: Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType,
            // StartDate, EndDate, SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, IsFrozen, FreezeStartDate,
            // IsDeleted, CreatedAt, Height, Weight, Notes
            // Note: Local has PhotoData (binary) and CardBalance - excluded as cloud doesn't need them.
            // Note: Cloud has PhotoPath (varchar) - we send empty string as local stores binary PhotoData.
            total += await SyncWithMappingAsync(local, cloud, "Players",
                "SELECT Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType, StartDate, EndDate, " +
                "SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, IsFrozen, FreezeStartDate, " +
                "IsDeleted, CreatedAt, '' AS PhotoPath, Height, Weight, Notes FROM Employees",
                new[] { "Id", "FullNameEn", "FullNameAr", "CardNo", "Phone", "SubscriptionType", "StartDate", "EndDate",
                        "SubscriptionFee", "AmountPaid", "MaxVisits", "UsedVisits", "IsFrozen", "FreezeStartDate",
                        "IsDeleted", "CreatedAt", "PhotoPath", "Height", "Weight", "Notes" }, errors);

            // Sync Events
            // Local AccessEvents columns: Id, DoorId, CardId, EventType (int), EventCode (int), Timestamp, Details
            // Cloud columns: Id, DoorId, CardId, RecordType (int), EventCode (int), EventDate, Details
            total += await SyncWithMappingAsync(local, cloud, "AccessEvents",
                "SELECT TOP 2000 Id, DoorId, CardId, EventType, EventCode, [Timestamp], Details " +
                "FROM AccessEvents ORDER BY [Timestamp] DESC",
                new[] { "Id", "DoorId", "CardId", "RecordType", "EventCode", "EventDate", "Details" }, errors);

            // Sync Devices
            total += await SyncWithMappingAsync(local, cloud, "Devices",
                "SELECT Id, Name, SerialNumber, IP, MAC FROM Devices",
                new[] { "Id", "Name", "SerialNumber", "IP", "MAC" }, errors);

            // Sync Doors
            total += await SyncWithMappingAsync(local, cloud, "Doors",
                "SELECT Id, DeviceId, Name, DoorNumber FROM Doors",
                new[] { "Id", "DeviceId", "Name", "DoorNumber" }, errors);

            // Sync Transactions
            // Local columns: Id, Type, Category, Amount, Description, RelatedEmployeeId, CreatedAt, CreatedBy
            // Cloud columns: Id, Type, Category, Amount, Description, RelatedEmployeeId, TransactionDate, RecordedBy
            total += await SyncWithMappingAsync(local, cloud, "Transactions",
                "SELECT Id, Type, Category, Amount, Description, RelatedEmployeeId, CreatedAt, CreatedBy " +
                "FROM Transactions",
                new[] { "Id", "Type", "Category", "Amount", "Description", "RelatedEmployeeId", "TransactionDate", "RecordedBy" }, errors);

            // Sync Users
            total += await SyncWithMappingAsync(local, cloud, "Users",
                "SELECT Id, Username, PasswordHash, DisplayName, Role, IsActive, Permissions FROM Users",
                new[] { "Id", "Username", "PasswordHash", "DisplayName", "Role", "IsActive", "Permissions" }, errors);

            // Sync AppSettings
            total += await SyncWithMappingAsync(local, cloud, "AppSettings",
                "SELECT TOP 1 Id, GymName FROM AppSettings",
                new[] { "Id", "GymName" }, errors);

            // Sync AuditLogs
            total += await SyncWithMappingAsync(local, cloud, "AuditLogs",
                "SELECT TOP 1000 Id, Action, EntityType, EntityId, Details, DetailsAr, PerformedBy, [Timestamp] " +
                "FROM AuditLogs ORDER BY [Timestamp] DESC",
                new[] { "Id", "Action", "EntityType", "EntityId", "Details", "DetailsAr", "PerformedBy", "Timestamp" }, errors);

            // Sync DeletedEmployees
            total += await SyncWithMappingAsync(local, cloud, "DeletedEmployees",
                "SELECT Id, OriginalId, FullNameEn, FullNameAr, CardNo, Phone, DeleteReason, DeletedBy, DeletedAt " +
                "FROM DeletedEmployees",
                new[] { "Id", "OriginalId", "FullNameEn", "FullNameAr", "CardNo", "Phone", "DeleteReason", "DeletedBy", "DeletedAt" }, errors);

            // Build sync details
            var status = errors.Count == 0 ? "Success" : "PartialSuccess";
            var details = $"Synced {total} records";
            if (errors.Count > 0)
                details += $" | Errors ({errors.Count}): " + string.Join("; ", errors.Take(5));

            // Log sync
            try
            {
                using var logCmd = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO ""CloudSyncLogs"" (""SyncType"", ""Status"", ""Details"", ""SyncedAt"")
                      VALUES (@t, @s, @d, @ts)", cloud);
                logCmd.Parameters.AddWithValue("t", "FullSync");
                logCmd.Parameters.AddWithValue("s", status);
                logCmd.Parameters.AddWithValue("d", details);
                logCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Log($"  Failed to write sync log: {ex.Message}"); }

            Log($"Cloud sync completed: {details}");
            return details;
        }
        catch (Exception ex)
        {
            var msg = $"Sync failed: {ex.Message}";
            Log($"Cloud sync FAILED: {ex.Message}\n  StackTrace: {ex.StackTrace}");

            // Try to log the failure to cloud
            try
            {
                var cloudConn2 = LoadCloudConnectionString();
                if (!string.IsNullOrEmpty(cloudConn2))
                {
                    using var cloud2 = new Npgsql.NpgsqlConnection(cloudConn2);
                    await cloud2.OpenAsync();
                    using var logCmd = new Npgsql.NpgsqlCommand(
                        @"INSERT INTO ""CloudSyncLogs"" (""SyncType"", ""Status"", ""Details"", ""SyncedAt"")
                          VALUES (@t, @s, @d, @ts)", cloud2);
                    logCmd.Parameters.AddWithValue("t", "FullSync");
                    logCmd.Parameters.AddWithValue("s", "Failed");
                    logCmd.Parameters.AddWithValue("d", ex.Message);
                    logCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
                    await logCmd.ExecuteNonQueryAsync();
                }
            }
            catch { }

            return msg;
        }
    }

    private async Task<int> SyncWithMappingAsync(SqlConnection local, Npgsql.NpgsqlConnection cloud,
        string cloudTable, string localSelect, string[] cloudColumns, List<string> errors)
    {
        // Clear cloud table
        try
        {
            using var del = new Npgsql.NpgsqlCommand($@"DELETE FROM ""{cloudTable}""", cloud);
            await del.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            var msg = $"Clear {cloudTable}: {ex.Message}";
            Log($"  {msg}");
            errors.Add(msg);
            return 0;
        }

        int count = 0;
        int failCount = 0;
        try
        {
            using var cmd = new SqlCommand(localSelect, local);
            cmd.CommandTimeout = 60;
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var cols = new List<string>();
                var vals = new List<string>();
                var pars = new List<Npgsql.NpgsqlParameter>();

                for (int i = 0; i < reader.FieldCount && i < cloudColumns.Length; i++)
                {
                    cols.Add($@"""{cloudColumns[i]}""");
                    vals.Add($"@p{i}");

                    object val = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    if (val is TimeSpan ts) val = ts.ToString(@"hh\:mm\:ss");
                    if (val is byte[]) val = DBNull.Value; // Skip binary data

                    pars.Add(new Npgsql.NpgsqlParameter($"p{i}", val));
                }

                try
                {
                    var sql = $@"INSERT INTO ""{cloudTable}"" ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
                    using var ins = new Npgsql.NpgsqlCommand(sql, cloud);
                    ins.Parameters.AddRange(pars.ToArray());
                    await ins.ExecuteNonQueryAsync();
                    count++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    if (failCount <= 3) // Log first 3 insert errors per table
                        Log($"  {cloudTable} insert error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            var msg = $"Read {cloudTable}: {ex.Message}";
            Log($"  {msg}");
            errors.Add(msg);
        }

        if (failCount > 0)
        {
            var msg = $"{cloudTable}: {failCount} rows failed to insert";
            Log($"  {msg}");
            errors.Add(msg);
        }

        Log($"  {cloudTable}: {count} records synced" + (failCount > 0 ? $", {failCount} failed" : ""));
        return count;
    }

    private static string? LoadCloudConnectionString()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                cs.TryGetProperty("CloudConnection", out var conn))
                return conn.GetString();
        }
        catch { }
        return null;
    }
}
