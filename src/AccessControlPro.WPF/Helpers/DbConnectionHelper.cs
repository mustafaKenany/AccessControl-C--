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
    public static bool IsConnectionError(Exception ex)
    {
        if (ex is SqlException sqlEx)
        {
            // Network-related or instance-specific errors
            return sqlEx.Number is 2 or 53 or -2 or 10054 or 10060 or 10061
                or 233 or 258 or 4060 or 18456
                || sqlEx.Class >= 20; // severity >= 20 = connection-level
        }

        if (ex.InnerException is SqlException innerSql)
            return IsConnectionError(innerSql);

        // EF Core wraps in InvalidOperationException sometimes
        return ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
            && ex.Message.Contains("server", StringComparison.OrdinalIgnoreCase);
    }
}
