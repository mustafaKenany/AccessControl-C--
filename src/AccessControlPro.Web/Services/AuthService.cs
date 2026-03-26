using System.Collections.Concurrent;
using AccessControlPro.Web.Data;
using Npgsql;

namespace AccessControlPro.Web.Services;

public class WebAuthService
{
    private readonly DbHelper _db;
    private readonly GymDbHelper _gymDb;

    // Rate limiting: track failed login attempts per identifier
    private static readonly ConcurrentDictionary<string, (int attempts, DateTime lastAttempt)> _loginAttempts = new();
    private const int MaxAttempts = 5;
    private const int BlockMinutes = 15;

    public WebAuthService(DbHelper db, GymDbHelper gymDb)
    {
        _db = db;
        _gymDb = gymDb;
    }

    private bool IsBlocked(string identifier)
    {
        if (_loginAttempts.TryGetValue(identifier, out var entry))
        {
            if (entry.attempts >= MaxAttempts && (DateTime.UtcNow - entry.lastAttempt).TotalMinutes < BlockMinutes)
                return true;
            if ((DateTime.UtcNow - entry.lastAttempt).TotalMinutes >= BlockMinutes)
                _loginAttempts.TryRemove(identifier, out _);
        }
        return false;
    }

    private void RecordFailedAttempt(string identifier)
    {
        _loginAttempts.AddOrUpdate(
            identifier,
            (1, DateTime.UtcNow),
            (_, existing) => (existing.attempts + 1, DateTime.UtcNow));
    }

    private void ClearAttempts(string identifier)
    {
        _loginAttempts.TryRemove(identifier, out _);
    }

    public async Task<AuthResult> OwnerLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failed("Username and password are required.");

        var identifier = $"owner:{username.Trim().ToLower()}";
        if (IsBlocked(identifier))
            return AuthResult.Failed("Too many failed attempts. Please try again in 15 minutes.");

        // Try to find which gym database has this user
        string gymDatabase = "";
        int gymId = 0;
        NpgsqlConnection? conn = null;

        try
        {
            var found = await _gymDb.FindUserDatabaseAsync(username);
            if (found.HasValue)
            {
                gymDatabase = found.Value.dbName ?? "";
                gymId = found.Value.gymId;
                conn = await _gymDb.GetGymConnectionAsync(gymDatabase);
            }
            else
            {
                // No gym found for this user — login fails
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Invalid username or password.");
            }
        }
        catch
        {
            return AuthResult.Failed("Unable to connect. Please try again later.");
        }

        // Check if gym is active and exists in master
        if (gymDatabase != "gymcloud")
        {
            try
            {
                using var masterConn = await _gymDb.GetMasterConnectionAsync();

                using var existsCmd = new NpgsqlCommand(
                    @"SELECT ""IsActive"" FROM ""Gyms"" WHERE ""DatabaseName"" = @db", masterConn);
                existsCmd.Parameters.AddWithValue("db", gymDatabase);
                var isActiveResult = await existsCmd.ExecuteScalarAsync();

                if (isActiveResult == null)
                {
                    if (conn != null) await conn.DisposeAsync();
                    return AuthResult.Failed("Your gym account has been removed. Contact support.");
                }
                if (isActiveResult is bool active && !active)
                {
                    if (conn != null) await conn.DisposeAsync();
                    return AuthResult.Failed("Your gym account has been deactivated. Contact support.");
                }
            }
            catch { /* If master check fails, allow login attempt to proceed */ }
        }

        try
        {
            using var cmd = new NpgsqlCommand(
                @"SELECT ""Id"", ""Username"", ""PasswordHash"", ""DisplayName"", ""Role""
                  FROM ""Users"" WHERE ""Username"" = @u AND ""IsActive"" = TRUE", conn);
            cmd.Parameters.AddWithValue("u", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Invalid username or password.");
            }

            var hash = reader.GetString(2);
            try
            {
                if (!BCrypt.Net.BCrypt.Verify(password, hash))
                {
                    RecordFailedAttempt(identifier);
                    return AuthResult.Failed("Invalid username or password.");
                }
            }
            catch
            {
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Invalid username or password.");
            }

            ClearAttempts(identifier);
            var result = AuthResult.Success(
                reader.IsDBNull(3) ? reader.GetString(1) : reader.GetString(3),
                reader.IsDBNull(4) ? "Owner" : reader.GetString(4),
                reader.GetInt32(0));
            result.GymDatabase = gymDatabase;
            result.GymId = gymId;
            return result;
        }
        finally
        {
            if (conn != null)
                await conn.DisposeAsync();
        }
    }

