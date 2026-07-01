// AccessControlPro Updater — minimal helper that swaps program files for a new
// version while the main WPF app is shut down. Runs as a separate process so it
// doesn't try to overwrite its own DLLs.
//
// Args: <main_app_pid> <install_folder>
// Reads: pending_update.json in install_folder (written by UpdateInstallerService)
// Effect: extracts the staged ZIP over the install folder, preserving user data,
//         then relaunches AccessControlPro.WPF.exe.
//
// No UI framework dependency — uses Win32 MessageBox via P/Invoke for fatal errors.
// During the install we show progress through the console window's title bar.

using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AccessControlPro.Updater;

public class Program
{
    // Items we NEVER overwrite during an update — these contain the customer's
    // gym data, credentials, and operational state.
    private static readonly HashSet<string> PreservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "appsettings.json",
        "License.dat",
        ".backup_status",
        "last_run_state.json",
        "last_sync.json",
        ".last_backup",
        "pending_update.json",
        ".update_snooze",
    };

    private static readonly HashSet<string> PreservedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Logs",
        "Backups",
        "update_staging",
        "_rollback",
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string src, string? dst, int flags);

    private const uint MB_ICONERROR = 0x10;
    private const uint MB_ICONINFORMATION = 0x40;
    private const uint MB_OK = 0x0;
    private const int SW_SHOW = 5;
    private const int MOVEFILE_REPLACE_EXISTING = 0x1;
    private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

    public static int Main(string[] args)
    {
        // Make sure the console window is visible (WinExe hides it by default)
        ShowWindow(GetConsoleWindow(), SW_SHOW);
        SetTitle("AccessControlPro Updater");

        if (args.Length < 2)
        {
            ShowError("Updater was launched with the wrong arguments. Please relaunch AccessControlPro normally.");
            return 1;
        }

        // -------- Step 0: Self-relocate so we can overwrite our own Updater.exe --------
        // The Updater ships INSIDE the release ZIP and is launched FROM the install folder,
        // so extracting the new Updater.exe over the running one fails with "the process
        // cannot access the file because it is being used by another process" — which aborts
        // the whole update. Fix: copy ourselves to %TEMP% and relaunch from there. The temp
        // copy is free to overwrite the install-folder Updater.exe. The "--relocated" sentinel
        // prevents an infinite relaunch loop.
        const string RelocatedFlag = "--relocated";
        if (!args.Contains(RelocatedFlag))
        {
            try
            {
                var selfPath = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule!.FileName;
                var tempDir = Path.Combine(Path.GetTempPath(), $"acp_updater_{Environment.ProcessId}");
                Directory.CreateDirectory(tempDir);
                var tempExe = Path.Combine(tempDir, "Updater.exe");
                File.Copy(selfPath!, tempExe, overwrite: true);

                // Re-quote args that contain spaces (e.g. the install path) so the temp copy
                // receives them intact, then append the sentinel flag.
                var relaunchArgs = string.Join(" ",
                    args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)) + " " + RelocatedFlag;

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = relaunchArgs,
                    UseShellExecute = true,
                    WorkingDirectory = tempDir
                });
                return 0; // install-folder copy exits; the temp copy takes over the update
            }
            catch
            {
                // If relocation fails for any reason, fall through and run in place — the
                // per-file retry/skip below still applies, so at worst Updater.exe stays old.
            }
        }

        if (!int.TryParse(args[0], out var mainPid))
        {
            ShowError("Invalid main-app process ID.");
            return 1;
        }

        var installFolder = args[1].TrimEnd('\\', '/');
        var sentinelPath = Path.Combine(installFolder, "pending_update.json");
        var logPath = Path.Combine(installFolder, "Logs", "updater.log");

        try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); } catch { }

        Log(logPath, "===========================================");
        Log(logPath, $"Updater started. MainPid={mainPid} InstallFolder={installFolder}");

        try
        {
            // -------- Step 1: Wait for the main app to exit, then close ALL sibling apps --------
            // The main WPF app hands us its PID, but Admin and POS are separate processes that
            // ALSO load the shared WPF framework DLLs (e.g. WindowsBase.dll). If either is still
            // running, those DLLs stay locked and the extract fails ("being used by another
            // process") — aborting the whole update. So after the main app exits, close every
            // AccessControlPro.* process by name before touching any file.
            SetStatus("Waiting for AccessControlPro to close...");
            Log(logPath, "Waiting for main app to exit...");
            WaitForProcessExit(mainPid, TimeSpan.FromSeconds(30), logPath);
            KillRelatedProcesses(logPath);

            // -------- Step 2: Read the pending-update sentinel --------
            if (!File.Exists(sentinelPath))
                throw new FileNotFoundException("pending_update.json missing — nothing to install", sentinelPath);

            var json = File.ReadAllText(sentinelPath);
            var sentinel = JsonDocument.Parse(json).RootElement;
            var zipPath = sentinel.GetProperty("zipPath").GetString()
                ?? throw new InvalidOperationException("zipPath missing in sentinel");
            var targetVersion = sentinel.TryGetProperty("version", out var v) ? v.GetString() ?? "?" : "?";

            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"Staged ZIP not found: {zipPath}");

            Log(logPath, $"Installing v{targetVersion} from {zipPath}");
            SetTitle($"AccessControlPro Updater — installing v{targetVersion}");

            // -------- Step 3: Back up the current DLLs (rollback safety net) --------
            SetStatus("Preparing rollback safety net...");
            var rollbackDir = Path.Combine(installFolder, "_rollback");
            Directory.CreateDirectory(rollbackDir);
            foreach (var f in Directory.GetFiles(rollbackDir)) { try { File.Delete(f); } catch { } }

            foreach (var file in Directory.GetFiles(installFolder, "*.dll"))
            {
                try { File.Copy(file, Path.Combine(rollbackDir, Path.GetFileName(file)), overwrite: true); }
                catch (Exception ex) { Log(logPath, $"  rollback copy skipped for {Path.GetFileName(file)}: {ex.Message}"); }
            }
            var mainExe = Path.Combine(installFolder, "AccessControlPro.WPF.exe");
            if (File.Exists(mainExe))
            {
                try { File.Copy(mainExe, Path.Combine(rollbackDir, "AccessControlPro.WPF.exe"), overwrite: true); }
                catch (Exception ex) { Log(logPath, $"  rollback copy of main exe skipped: {ex.Message}"); }
            }

            // -------- Step 4: Extract the ZIP, skipping preserved files --------
            SetStatus("Installing new version...");
            int extracted = 0, skipped = 0, deferred = 0;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                int entryIndex = 0;
                int totalEntries = archive.Entries.Count;
                foreach (var entry in archive.Entries)
                {
                    entryIndex++;
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var entryFileName = Path.GetFileName(relativePath);
                    var topFolder = relativePath.Split(Path.DirectorySeparatorChar).FirstOrDefault();

                    if (PreservedNames.Contains(entryFileName))
                    {
                        skipped++;
                        Log(logPath, $"  preserved file (skipped): {relativePath}");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(topFolder) && PreservedFolders.Contains(topFolder))
                    {
                        skipped++;
                        Log(logPath, $"  preserved folder (skipped): {relativePath}");
                        continue;
                    }

                    var destPath = Path.Combine(installFolder, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    // Files might be briefly locked (antivirus scan, a slow-closing sibling app,
                    // etc.) — retry a few times with a growing pause.
                    bool written = false;
                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        try
                        {
                            entry.ExtractToFile(destPath, overwrite: true);
                            extracted++;
                            written = true;
                            break;
                        }
                        catch (IOException) when (attempt < 5) { Thread.Sleep(700); }
                    }

                    // Still locked after all retries — DON'T abort the whole update. Extract to a
                    // temp name and schedule the swap for the next reboot (MoveFileEx). The update
                    // then completes on restart instead of failing + rolling everything back.
                    if (!written)
                    {
                        try
                        {
                            var pending = destPath + ".pending_update";
                            entry.ExtractToFile(pending, overwrite: true);
                            MoveFileEx(pending, destPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_DELAY_UNTIL_REBOOT);
                            deferred++;
                            Log(logPath, $"  deferred to reboot (locked): {relativePath}");
                        }
                        catch (Exception ex) { Log(logPath, $"  FAILED to write {relativePath}: {ex.Message}"); }
                    }

                    if (entryIndex % 25 == 0)
                        SetStatus($"Installing... {entryIndex}/{totalEntries}");
                }
            }
            Log(logPath, $"Extraction complete: {extracted} files written, {skipped} preserved, {deferred} deferred to reboot.");

            // -------- Step 5: Cleanup --------
            try { File.Delete(sentinelPath); } catch { }
            try { File.Delete(zipPath); } catch { }
            var stagingDir = Path.Combine(installFolder, "update_staging");
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, recursive: true); } catch { }
            }

            // If any file was locked and deferred, the swap finishes on reboot — tell the operator.
            if (deferred > 0)
            {
                ShowInfo($"Update to v{targetVersion} installed.\n\n" +
                         $"{deferred} file(s) were in use and will finish updating the next time you RESTART the computer.\n\n" +
                         "Please restart the PC when convenient.");
            }

            // -------- Step 6: Mark post-update so the main app shows the What's New dialog --------
            var postUpdateMarker = Path.Combine(installFolder, ".post_update");
            try { File.WriteAllText(postUpdateMarker, targetVersion); } catch { }

            // -------- Step 7: Relaunch the main app --------
            SetStatus("Restarting AccessControlPro...");
            var newMainExe = Path.Combine(installFolder, "AccessControlPro.WPF.exe");
            if (File.Exists(newMainExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = newMainExe,
                    WorkingDirectory = installFolder,
                    UseShellExecute = true
                });
                Log(logPath, "Main app relaunched. Updater exiting.");
            }
            else
            {
                Log(logPath, $"WARN: main exe not found at {newMainExe}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log(logPath, $"FATAL: {ex}");
            ShowError(
                $"The update could not be installed:\n\n{ex.Message}\n\n" +
                $"A rollback copy of the previous version is at:\n{Path.Combine(installFolder, "_rollback")}\n\n" +
                $"Please contact support.");
            return 1;
        }
    }

    // Close every AccessControlPro process (main app + Admin + POS + any zombie/second instance)
    // so no shared framework DLL stays locked during the file swap. Graceful close first, then kill.
    private static void KillRelatedProcesses(string logPath)
    {
        var names = new[] { "AccessControlPro.WPF", "AccessControlPro.Admin", "AccessControlPro.POS" };
        foreach (var name in names)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); } catch { continue; }
            foreach (var p in procs)
            {
                try
                {
                    Log(logPath, $"Closing {name} (pid {p.Id})...");
                    try { p.CloseMainWindow(); } catch { }
                    if (!p.WaitForExit(4000))
                    {
                        p.Kill();
                        p.WaitForExit(4000);
                    }
                }
                catch (Exception ex) { Log(logPath, $"  close {name} warning: {ex.Message}"); }
            }
        }
        // Give the OS a moment to release file handles after the processes die.
        Thread.Sleep(800);
    }

    private static void WaitForProcessExit(int pid, TimeSpan timeout, string logPath)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
            {
                Log(logPath, $"WARN: main app did not exit within {timeout.TotalSeconds}s. Forcing kill.");
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
            }
        }
        catch (ArgumentException) { /* already exited — perfect */ }
        catch (Exception ex) { Log(logPath, $"WaitForExit warning: {ex.Message}"); }
    }

    private static void Log(string path, string msg)
    {
        try { File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n"); }
        catch { }
    }

    private static void SetStatus(string text)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
            SetTitle($"AccessControlPro Updater — {text}");
        }
        catch { }
    }

    private static void SetTitle(string title)
    {
        try { Console.Title = title; } catch { }
    }

    private static void ShowError(string msg)
    {
        try { MessageBoxW(IntPtr.Zero, msg, "AccessControlPro Updater", MB_ICONERROR | MB_OK); }
        catch { Console.Error.WriteLine(msg); }
    }

    private static void ShowInfo(string msg)
    {
        try { MessageBoxW(IntPtr.Zero, msg, "AccessControlPro Updater", MB_ICONINFORMATION | MB_OK); }
        catch { Console.WriteLine(msg); }
    }
}
