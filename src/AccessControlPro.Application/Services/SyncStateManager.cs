using System.IO;
using System.Text.Json;

namespace AccessControlPro.Application.Services;

/// <summary>
/// Tracks the last successful cloud sync timestamp so the next sync can send only
/// rows changed since then (delta sync) instead of all rows every time.
/// </summary>
public static class SyncStateManager
{
    private static readonly string StatePath = Path.Combine(AppContext.BaseDirectory, "sync_state.json");
    private static readonly object _lock = new();

    private class State
    {
        public DateTime? LastSyncAt { get; set; }
    }

    public static DateTime? LoadLastSyncAt()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(StatePath)) return null;
                var json = File.ReadAllText(StatePath);
                var state = JsonSerializer.Deserialize<State>(json);
                return state?.LastSyncAt;
            }
            catch { return null; }
        }
    }

    public static void SaveLastSyncAt(DateTime utc)
    {
        lock (_lock)
        {
            try
            {
                var state = new State { LastSyncAt = utc };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StatePath, json);
            }
            catch { /* best effort — next sync will re-send anything missed */ }
        }
    }

    /// <summary>Delete sync state — forces next sync to be a full sync. Useful for recovery.</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            try { if (File.Exists(StatePath)) File.Delete(StatePath); } catch { }
        }
    }
}
