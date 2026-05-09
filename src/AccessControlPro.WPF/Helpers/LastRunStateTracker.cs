using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Tracks why the previous app session ended. Writes a state file at startup ("running")
/// and updates it on clean exit ("clean_exit") or Windows session-end ("system_shutdown").
/// On next startup, reading the file tells us if the previous run died unexpectedly.
///
/// Categories of previous-session end:
/// - clean_exit:       OnExit ran successfully (user closed app normally)
/// - system_shutdown:  Windows logoff/shutdown intercepted via SessionEnding
/// - killed_or_crashed: state still says "running" — process died without OnExit
///                     (Task Manager kill, power loss, native crash, BSOD, etc.)
/// - first_run:        no state file existed (fresh install or file deleted)
/// </summary>
public static class LastRunStateTracker
{
    private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "last_run_state.json");

    private record State(int Pid, string Status, string StartedAt, string? EndedAt, string? ShutdownReason);

    /// <summary>
    /// Read previous session state, return a human-readable summary line, then overwrite
    /// the state with this session's startup info. Call this once early in OnStartup.
    /// The returned string is suitable for direct logging.
    /// </summary>
    public static string ReadPreviousAndRecordStartup()
    {
        var summary = ReadPreviousState();
        WriteRunningState();
        return summary;
    }

    public static void RecordCleanExit()
    {
        try
        {
            var existing = TryReadState();
            var updated = new State(
                Pid: existing?.Pid ?? Process.GetCurrentProcess().Id,
                Status: "clean_exit",
                StartedAt: existing?.StartedAt ?? DateTime.Now.ToString("O"),
                EndedAt: DateTime.Now.ToString("O"),
                ShutdownReason: null);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(updated));
        }
        catch { /* logging this would be circular; just swallow */ }
    }

    public static void RecordSystemShutdown(string reason)
    {
        try
        {
            var existing = TryReadState();
            var updated = new State(
                Pid: existing?.Pid ?? Process.GetCurrentProcess().Id,
                Status: "system_shutdown",
                StartedAt: existing?.StartedAt ?? DateTime.Now.ToString("O"),
                EndedAt: DateTime.Now.ToString("O"),
                ShutdownReason: reason);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(updated));
        }
        catch { }
    }

    private static string ReadPreviousState()
    {
        var prev = TryReadState();
        if (prev == null)
            return "Previous session: first_run (no state file)";

        return prev.Status switch
        {
            "clean_exit" =>
                $"Previous session: clean_exit (started {prev.StartedAt}, ended {prev.EndedAt})",
            "system_shutdown" =>
                $"Previous session: system_shutdown — {prev.ShutdownReason ?? "unknown reason"} " +
                $"(started {prev.StartedAt}, ended {prev.EndedAt})",
            "running" =>
                $"Previous session: KILLED OR CRASHED — process {prev.Pid} stopped without clean shutdown " +
                $"(was running since {prev.StartedAt}). Check crash_log.txt for the same window. " +
                $"If crash_log has no matching entry, the process was force-killed (Task Manager / power loss / BSOD).",
            _ =>
                $"Previous session: unknown status '{prev.Status}'"
        };
    }

    private static State? TryReadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<State>(json);
        }
        catch { return null; }
    }

    private static void WriteRunningState()
    {
        try
        {
            var state = new State(
                Pid: Process.GetCurrentProcess().Id,
                Status: "running",
                StartedAt: DateTime.Now.ToString("O"),
                EndedAt: null,
                ShutdownReason: null);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
        }
        catch { /* if we can't write the marker, we can't track — accept and continue */ }
    }
}
