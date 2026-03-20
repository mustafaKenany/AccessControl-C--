using System;
using System.IO;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Simple static activity logger. Appends navigation and action entries to Logs/activity.log.
/// Thread-safe. File is never cleared — it accumulates across sessions.
/// </summary>
public static class ActivityLogger
{
    private static readonly object _lock = new();
    private static readonly string _logPath;

    static ActivityLogger()
    {
        var logsDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);
        _logPath = Path.Combine(logsDir, "activity.log");
    }

    /// <summary>Log when a user navigates to a section.</summary>
    public static void LogNavigation(string section)
        => Write($"[NAV] {section}: entered");

    /// <summary>Log an action performed in a section.</summary>
    public static void LogAction(string section, string action, string details = "")
    {
        var entry = string.IsNullOrWhiteSpace(details)
            ? $"[ACTION] {section}: {action}"
            : $"[ACTION] {section}: {action} - {details}";
        Write(entry);
    }

    /// <summary>Log an error that occurred in a section.</summary>
    public static void LogError(string section, string action, string error)
        => Write($"[ERROR] {section}: {action} - {error}");

    private static void Write(string message)
    {
        lock (_lock)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Never crash the app due to logging failure
            }
        }
    }
}
