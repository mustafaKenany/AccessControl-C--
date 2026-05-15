using System.IO;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

/// <summary>
/// Centralised sink for database exceptions. Services that touch SQL Server can call
/// <see cref="Capture"/> in their catch blocks to leave a breadcrumb behind before
/// they rethrow. The bundle uploader picks up <c>db_errors_log.txt</c> automatically.
///
/// Goal isn't to swallow exceptions — it's to make sure that when "something failed"
/// becomes a customer report two days later, there's a timestamped record of the
/// operation + the SQL error code + the message, instead of nothing.
/// </summary>
public static class DbErrorLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "db_errors_log.txt");

    /// <summary>
    /// Log a DB exception with context. Pass the operation name (e.g. "EmployeeRepo.Add",
    /// "AccessCardRepo.GetByNumber") so we can grep later. Returns the exception so callers
    /// can chain: <c>throw DbErrorLog.Capture("op", ex);</c>
    /// </summary>
    public static Exception Capture(string operation, Exception ex)
    {
        try
        {
            var details = ex switch
            {
                SqlException sql => $"SqlException #{sql.Number} state={sql.State} class={sql.Class} line={sql.LineNumber}: {sql.Message}",
                _ => $"{ex.GetType().Name}: {ex.Message}"
            };

            var sb = new System.Text.StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ");
            sb.Append(operation).Append(" — ").AppendLine(details);
            if (ex.InnerException != null)
                sb.Append("    Inner: ").Append(ex.InnerException.GetType().Name).Append(": ").AppendLine(ex.InnerException.Message);
            RollingLogFile.Append(LogPath, sb.ToString());
        }
        catch
        {
            // Logging must never throw. If we can't write the log file (disk full,
            // permissions), swallow — the caller will rethrow the original anyway.
        }
        return ex;
    }
}
