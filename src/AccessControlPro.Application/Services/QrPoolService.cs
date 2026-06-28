using System.IO;
using AccessControlPro.Domain.Entities;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

public interface IQrPoolService
{
    Task<int> GeneratePoolAsync(int count = 3500, int startFrom = 50001001, string source = "Local");
    Task<QrPoolEntry?> AssignCodeAsync(string guestName, string phone, string reason, string doorPermissions = "01010000");
    Task<int> GetAvailableCountAsync(string source = "Local");
    Task<bool> ValidateCodeAsync(string code);
    Task MarkUsedAsync(string code);
    Task<int> CleanupExpiredAsync();
    Task<List<QrPoolEntry>> GetAssignedAsync();
    Task DeactivateAsync(string code);
    Task<(int uploaded, int deleted, int generated)> SyncQrPoolToDeviceAsync(IAccessControlSdk sdk, List<DeviceInfo> devices, IProgress<(int done, int total)>? progress = null);
    Task<int> GetPendingUploadCountAsync();
    /// <summary>Pushes EVERY active pool code (daily-pass + visitor) to ONE device, ignoring the
    /// IsUploadedToDevice flag — used to repopulate a new/replacement controller. Reports progress.</summary>
    Task<(int pushed, int failed)> ForcePushPoolToDeviceAsync(IAccessControlSdk sdk, DeviceInfo device,
        IProgress<(int done, int total)>? progress = null, System.Threading.CancellationToken ct = default);
    /// <summary>Count of active pool codes (Local + Cloud) — for the "load to device?" prompt.</summary>
    Task<int> GetActivePoolCountAsync();
}

public class QrPoolService : IQrPoolService
{
    private readonly string _connectionString;
    private readonly int _configPoolSize;
    private readonly int _configRangeStart;

