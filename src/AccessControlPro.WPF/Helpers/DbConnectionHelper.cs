using Microsoft.Data.SqlClient;

namespace AccessControlPro.WPF.Helpers;

public static class DbConnectionHelper
{
    /// <summary>
    /// Tests if a SQL Server connection can be established.
    /// Returns null on success, or the error message on failure.
    /// </summary>
    public static string? TestConnection(string connectionString, int timeoutSeconds = 5)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = timeoutSeconds
            };

            using var conn = new SqlConnection(builder.ConnectionString);
            conn.Open();
            return null; // success
        }
        catch (SqlException ex)
        {
            return ex.Message;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Builds a connection string from individual parts.
    /// </summary>
    public static string BuildConnectionString(string server, string database, string userId, string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = userId,
            Password = password,
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Checks if a SqlException indicates a connection/network problem.
    /// </summary>
    public static bool IsConnectionError(Exception? ex)
    {
        // Walk the WHOLE inner-exception chain — EF wraps the real failure several levels deep
        // (e.g. RetryLimitExceededException -> InvalidOperationException pool-timeout -> SqlException).
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqlException sqlEx
                && (sqlEx.Number is 2 or 53 or -2 or 10054 or 10060 or 10061
                    or 233 or 258 or 4060 or 18456
                    || sqlEx.Class >= 20)) // severity >= 20 = connection-level
                return true;

            // EF Core's retrying execution strategy gives up after N transient failures — it only
            // ever retries transient (connection/timeout) errors, so this always means the DB was
            // unreachable (power gym, 2026-07). Match by name to avoid an EF Core reference here.
            if (e.GetType().Name == "RetryLimitExceededException")
                return true;

            var m = e.Message;
            // Connection-pool exhaustion / timeout obtaining a connection.
            if (m.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                && m.Contains("pool", StringComparison.OrdinalIgnoreCase))
                return true;
            // EF Core sometimes wraps in a plain InvalidOperationException.
            if (m.Contains("connection", StringComparison.OrdinalIgnoreCase)
                && m.Contains("server", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
