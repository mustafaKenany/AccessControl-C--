using System.Diagnostics;
using System.IO;
using AccessControlPro.SDK.Models;

namespace AccessControlPro.Application.Helpers;

/// <summary>
/// Spawns a separate process to execute SDK card operations.
/// This avoids the TCP stuck issue caused by monitoring corrupting the SDK native state.
/// </summary>
public static class CardSyncRunner
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "sdk_log.txt");

    private static void Log(string msg)
    {
        AccessControlPro.Application.Services.RollingLogFile.Append(
            LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
    }

    /// <summary>
    /// Executes addUnSortCard in a separate process. Returns true if successful.
    /// </summary>
    public static async Task<bool> AddCardViaSubprocessAsync(
        DeviceInfo device, string cardNo, string cardPassword,
        int openMode, string doorPermissions, string permitTime,
        int effectiveTimes, string timePeriodIndex, int holidayEnabled)
    {
        var paramsFile = Path.Combine(Path.GetTempPath(), $"cardsync_{Guid.NewGuid():N}.tmp");
        var cardSyncExe = Path.Combine(AppContext.BaseDirectory, "AccessControlPro.CardSync.exe");

        // Fallback: if CardSync.exe not found, look for CardSync.dll
        if (!File.Exists(cardSyncExe))
        {
            var cardSyncDll = Path.Combine(AppContext.BaseDirectory, "AccessControlPro.CardSync.dll");
            if (File.Exists(cardSyncDll))
                cardSyncExe = cardSyncDll;
            else
            {
                Log("CardSyncRunner: CardSync.exe not found");
                return false;
            }
        }

        try
        {
            // Write params file
            var lines = new[]
            {
                device.SerialNumber,
                device.IP,
                device.TCPPort.ToString(),
                device.Password ?? "",
                cardNo,
                cardPassword ?? "",
                openMode.ToString(),
                doorPermissions,
                permitTime,
                effectiveTimes.ToString(),
                timePeriodIndex ?? "00000000",
                holidayEnabled.ToString()
            };
            await File.WriteAllLinesAsync(paramsFile, lines);

            Log($"CardSyncRunner: Spawning subprocess for card {cardNo} on device {device.SerialNumber}");

            var psi = new ProcessStartInfo
            {
                FileName = cardSyncExe.EndsWith(".dll") ? "dotnet" : cardSyncExe,
                Arguments = cardSyncExe.EndsWith(".dll") ? $"\"{cardSyncExe}\" \"{paramsFile}\"" : $"\"{paramsFile}\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log("CardSyncRunner: Failed to start process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            // Wait max 30 seconds
            var exited = await Task.Run(() => process.WaitForExit(30000));
            if (!exited)
            {
                process.Kill();
                Log("CardSyncRunner: Process timed out (30s)");
                return false;
            }

            var result = output.Trim();
            var success = process.ExitCode == 0 && result == "1";

            Log($"CardSyncRunner: ExitCode={process.ExitCode}, Output={result}, Error={error.Trim()}, Success={success}");
            return success;
        }
        catch (Exception ex)
        {
            Log($"CardSyncRunner: Exception - {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(paramsFile)) File.Delete(paramsFile); } catch { }
        }
    }
}
