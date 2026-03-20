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
            return "Cloud sync disabled - configure CloudConnection in appsettings.json";

        try
        {
            Log("Starting cloud sync...");

            using var localConn = new SqlConnection(_localConnectionString);
            await localConn.OpenAsync();

            using var cloudConnPg = new Npgsql.NpgsqlConnection(cloudConn);
            await cloudConnPg.OpenAsync();

            int total = 0;

            // Sync Players (from Employees table)
            total += await SyncTableAsync(localConn, cloudConnPg, "Players",
                "SELECT Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType, " +
                "StartDate, EndDate, SubscriptionFee, AmountPaid, MaxVisits, UsedVisits, " +
                "IsFrozen, FreezeStartDate, CreatedAt, Height, Weight, Notes, CardBalance " +
                "FROM Employees");

            // Sync AccessCards
            total += await SyncTableAsync(localConn, cloudConnPg, "AccessCards",
                "SELECT Id, EmployeeId, CardNumber, CardPassword, CardType, OpenMode, " +
                "DoorPermissions, EffectiveTimes, TimePeriodIndex, HolidayEnabled, " +
                "IsActive, IsSyncedToDevice, ValidFrom, ValidTo, CreatedAt " +
                "FROM AccessCards");

            // Sync AccessEvents (recent 1000)
            total += await SyncTableAsync(localConn, cloudConnPg, "AccessEvents",
                "SELECT TOP 1000 Id, DoorId, CardId, EventType, EventCode, " +
                "Timestamp, Details FROM AccessEvents ORDER BY Timestamp DESC");

            // Sync Devices
            total += await SyncTableAsync(localConn, cloudConnPg, "Devices",
                "SELECT Id, Name, SerialNumber, IP, MAC, TCPPort, IsOnline FROM Devices");

            // Sync Doors
            total += await SyncTableAsync(localConn, cloudConnPg, "Doors",
                "SELECT Id, DeviceId, Name, DoorNumber, IsLocked, " +
                "WorkStartTime, WorkEndTime, Is24Hours, WorkingDays FROM Doors");

            // Sync Transactions
            total += await SyncTableAsync(localConn, cloudConnPg, "Transactions",
                "SELECT Id, Type, Category, Amount, Description, " +
                "RelatedEmployeeId, PaymentMethod, CreatedBy, CreatedAt FROM Transactions");

            // Sync Users (for owner login)
            total += await SyncTableAsync(localConn, cloudConnPg, "Users",
                "SELECT Id, Username, PasswordHash, DisplayName, Role, IsActive, " +
                "CreatedAt, Permissions FROM Users");

            // Sync AppSettings
            total += await SyncTableAsync(localConn, cloudConnPg, "AppSettings",
                "SELECT Id, CompanyName, GymName, LogoPath, DevLogoPath, Phone, Address FROM AppSettings");

            // Log sync result to cloud
            await LogSyncResultAsync(cloudConnPg, total, null);

            Log($"Cloud sync completed: {total} records synced");
            return $"Synced {total} records to cloud";
        }
        catch (Exception ex)
        {
            Log($"Cloud sync FAILED: {ex.Message}");
            return $"Sync failed: {ex.Message}";
        }
    }

    private async Task<int> SyncTableAsync(SqlConnection local, Npgsql.NpgsqlConnection cloud,
        string tableName, string selectSql)
    {
        try
        {
            // Clear existing data in cloud table
            using var deleteCmd = new Npgsql.NpgsqlCommand($"DELETE FROM \"{tableName}\"", cloud);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Table might not exist yet - EnsureCreated should handle this
        }

        int count = 0;
        try
        {
            using var selectCmd = new SqlCommand(selectSql, local);
            selectCmd.CommandTimeout = 30;
            using var reader = await selectCmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var columns = new List<string>();
                var values = new List<string>();
                var parameters = new List<Npgsql.NpgsqlParameter>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    columns.Add($"\"{colName}\"");
                    values.Add($"@p{i}");

                    object val = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);

                    // Convert TimeSpan to a format PostgreSQL understands
                    if (val is TimeSpan ts)
                        val = ts.ToString(@"hh\:mm\:ss");

                    parameters.Add(new Npgsql.NpgsqlParameter($"p{i}", val));
                }

                var insertSql = $"INSERT INTO \"{tableName}\" ({string.Join(",", columns)}) " +
                                $"VALUES ({string.Join(",", values)}) ON CONFLICT (\"Id\") DO NOTHING";
                try
                {
                    using var insertCmd = new Npgsql.NpgsqlCommand(insertSql, cloud);
                    insertCmd.Parameters.AddRange(parameters.ToArray());
                    await insertCmd.ExecuteNonQueryAsync();
                    count++;
                }
                catch (Exception ex)
                {
                    Log($"  Skip {tableName} record: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"  Error reading {tableName}: {ex.Message}");
        }

        return count;
    }

    private static async Task LogSyncResultAsync(Npgsql.NpgsqlConnection cloud, int total, string? error)
    {
        try
        {
            var sql = "INSERT INTO \"CloudSyncLogs\" (\"Id\", \"SyncedAt\", \"TableName\", \"RecordCount\", \"Status\", \"Error\") " +
                      "VALUES (DEFAULT, @ts, @tn, @rc, @st, @err)";

            // Try with DEFAULT id first, fallback to explicit
            try
            {
                using var cmd = new Npgsql.NpgsqlCommand(sql, cloud);
                cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("tn", "ALL");
                cmd.Parameters.AddWithValue("rc", total);
                cmd.Parameters.AddWithValue("st", error == null ? "Success" : "Failed");
                cmd.Parameters.AddWithValue("err", (object?)error ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* sync log is best-effort */ }
        }
        catch { }
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
