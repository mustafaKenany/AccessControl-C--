using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

public class BackupStatus
{
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastAttempt { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    // Disk-space warning state — surfaced to the UI so the customer sees a toast
    // when free space drops below WarnMb and an error toast when it drops below RefuseMb.
    public bool LowDiskWarning { get; set; }
    public long LastFreeSpaceMb { get; set; }
}

public interface IBackupService
{
    Task<string> RunBackupAsync();
}

public class BackupService : IBackupService
{
    private readonly string _connectionString;
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "backup_log.txt");
    private static readonly string StatusPath = Path.Combine(AppContext.BaseDirectory, ".backup_status");
    private const int MaxRetries = 3;
    private const int RetryIntervalMinutes = 30;

    // Disk-space thresholds. WarnMb = log + flag status so the UI can show a toast.
    // RefuseMb = abort the backup so we don't write a 0-byte / corrupt .bak that
    // would overwrite a good one and leave the customer with no restore point.
    private const long DiskWarnMb = 500;
    private const long DiskRefuseMb = 100;

    public BackupService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static void Log(string msg)
    {
        RollingLogFile.Append(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
    }

    public static BackupStatus LoadStatus()
    {
        try
        {
            if (File.Exists(StatusPath))
            {
                var json = File.ReadAllText(StatusPath);
                return JsonSerializer.Deserialize<BackupStatus>(json) ?? new BackupStatus();
            }
        }
        catch { }
        return new BackupStatus();
    }

    public static void SaveStatus(BackupStatus status)
    {
        try
        {
            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatusPath, json);
        }
        catch { }
    }

