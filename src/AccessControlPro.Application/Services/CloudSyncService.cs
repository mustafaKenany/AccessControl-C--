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

        try
        {
            Log("Starting cloud sync...");

            using var local = new SqlConnection(_localConnectionString);
            await local.OpenAsync();

            using var cloud = new Npgsql.NpgsqlConnection(cloudConn);
            await cloud.OpenAsync();

            int total = 0;

            // Sync Players (Employees → Players)
            total += await SyncWithMappingAsync(local, cloud, "Players",
                "SELECT Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType, StartDate, EndDate, " +
                "SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, IsFrozen, FreezeStartDate, " +
                "IsDeleted, CreatedAt, Height, Weight, Notes FROM Employees",
                new[] { "Id", "FullNameEn", "FullNameAr", "CardNo", "Phone", "SubscriptionType", "StartDate", "EndDate",
                        "SubscriptionFee", "AmountPaid", "MaxVisits", "UsedVisits", "IsFrozen", "FreezeStartDate",
                        "IsDeleted", "CreatedAt", "Height", "Weight", "Notes" });

            // Sync Events
            total += await SyncWithMappingAsync(local, cloud, "AccessEvents",
                "SELECT TOP 2000 Id, DoorId, CardId, EventType, EventCode, Timestamp, Details " +
                "FROM AccessEvents ORDER BY Timestamp DESC",
                new[] { "Id", "DoorId", "CardId", "RecordType", "EventCode", "EventDate", "Details" });

            // Sync Devices
            total += await SyncWithMappingAsync(local, cloud, "Devices",
                "SELECT Id, Name, SerialNumber, IP, MAC FROM Devices",
                new[] { "Id", "Name", "SerialNumber", "IP", "MAC" });

            // Sync Doors
            total += await SyncWithMappingAsync(local, cloud, "Doors",
                "SELECT Id, DeviceId, Name, DoorNumber FROM Doors",
                new[] { "Id", "DeviceId", "Name", "DoorNumber" });

            // Sync Transactions
            total += await SyncWithMappingAsync(local, cloud, "Transactions",
                "SELECT Id, Type, Category, Amount, Description, RelatedEmployeeId, CreatedAt, CreatedBy " +
                "FROM Transactions",
                new[] { "Id", "Type", "Category", "Amount", "Description", "RelatedEmployeeId", "TransactionDate", "RecordedBy" });

            // Sync Users
            total += await SyncWithMappingAsync(local, cloud, "Users",
                "SELECT Id, Username, PasswordHash, DisplayName, Role, IsActive, Permissions FROM Users",
                new[] { "Id", "Username", "PasswordHash", "DisplayName", "Role", "IsActive", "Permissions" });

            // Sync AppSettings
            total += await SyncWithMappingAsync(local, cloud, "AppSettings",
                "SELECT TOP 1 Id, GymName FROM AppSettings",
                new[] { "Id", "GymName" });

            // Sync AuditLogs
            total += await SyncWithMappingAsync(local, cloud, "AuditLogs",
                "SELECT TOP 1000 Id, Action, EntityType, EntityId, Details, DetailsAr, PerformedBy, Timestamp " +
                "FROM AuditLogs ORDER BY Timestamp DESC",
                new[] { "Id", "Action", "EntityType", "EntityId", "Details", "DetailsAr", "PerformedBy", "Timestamp" });

            // Sync DeletedEmployees
            total += await SyncWithMappingAsync(local, cloud, "DeletedEmployees",
                "SELECT Id, OriginalId, FullNameEn, FullNameAr, CardNo, Phone, DeleteReason, DeletedBy, DeletedAt " +
                "FROM DeletedEmployees",
                new[] { "Id", "OriginalId", "FullNameEn", "FullNameAr", "CardNo", "Phone", "DeleteReason", "DeletedBy", "DeletedAt" });

            // Log sync
            try
            {
                using var logCmd = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO ""CloudSyncLogs"" (""SyncType"", ""Status"", ""Details"", ""SyncedAt"")
                      VALUES (@t, @s, @d, @ts)", cloud);
                logCmd.Parameters.AddWithValue("t", "FullSync");
                logCmd.Parameters.AddWithValue("s", "Success");
                logCmd.Parameters.AddWithValue("d", $"Synced {total} records");
                logCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
                await logCmd.ExecuteNonQueryAsync();
            }
            catch { }

            Log($"Cloud sync completed: {total} records");
            return $"Synced {total} records";
        }
        catch (Exception ex)
        {
            Log($"Cloud sync FAILED: {ex.Message}");
            return $"Sync failed: {ex.Message}";
        }
    }

    private async Task<int> SyncWithMappingAsync(SqlConnection local, Npgsql.NpgsqlConnection cloud,
        string cloudTable, string localSelect, string[] cloudColumns)
    {
        // Clear cloud table
        try
        {
            using var del = new Npgsql.NpgsqlCommand($@"DELETE FROM ""{cloudTable}""", cloud);
            await del.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log($"  Clear {cloudTable}: {ex.Message}");
            return 0;
        }

        int count = 0;
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
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log($"  Read {cloudTable}: {ex.Message}");
        }

        Log($"  {cloudTable}: {count} records");
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
