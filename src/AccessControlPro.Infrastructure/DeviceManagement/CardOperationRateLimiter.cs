namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Rate limiting for card operations to prevent DOS attacks on devices.
/// Tracks per-device card operations and blocks if rate exceeded.
/// </summary>
public interface ICardOperationRateLimiter
{
    /// <summary>
    /// Checks if a card operation is allowed for the device. Throws if rate limit exceeded.
    /// </summary>
    void ThrottleOrThrow(int deviceId, string operationType = "CardSync");

    /// <summary>
    /// Records a successful card operation
    /// </summary>
    void RecordOperation(int deviceId, string operationType = "CardSync");

    /// <summary>
    /// Reset rate limit for a device (after rate-limit window expires)
    /// </summary>
    void Reset(int deviceId);

    /// <summary>
    /// Get current operation count for device
    /// </summary>
    int GetOperationCount(int deviceId);
}

public class CardOperationRateLimiter : ICardOperationRateLimiter
{
    private class RateLimitBucket
    {
        public int OperationCount { get; set; }
        public DateTime WindowStart { get; set; }
    }

    private readonly Dictionary<int, RateLimitBucket> _limitBuckets = new();
    private readonly object _lock = new();

    // Configuration: max 100 card operations per minute per device
    private readonly int _maxOperationsPerMinute = 100;
    private readonly int _windowSizeSeconds = 60;

    public CardOperationRateLimiter(int maxOperationsPerMinute = 100, int windowSizeSeconds = 60)
    {
        _maxOperationsPerMinute = maxOperationsPerMinute;
        _windowSizeSeconds = windowSizeSeconds;
    }

    public void ThrottleOrThrow(int deviceId, string operationType = "CardSync")
    {
        lock (_lock)
        {
            _limitBuckets.TryGetValue(deviceId, out var bucket);

            // Create new bucket if doesn't exist or window expired
            if (bucket == null || DateTime.UtcNow - bucket.WindowStart > TimeSpan.FromSeconds(_windowSizeSeconds))
            {
                _limitBuckets[deviceId] = new RateLimitBucket
                {
                    OperationCount = 0,
                    WindowStart = DateTime.UtcNow
                };
                return; // First operation is always allowed
            }

            // Check if rate limit exceeded
            if (bucket.OperationCount >= _maxOperationsPerMinute)
            {
                var retryAfterSeconds = _windowSizeSeconds - (int)(DateTime.UtcNow - bucket.WindowStart).TotalSeconds;
                throw new InvalidOperationException(
                    $"Rate limit exceeded for device {deviceId} ({operationType}). " +
                    $"Max {_maxOperationsPerMinute} operations per {_windowSizeSeconds}s. " +
                    $"Retry after {retryAfterSeconds}s.");
            }

            // Cleanup expired buckets to prevent unbounded growth
            if (_limitBuckets.Count > 1000)
            {
                var expired = _limitBuckets
                    .Where(kv => DateTime.UtcNow - kv.Value.WindowStart > TimeSpan.FromSeconds(_windowSizeSeconds * 2))
                    .Select(kv => kv.Key).ToList();
                foreach (var key in expired)
                    _limitBuckets.Remove(key);
            }
        }
    }

    public void RecordOperation(int deviceId, string operationType = "CardSync")
    {
        lock (_lock)
        {
            if (_limitBuckets.TryGetValue(deviceId, out var bucket))
            {
                bucket.OperationCount++;
            }
        }
    }

    public void Reset(int deviceId)
    {
        lock (_lock)
        {
            _limitBuckets.Remove(deviceId);
        }
    }

    public int GetOperationCount(int deviceId)
    {
        lock (_lock)
        {
            if (_limitBuckets.TryGetValue(deviceId, out var bucket))
            {
                // Return 0 if window expired
                if (DateTime.UtcNow - bucket.WindowStart > TimeSpan.FromSeconds(_windowSizeSeconds))
                    return 0;

                return bucket.OperationCount;
            }
            return 0;
        }
    }
}
