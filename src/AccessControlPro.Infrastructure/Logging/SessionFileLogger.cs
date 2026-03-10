using System;
using System.IO;
using System.Threading.Tasks;

namespace AccessControlPro.Infrastructure.Logging;

/// <summary>
/// Session-based file logger. Logs operations to a temporary file for the current session.
/// File is cleared on app exit and recreated on app startup.
/// </summary>
public class SessionFileLogger : ISessionLogger, IDisposable
{
    private readonly string _sessionLogPath;
    private readonly object _lockObject = new();

    public SessionFileLogger()
    {
        // Create logs folder in app directory if it doesn't exist
        var appDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? AppDomain.CurrentDomain.BaseDirectory;
        var logsDir = Path.Combine(appDir, "Logs");

        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);

        _sessionLogPath = Path.Combine(logsDir, "session.log");

        // Clear any previous session log
        if (File.Exists(_sessionLogPath))
            File.Delete(_sessionLogPath);

        LogInternal($"[SESSION START] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
    }

    /// <summary>
    /// Log an operation with EN/AR descriptions
    /// </summary>
    public async Task LogOperationAsync(string operationType, string entityType, int? entityId,
        string descriptionEn, string descriptionAr, string? performedBy = null)
    {
        var message = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {operationType} | {entityType} (ID:{entityId ?? 0}) | BY:{performedBy ?? "Unknown"} | EN: {descriptionEn} | AR: {descriptionAr}";
        await Task.Run(() => LogInternal(message));
    }

    /// <summary>
    /// Log a general message
    /// </summary>
    public async Task LogAsync(string message)
    {
        var formatted = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        await Task.Run(() => LogInternal(formatted));
    }

    /// <summary>
    /// Log an error
    /// </summary>
    public async Task LogErrorAsync(string operationType, string entityType, int? entityId,
        string errorMessage, string? performedBy = null)
    {
        var message = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] ERROR | {operationType} | {entityType} (ID:{entityId ?? 0}) | BY:{performedBy ?? "Unknown"} | {errorMessage}";
        await Task.Run(() => LogInternal(message));
    }

    private void LogInternal(string message)
    {
        lock (_lockObject)
        {
            try
            {
                File.AppendAllText(_sessionLogPath, message + Environment.NewLine);
            }
            catch
            {
                // Silently fail if logging fails (don't crash the app)
            }
        }
    }

    /// <summary>
    /// Clear session logs (called on app exit)
    /// </summary>
    public void ClearLogs()
    {
        lock (_lockObject)
        {
            try
            {
                if (File.Exists(_sessionLogPath))
                {
                    LogInternal($"[SESSION END] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                    File.Delete(_sessionLogPath);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }

    public void Dispose()
    {
        ClearLogs();
        GC.SuppressFinalize(this);
    }

    ~SessionFileLogger()
    {
        ClearLogs();
    }
}