    public async Task<AuthResult> PlayerLoginAsync(string phone, string cardLast4)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(cardLast4))
            return AuthResult.Failed("Phone and card digits are required.");

        var identifier = $"player:{phone.Trim()}";
        if (IsBlocked(identifier))
            return AuthResult.Failed("Too many failed attempts. Please try again in 15 minutes.");

        // Try to find which gym database has this player
        string gymDatabase = "";
        int gymId = 0;
        NpgsqlConnection? conn = null;

        try
        {
            var found = await _gymDb.FindPlayerDatabaseAsync(phone);
            if (found.HasValue)
            {
                gymDatabase = found.Value.dbName ?? "";
                gymId = found.Value.gymId;
                conn = await _gymDb.GetGymConnectionAsync(gymDatabase);
            }
            else
            {
                // No gym found for this player — login fails
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Player not found. Check your phone number.");
            }
        }
        catch
        {
            return AuthResult.Failed("Unable to connect. Please try again later.");
        }

        // Check if gym is active and exists in master
        if (gymDatabase != "gymcloud")
        {
            try
            {
                using var masterConn = await _gymDb.GetMasterConnectionAsync();

                using var existsCmd = new NpgsqlCommand(
                    @"SELECT ""IsActive"" FROM ""Gyms"" WHERE ""DatabaseName"" = @db", masterConn);
                existsCmd.Parameters.AddWithValue("db", gymDatabase);
                var isActiveResult = await existsCmd.ExecuteScalarAsync();

                if (isActiveResult == null)
                {
                    if (conn != null) await conn.DisposeAsync();
                    return AuthResult.Failed("Your gym account has been removed. Contact support.");
                }
                if (isActiveResult is bool active && !active)
                {
                    if (conn != null) await conn.DisposeAsync();
                    return AuthResult.Failed("Your gym account has been deactivated. Contact support.");
                }
            }
            catch { /* If master check fails, allow login attempt to proceed */ }
        }

        try
        {
            using var cmd = new NpgsqlCommand(
                @"SELECT ""Id"", ""FullNameEn"", ""FullNameAr"", ""CardNo""
                  FROM ""Players"" WHERE ""Phone"" = @p AND ""IsDeleted"" = FALSE", conn);
            cmd.Parameters.AddWithValue("p", phone);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Player not found. Check your phone number.");
            }

            var cardNo = reader.IsDBNull(3) ? "" : reader.GetString(3);
            if (string.IsNullOrEmpty(cardNo) || !cardNo.EndsWith(cardLast4))
            {
                RecordFailedAttempt(identifier);
                return AuthResult.Failed("Invalid card digits.");
            }

            var nameEn = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var nameAr = reader.IsDBNull(2) ? "" : reader.GetString(2);

            ClearAttempts(identifier);
            var result = AuthResult.Success(
                !string.IsNullOrEmpty(nameEn) ? nameEn : nameAr,
                "Player",
                reader.GetInt32(0));
            result.GymDatabase = gymDatabase;
            result.GymId = gymId;
            return result;
        }
        finally
        {
            if (conn != null)
                await conn.DisposeAsync();
        }
    }
}

public class AuthResult
{
    public bool IsAuthenticated { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public int UserId { get; set; }
    public string Error { get; set; } = "";
    public string GymDatabase { get; set; } = "";
    public int GymId { get; set; }

    public static AuthResult Success(string displayName, string role, int userId)
        => new() { IsAuthenticated = true, DisplayName = displayName, Role = role, UserId = userId };

    public static AuthResult Failed(string error)
        => new() { IsAuthenticated = false, Error = error };
}