    // QR pool + pass operations both write to the same file — same family of bugs.
    internal static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "qr_log.txt");
    internal static void Log(string msg, string level = "info") =>
        RollingLogFile.Append(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}\n");

    public QrPoolService(string connectionString)
    {
        _connectionString = connectionString;
        _configPoolSize = 3500;
        _configRangeStart = 50001001;
    }

    /// <summary>
    /// Creates a QrPoolService with configurable pool size and range start.
    /// Use this constructor when QR settings come from cloud/Super Admin config.
    /// </summary>
    public QrPoolService(string connectionString, int poolSize, int rangeStart)
    {
        _connectionString = connectionString;
        _configPoolSize = poolSize > 0 ? poolSize : 3500;
        _configRangeStart = rangeStart > 0 ? rangeStart : 50001001;
    }

    /// <summary>Configured pool size (from appsettings or Super Admin)</summary>
    public int ConfigPoolSize => _configPoolSize;

    /// <summary>Configured range start (from appsettings or Super Admin)</summary>
    public int ConfigRangeStart => _configRangeStart;

    public async Task<int> GeneratePoolAsync(int count = 3500, int startFrom = 50001001, string source = "Local")
    {
        Log($"GeneratePool: requested count={count} startFrom={startFrom} source={source}");
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        int generated = 0;
        int skipped = 0;
        var validTo = DateTime.UtcNow.AddYears(1);

        for (int i = 0; i < count; i++)
        {
            var code = (startFrom + i).ToString();
            try
            {
                using var cmd = new SqlCommand(
                    @"IF NOT EXISTS (SELECT 1 FROM QrPool WHERE Code = @code)
                      INSERT INTO QrPool (Code, Status, Source, ValidFrom, ValidTo, CreatedAt)
                      VALUES (@code, 0, @source, GETUTCDATE(), @validTo, GETUTCDATE())", conn);
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@source", source);
                cmd.Parameters.AddWithValue("@validTo", validTo);
                await cmd.ExecuteNonQueryAsync();
                generated++;
            }
            catch { skipped++; /* duplicate or constraint */ }
        }
        Log($"GeneratePool DONE: generated={generated} skipped={skipped}");
        return generated;
    }

    public async Task<QrPoolEntry?> AssignCodeAsync(string guestName, string phone, string reason, string doorPermissions = "01010000")
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Pick random available code from LOCAL pool
        string? code;
        using (var pickCmd = new SqlCommand(
            "SELECT TOP 1 Code FROM QrPool WHERE Status = 0 AND Source = 'Local' ORDER BY NEWID()", conn))
        {
            var result = await pickCmd.ExecuteScalarAsync();
            code = result?.ToString();
        }

        if (string.IsNullOrEmpty(code))
        {
            Log($"AssignCode FAILED: no available Local codes (guest={guestName})", "warn");
            return null;
        }

        // Assign the code
        using (var updateCmd = new SqlCommand(
            @"UPDATE QrPool SET Status = 1, GuestName = @name, GuestPhone = @phone, Reason = @reason,
              DoorPermissions = @perms, AssignedAt = GETUTCDATE() WHERE Code = @code", conn))
        {
            updateCmd.Parameters.AddWithValue("@name", guestName);
            updateCmd.Parameters.AddWithValue("@phone", phone);
            updateCmd.Parameters.AddWithValue("@reason", reason);
            updateCmd.Parameters.AddWithValue("@perms", doorPermissions);
            updateCmd.Parameters.AddWithValue("@code", code);
            await updateCmd.ExecuteNonQueryAsync();
        }

        Log($"AssignCode OK: code={code} guest={guestName} doors={doorPermissions}");

        // Return the entry
        return await GetEntryByCodeAsync(conn, code);
    }

    public async Task<int> GetAvailableCountAsync(string source = "Local")
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM QrPool WHERE Status = 0 AND Source = @source", conn);
        cmd.Parameters.AddWithValue("@source", source);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> ValidateCodeAsync(string code)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM QrPool WHERE Code = @code AND Status = 1 AND ValidTo > GETUTCDATE() AND UsedCount < MaxUses",
            conn);
        cmd.Parameters.AddWithValue("@code", code);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task MarkUsedAsync(string code)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            @"UPDATE QrPool SET UsedCount = UsedCount + 1, UsedAt = GETUTCDATE(),
              Status = CASE WHEN UsedCount + 1 >= MaxUses THEN 2 ELSE Status END
              WHERE Code = @code", conn);
        cmd.Parameters.AddWithValue("@code", code);
        var rows = await cmd.ExecuteNonQueryAsync();
        Log($"MarkUsed: code={code} affected={rows}");
    }

    public async Task<int> CleanupExpiredAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            @"UPDATE QrPool SET Status = 3, ExpiredAt = GETUTCDATE()
              WHERE Status IN (0, 1) AND ValidTo < GETUTCDATE()", conn);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected > 0) Log($"CleanupExpired: expired={affected}");
        return affected;
    }

    public async Task<List<QrPoolEntry>> GetAssignedAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            @"SELECT Id, Code, Status, Source, GuestName, GuestPhone, Reason,
              AssignedAt, UsedAt, ExpiredAt, MaxUses, UsedCount, ValidFrom, ValidTo,
              DoorPermissions, CreatedAt, IsUploadedToDevice
              FROM QrPool WHERE Status = 1 ORDER BY AssignedAt DESC", conn);

        var list = new List<QrPoolEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadEntry(reader));
        }
        return list;
    }

    public async Task DeactivateAsync(string code)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            @"UPDATE QrPool SET Status = 3, ExpiredAt = GETUTCDATE() WHERE Code = @code", conn);
        cmd.Parameters.AddWithValue("@code", code);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<QrPoolEntry?> GetEntryByCodeAsync(SqlConnection conn, string code)
    {
        using var cmd = new SqlCommand(
            @"SELECT Id, Code, Status, Source, GuestName, GuestPhone, Reason,
              AssignedAt, UsedAt, ExpiredAt, MaxUses, UsedCount, ValidFrom, ValidTo,
              DoorPermissions, CreatedAt, IsUploadedToDevice
              FROM QrPool WHERE Code = @code", conn);
        cmd.Parameters.AddWithValue("@code", code);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return ReadEntry(reader);
        return null;
    }

    public async Task<(int uploaded, int deleted, int generated)> SyncQrPoolToDeviceAsync(
        IAccessControlSdk sdk, List<DeviceInfo> devices, IProgress<(int done, int total)>? progress = null)
    {
        Log($"SyncQrPoolToDevice: starting (devices={devices.Count})");
        int uploaded = 0, deleted = 0, generated = 0;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Step 1: Delete expired/used codes from device
        // Find codes that are Status=2 (Used) or Status=3 (Expired) and still IsUploadedToDevice=1
        using (var cmd = new SqlCommand(
            "SELECT Code FROM QrPool WHERE (Status = 2 OR Status = 3) AND IsUploadedToDevice = 1", conn))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var expiredCodes = new List<string>();
            while (await reader.ReadAsync())
                expiredCodes.Add(reader.GetString(0));
            await reader.CloseAsync();

            // Mark them as not uploaded (they'll expire on device naturally via effectiveTimes)
            foreach (var code in expiredCodes)
            {
                using var updateCmd = new SqlCommand(
                    "UPDATE QrPool SET IsUploadedToDevice = 0 WHERE Code = @c", conn);
                updateCmd.Parameters.AddWithValue("@c", code);
                await updateCmd.ExecuteNonQueryAsync();
                deleted++;
            }
        }

        // Step 2: Check available count, generate if needed
        int available;
        using (var countCmd = new SqlCommand(
            "SELECT COUNT(*) FROM QrPool WHERE Status = 0 AND Source = 'Local'", conn))
        {
            available = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
        }

        if (available < 500)
        {
            // Find max existing code
            int maxCode = _configRangeStart;
            using (var maxCmd = new SqlCommand(
                "SELECT MAX(CAST(Code AS INT)) FROM QrPool WHERE Source = 'Local'", conn))
            {
                var result = await maxCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    maxCode = Convert.ToInt32(result) + 1;
            }

            var localPoolSize = (int)(_configPoolSize * 0.6);
            generated = await GeneratePoolAsync(localPoolSize - available, maxCode, "Local");
        }

        // Step 3: Upload un-uploaded codes to all devices. Includes BOTH the local (Daily Pass)
        // segment AND the cloud (Visitor) segment, so a cloud-issued visitor QR — assigned from
        // the pre-loaded Cloud codes via the owner portal — actually opens the gate.
        using (var cmd = new SqlCommand(
            "SELECT Code, DoorPermissions, ValidTo FROM QrPool WHERE IsUploadedToDevice = 0 AND Status IN (0, 1) AND Source IN ('Local', 'Cloud')", conn))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var codesToUpload = new List<(string code, string doors, DateTime validTo)>();
            while (await reader.ReadAsync())
            {
                codesToUpload.Add((
                    reader.GetString(0),
                    reader.IsDBNull(1) ? "01010000" : reader.GetString(1),
                    reader.GetDateTime(2)
                ));
            }
            await reader.CloseAsync();

            int total = codesToUpload.Count, done = 0;
            foreach (var (code, doors, validTo) in codesToUpload)
            {
                bool success = false;
                Exception? lastErr = null;
                foreach (var device in devices)
                {
                    try
                    {
                        var permitTime = validTo.ToString("yyyy-MM-dd HH:mm:ss");
                        sdk.AddAccessCard(device, code, "", 0, doors, permitTime, 2, 0, false);
                        success = true;
                    }
                    catch (Exception ex) { lastErr = ex; /* device might be offline */ }
                }

                if (success)
                {
                    using var updateCmd = new SqlCommand(
                        "UPDATE QrPool SET IsUploadedToDevice = 1 WHERE Code = @c", conn);
                    updateCmd.Parameters.AddWithValue("@c", code);
                    await updateCmd.ExecuteNonQueryAsync();
                    uploaded++;
                }
                else if (lastErr != null)
                {
                    Log($"  upload-to-device FAILED for code={code}: {lastErr.Message}", "error");
                }
                done++;
                progress?.Report((done, total));
            }
        }

        Log($"SyncQrPoolToDevice DONE: uploaded={uploaded} deleted={deleted} generated={generated}");
        return (uploaded, deleted, generated);
    }

    public async Task<int> GetActivePoolCountAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM QrPool WHERE Status IN (0, 1) AND Source IN ('Local', 'Cloud')", conn);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    public async Task<(int pushed, int failed)> ForcePushPoolToDeviceAsync(IAccessControlSdk sdk, DeviceInfo device,
        IProgress<(int done, int total)>? progress = null, System.Threading.CancellationToken ct = default)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var codes = new List<(string code, string doors, DateTime validTo)>();
        using (var cmd = new SqlCommand(
            "SELECT Code, DoorPermissions, ValidTo FROM QrPool WHERE Status IN (0, 1) AND Source IN ('Local', 'Cloud')", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                codes.Add((reader.GetString(0),
                          reader.IsDBNull(1) ? "01010000" : reader.GetString(1),
                          reader.GetDateTime(2)));
        }

        int pushed = 0, failed = 0, total = codes.Count, done = 0;
        foreach (var (code, doors, validTo) in codes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                sdk.AddAccessCard(device, code, "", 0, doors, validTo.ToString("yyyy-MM-dd HH:mm:ss"), 2, 0, false);
                using var up = new SqlCommand("UPDATE QrPool SET IsUploadedToDevice = 1 WHERE Code = @c", conn);
                up.Parameters.AddWithValue("@c", code);
                await up.ExecuteNonQueryAsync(ct);
                pushed++;
            }
            catch (Exception ex)
            {
                failed++;
                Log($"ForcePushPoolToDevice code={code} failed: {ex.Message}", "error");
                // If nothing is landing, the device is unreachable — abort rather than grind through thousands.
                if (pushed == 0 && failed >= 15)
                    throw new InvalidOperationException("Device is not responding — QR pool upload aborted.");
            }
            done++;
            if (done % 10 == 0 || done == total) progress?.Report((done, total));
        }
        Log($"ForcePushPoolToDevice DONE: pushed={pushed} failed={failed} total={total}");
        return (pushed, failed);
    }

    public async Task<int> GetPendingUploadCountAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM QrPool WHERE IsUploadedToDevice = 0 AND Status IN (0, 1)", conn);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    private static QrPoolEntry ReadEntry(SqlDataReader reader)
    {
        return new QrPoolEntry
        {
            Id = reader.GetInt32(0),
            Code = reader.GetString(1),
            Status = reader.GetInt32(2),
            Source = reader.GetString(3),
            GuestName = reader.IsDBNull(4) ? "" : reader.GetString(4),
            GuestPhone = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Reason = reader.IsDBNull(6) ? "" : reader.GetString(6),
            AssignedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            UsedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            ExpiredAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            MaxUses = reader.GetInt32(10),
            UsedCount = reader.GetInt32(11),
            ValidFrom = reader.GetDateTime(12),
            ValidTo = reader.GetDateTime(13),
            DoorPermissions = reader.IsDBNull(14) ? "01010000" : reader.GetString(14),
            CreatedAt = reader.GetDateTime(15),
            IsUploadedToDevice = reader.GetBoolean(16)
        };
    }
}
