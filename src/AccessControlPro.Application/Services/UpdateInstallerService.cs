using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;

namespace AccessControlPro.Application.Services;

public interface IUpdateInstallerService
{
    Task<UpdateInstallResult> DownloadAndStageAsync(
        VersionManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default);

    void TriggerInstallAndExit(UpdateInstallResult staged);
}

public class UpdateProgress
{
    public string Phase { get; set; } = "";  // "downloading" | "verifying" | "backing-up" | "ready"
    public long BytesDownloaded { get; set; }
    public long BytesTotal { get; set; }
    public int PercentComplete { get; set; }
}

public class UpdateInstallResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StagedZipPath { get; set; }
    public string? TargetVersion { get; set; }
}

// Downloads a release ZIP, verifies its SHA-256 hash, runs a pre-update backup,
// writes a `pending_update.json` sentinel, and launches the Updater.exe helper
// before exiting the WPF app. The actual file-swap happens in Updater.exe so
// the running WPF process doesn't try to overwrite its own DLLs.
public class UpdateInstallerService : IUpdateInstallerService
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private readonly IBackupService _backupService;

    public UpdateInstallerService(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public async Task<UpdateInstallResult> DownloadAndStageAsync(
        VersionManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new UpdateInstallResult { TargetVersion = manifest.Version };

        try
        {
            // -------- Step 1: Download --------
            progress?.Report(new UpdateProgress { Phase = "downloading", BytesTotal = manifest.SizeBytes });

            var stagingDir = Path.Combine(AppContext.BaseDirectory, "update_staging");
            Directory.CreateDirectory(stagingDir);
            // Clean previous staging attempts so we don't ship with stale partial files
            foreach (var f in Directory.GetFiles(stagingDir)) { try { File.Delete(f); } catch { } }

            var zipPath = Path.Combine(stagingDir, $"AccessControlPro-v{manifest.Version}.zip");

            using (var http = new HttpClient { Timeout = DownloadTimeout })
            {
                using var resp = await http.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Download failed: HTTP {(int)resp.StatusCode}";
                    return result;
                }

                var total = resp.Content.Headers.ContentLength ?? manifest.SizeBytes;
                using var src = await resp.Content.ReadAsStreamAsync(ct);
                using var dst = File.Create(zipPath);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;

                    if (total > 0 && progress != null)
                    {
                        progress.Report(new UpdateProgress
                        {
                            Phase = "downloading",
                            BytesDownloaded = downloaded,
                            BytesTotal = total,
                            PercentComplete = (int)((downloaded * 100L) / total)
                        });
                    }
                }
            }

            // -------- Step 2: Verify SHA-256 --------
            // If the hash is missing in the manifest, skip verification (older release format).
            // If present, mismatch = abort + delete the bad ZIP.
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                progress?.Report(new UpdateProgress { Phase = "verifying", PercentComplete = 100 });

                using var fs = File.OpenRead(zipPath);
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(fs, ct);
                var actualHash = Convert.ToHexString(hashBytes);

                if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(zipPath); } catch { }
                    result.ErrorMessage = $"Integrity check failed (expected {manifest.Sha256.Substring(0, 12)}..., got {actualHash.Substring(0, 12)}...)";
                    return result;
                }
            }

            // -------- Step 3: Safety backup before swap --------
            progress?.Report(new UpdateProgress { Phase = "backing-up", PercentComplete = 100 });
            try
            {
                await _backupService.RunBackupAsync();
            }
            catch
            {
                // Backup failure is non-fatal — the update can still proceed.
                // The user already has the most recent scheduled backup.
            }

            // -------- Step 4: Write the pending-update sentinel --------
            // Updater.exe reads this on launch to know what to extract and where.
            var sentinel = new
            {
                version = manifest.Version,
                zipPath,
                targetDir = AppContext.BaseDirectory.TrimEnd('\\', '/'),
                releasedAt = manifest.ReleasedAt,
                releaseNotes_en = manifest.ReleaseNotesEn,
                releaseNotes_ar = manifest.ReleaseNotesAr,
                stagedAt = DateTime.UtcNow
            };
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "pending_update.json"),
                JsonSerializer.Serialize(sentinel, new JsonSerializerOptions { WriteIndented = true }));

            result.Success = true;
            result.StagedZipPath = zipPath;
            progress?.Report(new UpdateProgress { Phase = "ready", PercentComplete = 100 });
            return result;
        }
        catch (TaskCanceledException)
        {
            result.ErrorMessage = "Download cancelled or timed out";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Update staging failed: {ex.Message}";
            return result;
        }
    }

    // Launches Updater.exe (which waits for our process to exit, then swaps files,
    // then relaunches the main app) and exits the WPF process so file locks release.
    public void TriggerInstallAndExit(UpdateInstallResult staged)
    {
        if (!staged.Success || string.IsNullOrEmpty(staged.StagedZipPath))
            throw new InvalidOperationException("Cannot trigger install on an unsuccessful stage");

        var updaterExe = Path.Combine(AppContext.BaseDirectory, "Updater.exe");
        if (!File.Exists(updaterExe))
            throw new FileNotFoundException("Updater.exe not found in install folder", updaterExe);

        var pid = Environment.ProcessId;
        var psi = new ProcessStartInfo
        {
            FileName = updaterExe,
            // Arg 1: PID of the running WPF app (Updater waits for it to exit)
            // Arg 2: install folder (where to extract)
            // Updater reads pending_update.json from the install folder for everything else.
            Arguments = $"{pid} \"{AppContext.BaseDirectory.TrimEnd('\\', '/')}\"",
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        Process.Start(psi);

        // Exit the WPF app so Updater can release file locks
        // Don't use Application.Current.Shutdown() here — caller should.
    }
}
