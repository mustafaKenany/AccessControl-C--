namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Handles retry logic for SDK device operations with exponential backoff.
/// Improves reliability when devices timeout or are temporarily unreachable.
/// </summary>
public interface ISdkRetryPolicy
{
    /// <summary>
    /// Executes an SDK operation with automatic retry on failure.
    /// Returns true if successful, throws if all retries exhausted.
    /// </summary>
    Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3,
        int initialDelayMs = 500);

    /// <summary>
    /// Executes a synchronous SDK operation with retry (for non-async SDK methods)
    /// </summary>
    T ExecuteWithRetry<T>(
        Func<T> operation,
        string operationName,
        int maxRetries = 3,
        int initialDelayMs = 500);

    /// <summary>
    /// Fire-and-forget operation (doesn't throw, logs errors)
    /// </summary>
    Task ExecuteFireAndForgetAsync(
        Func<Task> operation,
        string operationName,
        int maxRetries = 2,
        Action<Exception>? onFailure = null);
}

public class SdkRetryPolicy : ISdkRetryPolicy
{
    private static readonly System.Diagnostics.TraceSource _trace = new("SDK.Retry");

    private static void LogWarning(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Warning, 0, message);
    }

    private static void LogError(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Error, 0, message);
    }

    private static void LogInfo(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Information, 0, message);
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3,
        int initialDelayMs = 500)
    {
        int delayMs = initialDelayMs;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                LogInfo($"[{operationName}] Attempt {attempt}/{maxRetries}");
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogWarning(
                    $"[{operationName}] Attempt {attempt} failed: {ex.GetType().Name}: {ex.Message}. " +
                    $"Retrying in {delayMs}ms...");

                await Task.Delay(delayMs);
                delayMs = (int)(delayMs * 1.5); // Exponential backoff
            }
        }

        // All retries exhausted
        throw new InvalidOperationException(
            $"Operation '{operationName}' failed after {maxRetries} attempts.");
    }

    public T ExecuteWithRetry<T>(
        Func<T> operation,
        string operationName,
        int maxRetries = 3,
        int initialDelayMs = 500)
    {
        int delayMs = initialDelayMs;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                LogInfo($"[{operationName}] Attempt {attempt}/{maxRetries}");
                return operation();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogWarning(
                    $"[{operationName}] Attempt {attempt} failed: {ex.GetType().Name}: {ex.Message}. " +
                    $"Retrying in {delayMs}ms...");

                Task.Delay(delayMs).Wait();
                delayMs = (int)(delayMs * 1.5);
            }
        }

        throw new InvalidOperationException(
            $"Operation '{operationName}' failed after {maxRetries} attempts.");
    }

    public async Task ExecuteFireAndForgetAsync(
        Func<Task> operation,
        string operationName,
        int maxRetries = 2,
        Action<Exception>? onFailure = null)
    {
        int delayMs = 300;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await operation();
                return; // Success
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogWarning(
                    $"[{operationName}] Background attempt {attempt} failed: {ex.Message}. Retrying...");
                await Task.Delay(delayMs);
                delayMs = (int)(delayMs * 1.5);
            }
            catch (Exception ex)
            {
                LogError($"[{operationName}] All background retries failed: {ex.Message}");
                onFailure?.Invoke(ex);
            }
        }
    }
}
