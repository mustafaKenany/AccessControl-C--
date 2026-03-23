using Npgsql;

namespace AccessControlPro.Web.Data;

/// <summary>
/// Manages connections to gym-specific databases.
/// Master DB (gymcloud) stores the Gyms table.
/// Each gym has its own database (gymcloud_gymX).
/// </summary>
public class GymDbHelper
{
    private readonly string _masterConnectionString;

    public GymDbHelper(string masterConnectionString)
    {
        _masterConnectionString = masterConnectionString;
    }

    /// <summary>Get connection to master database (Gyms table, Super Admin)</summary>
    public async Task<NpgsqlConnection> GetMasterConnectionAsync()
    {
        var conn = new NpgsqlConnection(_masterConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Get connection to a specific gym's database</summary>
    public async Task<NpgsqlConnection> GetGymConnectionAsync(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_masterConnectionString);
        builder.Database = databaseName;
        var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Get gym database name by API key</summary>
    public async Task<string?> GetDatabaseByApiKeyAsync(string apiKey)
    {
        using var conn = await GetMasterConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT ""DatabaseName"" FROM ""Gyms"" WHERE ""ApiKey"" = @key AND ""IsActive"" = TRUE", conn);
        cmd.Parameters.AddWithValue("key", apiKey);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    /// <summary>Find which gym database contains a user with this username</summary>
    public async Task<(string? dbName, int gymId)?> FindUserDatabaseAsync(string username)
    {
        using var masterConn = await GetMasterConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT ""Id"", ""DatabaseName"" FROM ""Gyms"" WHERE ""IsActive"" = TRUE", masterConn);
        using var reader = await cmd.ExecuteReaderAsync();

        var gyms = new List<(int id, string db)>();
        while (await reader.ReadAsync())
            gyms.Add((reader.GetInt32(0), reader.GetString(1)));
        await reader.CloseAsync();

        foreach (var (gymId, dbName) in gyms)
        {
            try
            {
                using var gymConn = await GetGymConnectionAsync(dbName);
                using var userCmd = new NpgsqlCommand(
                    @"SELECT COUNT(*) FROM ""Users"" WHERE ""Username"" = @u AND ""IsActive"" = TRUE", gymConn);
                userCmd.Parameters.AddWithValue("u", username);
                var count = Convert.ToInt32(await userCmd.ExecuteScalarAsync());
                if (count > 0)
                    return (dbName, gymId);
            }
            catch { continue; }
        }
        return null;
    }

    /// <summary>Find which gym database contains a player with this phone</summary>
    public async Task<(string? dbName, int gymId)?> FindPlayerDatabaseAsync(string phone)
    {
        using var masterConn = await GetMasterConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT ""Id"", ""DatabaseName"" FROM ""Gyms"" WHERE ""IsActive"" = TRUE", masterConn);
        using var reader = await cmd.ExecuteReaderAsync();

        var gyms = new List<(int id, string db)>();
        while (await reader.ReadAsync())
            gyms.Add((reader.GetInt32(0), reader.GetString(1)));
        await reader.CloseAsync();

        foreach (var (gymId, dbName) in gyms)
        {
            try
            {
                using var gymConn = await GetGymConnectionAsync(dbName);
                using var playerCmd = new NpgsqlCommand(
                    @"SELECT COUNT(*) FROM ""Players"" WHERE ""Phone"" = @p AND ""IsDeleted"" = FALSE", gymConn);
                playerCmd.Parameters.AddWithValue("p", phone);
                var count = Convert.ToInt32(await playerCmd.ExecuteScalarAsync());
                if (count > 0)
                    return (dbName, gymId);
            }
            catch { continue; }
        }
        return null;
    }

    /// <summary>Create a new database for a gym and initialize tables</summary>
    public async Task CreateGymDatabaseAsync(string databaseName)
    {
        // Create database
        using var masterConn = await GetMasterConnectionAsync();

        // Can't use parameters for CREATE DATABASE
        using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", masterConn);
        await createCmd.ExecuteNonQueryAsync();

        // Initialize tables in new database
        using var gymConn = await GetGymConnectionAsync(databaseName);
        var builder = new NpgsqlConnectionStringBuilder(_masterConnectionString);
        builder.Database = databaseName;
        var dbHelper = new DbHelper(builder.ConnectionString);
        await dbHelper.InitializeDatabaseAsync();
    }
}
