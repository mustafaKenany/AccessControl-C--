using System.Text.Json;
using AccessControlPro.Web.Data;
using Npgsql;
using WebPush;

namespace AccessControlPro.Web.Services;

/// <summary>
/// Web-push storage + sending for member renewal reminders.
/// Subscriptions are stored per-gym (each gym has its own Postgres DB). The table is
/// created lazily on first subscribe so no separate migration deploy is needed.
/// Sending is a no-op (Configured == false) until VAPID keys are set in config.
/// </summary>
public class PushService
{
    private readonly GymDbHelper _gymDb;
    private readonly VapidDetails? _vapid;
    private readonly WebPushClient _client = new();

    public bool Configured => _vapid != null;

    public PushService(GymDbHelper gymDb, IConfiguration config)
    {
        _gymDb = gymDb;
        var pub = config["WebPush:PublicKey"];
        var priv = config["WebPush:PrivateKey"];
        var subject = config["WebPush:Subject"];
        if (string.IsNullOrWhiteSpace(subject)) subject = "mailto:admin@hmtech.solutions";

        if (!string.IsNullOrWhiteSpace(pub) && !string.IsNullOrWhiteSpace(priv))
            _vapid = new VapidDetails(subject, pub, priv);
    }

    public static async Task EnsureTableAsync(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand(
            @"CREATE TABLE IF NOT EXISTS ""PushSubscriptions"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""PlayerId"" INT NOT NULL,
                ""Endpoint"" TEXT NOT NULL UNIQUE,
                ""P256dh"" TEXT NOT NULL,
                ""Auth"" TEXT NOT NULL,
                ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                ""LastSentAt"" TIMESTAMPTZ NULL
              )", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Store (or refresh) a player's push subscription in their gym DB.</summary>
    public async Task SaveSubscriptionAsync(string dbName, int playerId, string endpoint, string p256dh, string auth)
    {
        using var conn = await _gymDb.GetGymConnectionAsync(dbName);
        await EnsureTableAsync(conn);
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO ""PushSubscriptions"" (""PlayerId"", ""Endpoint"", ""P256dh"", ""Auth"")
              VALUES (@pid, @ep, @p, @a)
              ON CONFLICT (""Endpoint"") DO UPDATE
                SET ""PlayerId"" = @pid, ""P256dh"" = @p, ""Auth"" = @a", conn);
        cmd.Parameters.AddWithValue("pid", playerId);
        cmd.Parameters.AddWithValue("ep", endpoint);
        cmd.Parameters.AddWithValue("p", p256dh);
        cmd.Parameters.AddWithValue("a", auth);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Send a push to one subscription. Returns true on success. On 404/410 (the browser
    /// dropped the subscription) returns false AND the caller should delete the row.
    /// </summary>
    public async Task<(bool sent, bool gone)> SendAsync(
        string endpoint, string p256dh, string auth, string title, string body, string url)
    {
        if (_vapid == null) return (false, false);
        var sub = new PushSubscription(endpoint, p256dh, auth);
        var payload = JsonSerializer.Serialize(new { title, body, url });
        try
        {
            await _client.SendNotificationAsync(sub, payload, _vapid);
            return (true, false);
        }
        catch (WebPushException ex)
        {
            var gone = ex.StatusCode == System.Net.HttpStatusCode.NotFound
                    || ex.StatusCode == System.Net.HttpStatusCode.Gone;
            return (false, gone);
        }
        catch
        {
            return (false, false);
        }
    }
}
