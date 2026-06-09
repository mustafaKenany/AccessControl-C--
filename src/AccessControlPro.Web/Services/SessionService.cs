using System.Security.Cryptography;
using AccessControlPro.Web.Data;
using Npgsql;

namespace AccessControlPro.Web.Services;

/// <summary>
/// Server-side session store. Replaces the plaintext `.active_session` file and the
/// JS-set cookie scheme. The browser only sees an opaque 256-bit random token in an
/// HttpOnly cookie; all session state lives in the Sessions table where it can be
/// atomically revoked (e.g. on logout, or by an admin) and can't be forged by
/// editing a file or JavaScript variable.
/// </summary>
public class SessionService
{
    private readonly DbHelper _db;
    public const string CookieName = "ac_session";
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);
    // Players use the installed PWA daily and shouldn't re-enter credentials each time —
    // give their session a long life so the app opens straight to their dashboard.
    public static readonly TimeSpan PlayerLifetime = TimeSpan.FromDays(90);

    public SessionService(DbHelper db)
    {
        _db = db;
    }

    /// <summary>Generates a URL-safe random token (256 bits of entropy).</summary>
    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public async Task<string> CreateAsync(string role, string displayName, int userId,
        string gymDatabase, int gymId, string? ip, string? userAgent, TimeSpan? lifetime = null)
    {
        var token = NewToken();
        using var conn = await _db.GetConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO ""Sessions""
              (""Token"", ""Role"", ""DisplayName"", ""UserId"", ""GymDatabase"", ""GymId"",
               ""ExpiresAt"", ""LastIp"", ""LastUserAgent"")
              VALUES (@token, @role, @name, @uid, @gdb, @gid, @exp, @ip, @ua)", conn);
        cmd.Parameters.AddWithValue("token", token);
        cmd.Parameters.AddWithValue("role", role ?? "");
        cmd.Parameters.AddWithValue("name", displayName ?? "");
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("gdb", gymDatabase ?? "");
        cmd.Parameters.AddWithValue("gid", gymId);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.Add(lifetime ?? DefaultLifetime));
        cmd.Parameters.AddWithValue("ip", (object?)ip ?? "");
        cmd.Parameters.AddWithValue("ua", Truncate(userAgent, 500));
        await cmd.ExecuteNonQueryAsync();
        return token;
    }

    /// <summary>
    /// Returns the session row if the token is valid, not expired, and not revoked.
    /// Also touches LastSeenAt so idle sessions are visible to admins.
    /// </summary>
    public async Task<SessionInfo?> ValidateAsync(string token, string? ip, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var conn = await _db.GetConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"UPDATE ""Sessions""
              SET ""LastSeenAt"" = NOW(),
                  ""LastIp"" = COALESCE(NULLIF(@ip, ''), ""LastIp""),
                  ""LastUserAgent"" = COALESCE(NULLIF(@ua, ''), ""LastUserAgent"")
              WHERE ""Token"" = @token AND ""RevokedAt"" IS NULL AND ""ExpiresAt"" > NOW()
              RETURNING ""Role"", ""DisplayName"", ""UserId"", ""GymDatabase"", ""GymId"", ""ExpiresAt""", conn);
        cmd.Parameters.AddWithValue("token", token);
        cmd.Parameters.AddWithValue("ip", (object?)ip ?? "");
        cmd.Parameters.AddWithValue("ua", Truncate(userAgent, 500));

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new SessionInfo
        {
            Token = token,
            Role = reader.GetString(0),
            DisplayName = reader.GetString(1),
            UserId = reader.GetInt32(2),
            GymDatabase = reader.GetString(3),
            GymId = reader.GetInt32(4),
            ExpiresAt = reader.GetDateTime(5),
        };
    }

    public async Task RevokeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        using var conn = await _db.GetConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"UPDATE ""Sessions"" SET ""RevokedAt"" = NOW()
              WHERE ""Token"" = @token AND ""RevokedAt"" IS NULL", conn);
        cmd.Parameters.AddWithValue("token", token);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Deletes expired/revoked rows older than 30 days. Intended for periodic cleanup.</summary>
    public async Task<int> PruneAsync()
    {
        using var conn = await _db.GetConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"DELETE FROM ""Sessions""
              WHERE (""ExpiresAt"" < NOW() - INTERVAL '30 days')
                 OR (""RevokedAt"" IS NOT NULL AND ""RevokedAt"" < NOW() - INTERVAL '30 days')", conn);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s!.Length <= max ? s : s.Substring(0, max));
}

public class SessionInfo
{
    public string Token { get; set; } = "";
    public string Role { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int UserId { get; set; }
    public string GymDatabase { get; set; } = "";
    public int GymId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
