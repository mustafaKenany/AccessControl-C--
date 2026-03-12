namespace AccessControlPro.Infrastructure.Security;

/// <summary>
/// Enhanced audit logging for security events.
/// Records: failed auth attempts, unauthorized access, sensitive operations, etc.
/// Ensures compliance audit trails are complete.
/// </summary>
public interface IEnhancedAuditLogger
{
    /// <summary>Log failed authentication attempt</summary>
    Task LogFailedAuthenticationAsync(string username, string reason, string ipAddress = "");

    /// <summary>Log successful authentication</summary>
    Task LogSuccessfulAuthenticationAsync(string username, string ipAddress = "");

    /// <summary>Log unauthorized access attempt</summary>
    Task LogUnauthorizedAccessAsync(string username, string operation, string reason, string? ipAddress = null);

    /// <summary>Log sensitive data access</summary>
    Task LogSensitiveDataAccessAsync(string username, string dataType, int recordId, string action);

    /// <summary>Log system security event</summary>
    Task LogSecurityEventAsync(string eventType, string description, string severity = "INFO");

    /// <summary>Get audit log for compliance report</summary>
    Task<IEnumerable<AuditEventLog>> GetAuditLogsAsync(DateTime from, DateTime to, string? eventType = null);
}

public class AuditEventLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO"; // INFO, WARNING, ERROR, CRITICAL
    public bool Success { get; set; }
}

public class EnhancedAuditLogger : IEnhancedAuditLogger
{
    private readonly ICurrentUser _currentUser;
    private static readonly List<AuditEventLog> AuditLogs = new();
    private static readonly object _logLock = new();

    public EnhancedAuditLogger(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task LogFailedAuthenticationAsync(string username, string reason, string ipAddress = "")
    {
        var log = new AuditEventLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "AUTHENTICATION_FAILED",
            Username = username,
            IpAddress = ipAddress,
            Description = $"Failed login attempt. Reason: {reason}",
            Severity = "WARNING",
            Success = false
        };

        lock (_logLock) { AuditLogs.Add(log); }
        await LogToSystemAsync(log);
    }

    public async Task LogSuccessfulAuthenticationAsync(string username, string ipAddress = "")
    {
        var log = new AuditEventLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "AUTHENTICATION_SUCCESS",
            Username = username,
            IpAddress = ipAddress,
            Description = $"User '{username}' logged in successfully",
            Severity = "INFO",
            Success = true
        };

        lock (_logLock) { AuditLogs.Add(log); }
        await LogToSystemAsync(log);
    }

    public async Task LogUnauthorizedAccessAsync(string username, string operation, string reason, string? ipAddress = null)
    {
        var log = new AuditEventLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "UNAUTHORIZED_ACCESS",
            Username = username,
            IpAddress = ipAddress ?? "",
            Description = $"Unauthorized attempt to {operation}. Reason: {reason}",
            Severity = "ERROR",
            Success = false
        };

        lock (_logLock) { AuditLogs.Add(log); }
        await LogToSystemAsync(log);
    }

    public async Task LogSensitiveDataAccessAsync(string username, string dataType, int recordId, string action)
    {
        var log = new AuditEventLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = "SENSITIVE_DATA_ACCESS",
            Username = username,
            Description = $"{action} {dataType} record #{recordId}",
            Severity = "INFO",
            Success = true
        };

        lock (_logLock) { AuditLogs.Add(log); }
        await LogToSystemAsync(log);
    }

    public async Task LogSecurityEventAsync(string eventType, string description, string severity = "INFO")
    {
        var log = new AuditEventLog
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Username = _currentUser.Username,
            Description = description,
            Severity = severity,
            Success = severity == "INFO"
        };

        lock (_logLock) { AuditLogs.Add(log); }
        await LogToSystemAsync(log);
    }

    public async Task<IEnumerable<AuditEventLog>> GetAuditLogsAsync(DateTime from, DateTime to, string? eventType = null)
    {
        await Task.Delay(10); // Simulate DB call

        lock (_logLock)
        {
            var query = AuditLogs
                .Where(x => x.Timestamp >= from && x.Timestamp <= to);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(x => x.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));

            return query.OrderByDescending(x => x.Timestamp).ToList();
        }
    }

    private async Task LogToSystemAsync(AuditEventLog log)
    {
        // ✓ LOG TO FILE as well (not just DB)
        var logMessage = $"[{log.Severity}] {log.Timestamp:yyyy-MM-dd HH:mm:ss} - " +
                        $"{log.EventType} - User: {log.Username ?? "SYSTEM"} - {log.Description}";

        System.Diagnostics.Debug.WriteLine(logMessage);

        await Task.Run(() =>
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);

                var logFile = Path.Combine(logDir, $"security_{DateTime.Today:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            }
        });
    }
}

/// <summary>
/// Security audit report for compliance
/// </summary>
public class SecurityAuditReport
{
    public DateTime ReportDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Authentication metrics
    public int SuccessfulLogins { get; set; }
    public int FailedLoginAttempts { get; set; }
    public int AccountsLocked { get; set; }
    public List<string> TopFailedUsers { get; set; } = new();

    // Authorization metrics
    public int UnauthorizedAccessAttempts { get; set; }
    public Dictionary<string, int> UnauthorizedByOperation { get; set; } = new();

    // Data access metrics
    public int SensitiveDataAccessCount { get; set; }
    public List<string> UsersAccessingSensitiveData { get; set; } = new();

    // Security events
    public int PasswordChangesRequired { get; set; }
    public int SecurityEventsLogged { get; set; }
    public List<string> CriticalEvents { get; set; } = new();
}