    public async Task<string> RunBackupAsync()
    {
        var backupPath = LoadBackupPath();
        if (string.IsNullOrEmpty(backupPath))
        {
            Log("Backup skipped: no backup path configured");
            return "No backup path configured";
        }

        // Guard against a BackupPath whose drive doesn't exist on this machine —
        // e.g. appsettings.json copied from a PC that had a D: partition onto a PC
        // that only has C:. Without this, Directory.CreateDirectory throws
        // DirectoryNotFoundException, the retry loop waits 30 min between each of 3
        // attempts, and the on-launch backup splash appears frozen for ~1 hour.
        if (!DriveExists(backupPath))
        {
            var fallback = DefaultBackupPath();
            Log($"Backup path '{backupPath}' is on a drive that does not exist on this PC — falling back to '{fallback}'");
            backupPath = fallback;
        }

        var status = LoadStatus();
        string lastResult = "";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Log($"Backup attempt {attempt} of {MaxRetries}...");

                // Ensure backup directory exists
                Directory.CreateDirectory(backupPath);

                // Step 1: Delete old backups (older than 3 days)
                // 3 days = 6 auto-backups kept (2/day) = covers a weekend if a backup fails
                Log("Step 1: Cleaning old backups...");
                DeleteOldBackups(backupPath, 3);

                // Step 1b: Disk-space guard — after cleanup, before writing the new .bak.
                // Cleanup-first order matters: deleting yesterday's .bak might free enough
                // space to bring us back above the refuse threshold.
                var freeMb = GetFreeDiskSpaceMb(backupPath);
                status.LastFreeSpaceMb = freeMb;
                if (freeMb >= 0 && freeMb < DiskRefuseMb)
                {
                    status.LowDiskWarning = true;
                    SaveStatus(status);
                    var msg = $"Backup REFUSED: only {freeMb} MB free on backup drive (threshold {DiskRefuseMb} MB). Free disk space and retry.";
                    Log(msg);
                    return msg;
                }
                status.LowDiskWarning = freeMb >= 0 && freeMb < DiskWarnMb;
                if (status.LowDiskWarning)
                    Log($"WARNING: backup drive has only {freeMb} MB free (threshold {DiskWarnMb} MB) — backup will proceed but free up space soon");

                // Step 2: Optimize database
                Log("Step 2: Optimizing database...");
                await OptimizeDatabaseAsync();

                // Step 3: Run backup
                var fileName = $"AccessControlPro_{DateTime.Now:yyyy-MM-dd_HH-mm}.bak";
                var fullPath = Path.Combine(backupPath, fileName);

                Log($"Step 3: Creating backup: {fullPath}");
                await CreateBackupAsync(fullPath);

                var fileSize = new FileInfo(fullPath).Length / 1024.0 / 1024.0;
                lastResult = $"Backup completed: {fileName} ({fileSize:F1} MB)";
                Log(lastResult);

                // Success — update status
                status.LastSuccess = DateTime.Now;
                status.LastAttempt = DateTime.Now;
                status.LastError = null;
                status.ConsecutiveFailures = 0;
                SaveStatus(status);

                return lastResult;
            }
            catch (Exception ex)
            {
                Log($"Backup attempt {attempt} FAILED: {ex.Message}");
                lastResult = $"Backup failed: {ex.Message}";

                // Update status with failure
                status.LastAttempt = DateTime.Now;
                status.LastError = ex.Message;
                status.ConsecutiveFailures++;
                SaveStatus(status);

                // Only delay-retry for transient errors (SQL connectivity, timeouts —
                // e.g. SQL Server restarting during a Windows Update). A bad path, missing
                // directory, or permission error won't fix itself by waiting, and a 30-min
                // wait would freeze the on-launch backup splash. Fail fast on those.
                bool transient = ex is SqlException || ex is TimeoutException;
                if (attempt < MaxRetries && transient)
                {
                    Log($"Waiting {RetryIntervalMinutes} minutes before retry...");
                    await Task.Delay(TimeSpan.FromMinutes(RetryIntervalMinutes));
                }
                else if (!transient)
                {
                    Log("Non-transient error — not retrying.");
                    break;
                }
            }
        }

        return lastResult;
    }

    private async Task CreateBackupAsync(string filePath)
    {
        // Extract database name from connection string
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var dbName = builder.InitialCatalog;

        var sql = $@"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION, NAME = 'AutoBackup'";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.CommandTimeout = 300; // 5 minutes max
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task OptimizeDatabaseAsync()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = builder.InitialCatalog;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Shrink log file
            using (var cmd = new SqlCommand($"DBCC SHRINKFILE (N'{dbName}_log', 1)", conn))
            {
                cmd.CommandTimeout = 120;
                try { await cmd.ExecuteNonQueryAsync(); } catch { /* log shrink might fail on Express */ }
            }

            // Rebuild indexes on main tables
            var tables = new[] { "Employees", "AccessCards", "AccessEvents", "Transactions", "AuditLogs" };
            foreach (var table in tables)
            {
                try
                {
                    using var cmd = new SqlCommand($"IF OBJECT_ID('{table}') IS NOT NULL ALTER INDEX ALL ON [{table}] REBUILD", conn);
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { /* table might not exist */ }
            }

            // Update statistics
            using (var cmd = new SqlCommand("EXEC sp_updatestats", conn))
            {
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
            }

            Log("Database optimization completed");
        }
        catch (Exception ex)
        {
            Log($"Optimization warning (non-critical): {ex.Message}");
        }
    }

    private void DeleteOldBackups(string backupPath, int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            var files = Directory.GetFiles(backupPath, "AccessControlPro_*.bak");
            int deleted = 0;
            foreach (var file in files)
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            if (deleted > 0)
                Log($"Deleted {deleted} old backup(s)");
        }
        catch (Exception ex)
        {
            Log($"Cleanup warning: {ex.Message}");
        }
    }

    private static long GetFreeDiskSpaceMb(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return -1;
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return -1;
            return drive.AvailableFreeSpace / 1024 / 1024;
        }
        catch
        {
            return -1; // unknown — treat as "don't block"
        }
    }

    // Fallback backup location when the configured BackupPath points at a drive
    // that doesn't exist on this machine. Always on the system drive, so it always
    // resolves — and matches the setup wizard's default for a blank BackupPath.
    private static string DefaultBackupPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "AccessControlPro", "Backups");

    private static bool DriveExists(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return false;
            return new DriveInfo(root).IsReady;
        }
        catch { return false; }
    }

    private static string? LoadBackupPath()
    {
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath)) return null;
            var json = File.ReadAllText(settingsPath);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("BackupPath", out var val))
                return val.GetString();
        }
        catch { }
        return null;
    }
}
