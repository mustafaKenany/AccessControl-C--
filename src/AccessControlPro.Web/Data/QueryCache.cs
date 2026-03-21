using System.Collections.Concurrent;

namespace AccessControlPro.Web.Data;

/// <summary>
/// Simple in-memory cache for database query results.
/// Caches for 5 minutes (matches sync interval).
/// </summary>
public static class QueryCache
{
    private static readonly ConcurrentDictionary<string, (object data, DateTime expiry)> _cache = new();
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    public static T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && entry.expiry > DateTime.UtcNow)
            return entry.data as T;
        return null;
    }

    public static void Set<T>(string key, T data, TimeSpan? expiry = null) where T : class
    {
        _cache[key] = (data, DateTime.UtcNow + (expiry ?? DefaultExpiry));
    }

    public static void InvalidateAll()
    {
        _cache.Clear();
    }

    public static void Invalidate(string keyPrefix)
    {
        foreach (var key in _cache.Keys)
            if (key.StartsWith(keyPrefix))
                _cache.TryRemove(key, out _);
    }
}
