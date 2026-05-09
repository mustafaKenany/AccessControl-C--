using System;
using System.Collections.Generic;
using System.IO;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Simple static activity logger. Appends navigation and action entries to Logs/activity.log.
/// Thread-safe. File is never cleared — it accumulates across sessions.
///
/// Also maintains an in-memory ring buffer of the last N entries so crash handlers
/// can include "what the user was doing in the seconds before the crash" without
/// having to re-read the file (which may be locked / huge / on a slow disk).
/// </summary>
public static class ActivityLogger
{
    private const int BreadcrumbCapacity = 30;
    private static readonly object _lock = new();
    private static readonly string _logPath;
    private static readonly LinkedList<string> _breadcrumbs = new();

    static ActivityLogger()
    {
        var logsDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);
        _logPath = Path.Combine(logsDir, "activity.log");
    }

    /// <summary>Snapshot of recent user activity, oldest-first. For crash diagnostics.</summary>
    public static IReadOnlyList<string> GetRecentBreadcrumbs()
    {
        lock (_lock)
        {
            return new List<string>(_breadcrumbs);
        }
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
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            // Keep an in-memory tail so crash handlers always have recent activity
            // available even if the log file is locked or unreadable.
            _breadcrumbs.AddLast(line);
            while (_breadcrumbs.Count > BreadcrumbCapacity)
                _breadcrumbs.RemoveFirst();

            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Never crash the app due to logging failure
            }
        }
    }
}
