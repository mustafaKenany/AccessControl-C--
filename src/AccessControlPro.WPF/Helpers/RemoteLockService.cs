using System.IO;
using System.Text.Json;
using AccessControlPro.Application.Services;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Payment-enforcement lock. Polls the cloud for this gym's lock flag and applies a 7-day
/// offline grace so the customer can't bypass it by disconnecting the internet. Locking only
/// blocks app usage — it never touches the customer's data.
/// </summary>
public static class RemoteLockService
{
    private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, ".remote_lock_state");
    // 30-day offline grace: a paying customer with a long internet outage isn't locked out over a
    // temporary problem. An offline-unlock code (vendor-issued, machine-bound) can reset it sooner.
    private static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(30);

    public readonly struct LockResult
    {
        public bool Locked { get; init; }
        public string Message { get; init; }
    }

    private class LockState
    {
        public DateTime LastConfirmedActiveUtc { get; set; }
        public bool Locked { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Checks the cloud, applies the offline grace, persists state, returns the effective lock.
    /// </summary>
    public static async Task<LockResult> EvaluateAsync()
    {
        var state = LoadState();
        var (reachable, locked, message) = await CloudSyncService.CheckRemoteLockAsync();

        if (reachable)
        {
            if (locked)
            {
                state.Locked = true;
                state.Message = string.IsNullOrWhiteSpace(message) ? DefaultLockMsg() : message;
            }
            else
            {
                state.Locked = false;
                state.Message = "";
                state.LastConfirmedActiveUtc = DateTime.UtcNow; // fresh "active" confirmation
            }
            SaveState(state);
            return new LockResult { Locked = state.Locked, Message = state.Message };
        }

        // Cloud unreachable — apply cached state + offline grace.
        if (state.Locked)
            return new LockResult { Locked = true, Message = Coalesce(state.Message, DefaultLockMsg()) };

        if (DateTime.UtcNow - state.LastConfirmedActiveUtc > OfflineGrace)
            return new LockResult { Locked = true, Message = OfflineLockMsg() };

        return new LockResult { Locked = false, Message = "" };
    }

    /// <summary>
    /// Emergency offline unlock: re-seeds the grace (LastConfirmedActiveUtc = now) so a stranded
    /// customer who entered a valid vendor unlock code gets another full offline grace window.
    /// Does nothing to a cloud-confirmed lock — that still requires the admin to unlock from the cloud.
    /// </summary>
    public static bool TryOfflineUnlock(string code)
    {
        try
        {
            var license = new AccessControlPro.Application.Services.LicenseService();
            if (!license.VerifyOfflineUnlockCode(code)) return false;

            var state = LoadState();
            // Only clears the OFFLINE-grace lock; a deliberate cloud lock stays until cleared online.
            if (state.Locked) return false;
            state.LastConfirmedActiveUtc = DateTime.UtcNow;
            SaveState(state);
            return true;
        }
        catch { return false; }
    }

    private static LockState LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var s = JsonSerializer.Deserialize<LockState>(File.ReadAllText(StatePath));
                if (s != null)
                {
                    if (s.LastConfirmedActiveUtc == default) s.LastConfirmedActiveUtc = DateTime.UtcNow;
                    return s;
                }
            }
        }
        catch { }
        // First run: seed "now" so a brand-new install isn't immediately offline-locked.
        return new LockState { LastConfirmedActiveUtc = DateTime.UtcNow };
    }

    private static void SaveState(LockState s)
    {
        try { File.WriteAllText(StatePath, JsonSerializer.Serialize(s)); } catch { }
    }

    private static string Coalesce(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;

    private static string DefaultLockMsg()
        => "تم إيقاف البرنامج. يرجى التواصل مع المزود لإعادة التفعيل.\n" +
           "The software has been disabled. Please contact your provider to reactivate.";

    private static string OfflineLockMsg()
        => "تعذّر التحقق من الترخيص عبر الإنترنت لفترة طويلة. يرجى توصيل الجهاز بالإنترنت أو التواصل مع المزود.\n" +
           "License could not be verified online for too long. Please connect to the internet or contact your provider.";
}
