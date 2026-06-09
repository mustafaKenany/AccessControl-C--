using AccessControlPro.Web.Data;
using Npgsql;

namespace AccessControlPro.Web.Services;

/// <summary>
/// Background job: once-ish per day, find members whose subscription expires within the
/// next 3 days and who have a push subscription, and send a bilingual renewal reminder.
/// Multi-tenant — loops every active gym DB. Safe no-op until VAPID keys are configured.
/// </summary>
public class PushReminderHostedService : BackgroundService
{
    private readonly GymDbHelper _gymDb;
    private readonly PushService _push;
    private readonly ILogger<PushReminderHostedService> _log;

    // Send during the last N days before expiry; re-check every 12h but don't re-send
    // to the same device more than once per ~20h (so a member gets ~3 nudges: 3/2/1 days).
    private const int ReminderWindowDays = 3;

    public PushReminderHostedService(GymDbHelper gymDb, PushService push, ILogger<PushReminderHostedService> log)
    {
        _gymDb = gymDb;
        _push = push;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before the first run.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_push.Configured)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (Exception ex) { _log.LogError(ex, "Push reminder run failed"); }
            }

            try { await Task.Delay(TimeSpan.FromHours(12), stoppingToken); }
            catch { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        // Enumerate active gyms from the master registry.
        var gyms = new List<string>();
        using (var master = await _gymDb.GetMasterConnectionAsync())
        using (var cmd = new NpgsqlCommand(@"SELECT ""DatabaseName"" FROM ""Gyms"" WHERE ""IsActive"" = TRUE", master))
        using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct)) gyms.Add(r.GetString(0));
        }

        int totalSent = 0;
        foreach (var dbName in gyms)
        {
            try { totalSent += await ProcessGymAsync(dbName, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "Push reminders skipped for gym {Db}", dbName); }
        }

        if (totalSent > 0) _log.LogInformation("Push reminders sent: {Count}", totalSent);
    }

    private async Task<int> ProcessGymAsync(string dbName, CancellationToken ct)
    {
        using var conn = await _gymDb.GetGymConnectionAsync(dbName);
        await PushService.EnsureTableAsync(conn);

        // Players expiring within the window who have a subscription not nudged in the last ~20h.
        var targets = new List<(int subId, string endpoint, string p256dh, string auth, string nameEn, string nameAr, int daysLeft)>();
        using (var cmd = new NpgsqlCommand(
            @"SELECT ps.""Id"", ps.""Endpoint"", ps.""P256dh"", ps.""Auth"",
                     COALESCE(p.""FullNameEn"",''), COALESCE(p.""FullNameAr"",''),
                     GREATEST(0, EXTRACT(DAY FROM (p.""EndDate"" - NOW()))::int) AS days_left
              FROM ""PushSubscriptions"" ps
              JOIN ""Players"" p ON p.""Id"" = ps.""PlayerId""
              WHERE p.""IsDeleted"" = FALSE
                AND p.""EndDate"" >= NOW()
                AND p.""EndDate"" <= NOW() + (@win || ' days')::interval
                AND (ps.""LastSentAt"" IS NULL OR ps.""LastSentAt"" < NOW() - INTERVAL '20 hours')", conn))
        {
            cmd.Parameters.AddWithValue("win", ReminderWindowDays.ToString());
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                targets.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                             r.GetString(4), r.GetString(5), r.GetInt32(6)));
            }
        }

        int sent = 0;
        foreach (var t in targets)
        {
            var title = "تذكير الاشتراك / Subscription reminder";
            var body = t.daysLeft <= 0
                ? "ينتهي اشتراكك اليوم. يرجى التجديد. — Your subscription expires today. Please renew."
                : $"ينتهي اشتراكك خلال {t.daysLeft} يوم. — Your subscription expires in {t.daysLeft} day(s).";

            var (ok, gone) = await _push.SendAsync(t.endpoint, t.p256dh, t.auth, title, body, "/my");

            if (ok)
            {
                using var upd = new NpgsqlCommand(
                    @"UPDATE ""PushSubscriptions"" SET ""LastSentAt"" = NOW() WHERE ""Id"" = @id", conn);
                upd.Parameters.AddWithValue("id", t.subId);
                await upd.ExecuteNonQueryAsync(ct);
                sent++;
            }
            else if (gone)
            {
                // Browser dropped this subscription — clean it up so we stop trying.
                using var del = new NpgsqlCommand(@"DELETE FROM ""PushSubscriptions"" WHERE ""Id"" = @id", conn);
                del.Parameters.AddWithValue("id", t.subId);
                await del.ExecuteNonQueryAsync(ct);
            }
        }
        return sent;
    }
}
