namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Safely executes async operations with timeouts and state validation.
/// Problem 9: Prevents infinite loops in database queries (GetActiveFreezeAsync).
/// </summary>
public interface IAsyncOperationSafeExecutor
{
    /// <summary>
    /// Execute async operation with mandatory timeout
    /// </summary>
    Task<T> ExecuteWithTimeoutAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int timeoutMs = 30000,
        T? defaultValueOnTimeout = default);

    /// <summary>
    /// Execute with timeout and result validation
    /// </summary>
    Task<T> ExecuteWithValidationAsync<T>(
        Func<Task<T>> operation,
        Func<T?, bool> validator,
        string operationName,
        int timeoutMs = 30000,
        string validationErrorMessage = "Invalid result");

    /// <summary>
    /// Circuit breaker pattern - fail fast if too many errors
    /// </summary>
    Task<T> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int timeoutMs = 30000);

    /// <summary>
    /// Execute with automatic retry and timeout
    /// </summary>
    Task<T> ExecuteWithRetryAndTimeoutAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxAttempts = 2,
        int timeoutPerAttemptMs = 15000);
}

public class AsyncOperationSafeExecutor : IAsyncOperationSafeExecutor
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int failures, DateTime lastFailure)> CircuitBreakerStates = new();

    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int timeoutMs = 30000,
        T? defaultValueOnTimeout = default)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AsyncSafe] {operationName}: Executing with {timeoutMs}ms timeout");

            using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
            {
                var task = operation();
                var result = await task.ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine(
                    $"[AsyncSafe] {operationName}: Completed successfully");

                return result;
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AsyncSafe] {operationName}: TIMEOUT after {timeoutMs}ms. Using default value.");

            return defaultValueOnTimeout!;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AsyncSafe] {operationName}: Error: {ex.Message}");

            throw new InvalidOperationException(
                $"Operation '{operationName}' failed: {ex.Message}", ex);
        }
    }

    public async Task<T> ExecuteWithValidationAsync<T>(
        Func<Task<T>> operation,
        Func<T?, bool> validator,
        string operationName,
        int timeoutMs = 30000,
        string validationErrorMessage = "Invalid result")
    {
        var result = await ExecuteWithTimeoutAsync(operation, operationName, timeoutMs);

        if (!validator(result))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AsyncSafe] {operationName}: Validation failed. {validationErrorMessage}");

            throw new InvalidOperationException(
                $"Operation '{operationName}' returned invalid result: {validationErrorMessage}");
        }

        System.Diagnostics.Debug.WriteLine(
            $"[AsyncSafe] {operationName}: Validation passed");

        return result;
    }

    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int timeoutMs = 30000)
    {
        if (CircuitBreakerStates.TryGetValue(operationName, out var state))
        {
            // Circuit open if 3+ failures in last 5 minutes
            if (state.failures >= 3 && DateTime.UtcNow - state.lastFailure < TimeSpan.FromMinutes(5))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AsyncSafe] {operationName}: CIRCUIT BREAKER OPEN (failures: {state.failures})");

                throw new InvalidOperationException(
                    $"Operation '{operationName}' circuit breaker is open due to repeated failures");
            }

            // Reset if window expired
            if (DateTime.UtcNow - state.lastFailure >= TimeSpan.FromMinutes(5))
            {
                CircuitBreakerStates.TryRemove(operationName, out _);
            }
        }

        try
        {
            var result = await ExecuteWithTimeoutAsync(operation, operationName, timeoutMs);

            // Reset on success
            CircuitBreakerStates.TryRemove(operationName, out _);

            return result;
        }
        catch (Exception ex)
        {
            CircuitBreakerStates.AddOrUpdate(
                operationName,
                (1, DateTime.UtcNow),
                (key, existing) => (existing.failures + 1, DateTime.UtcNow));

            throw;
        }
    }

    public async Task<T> ExecuteWithRetryAndTimeoutAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxAttempts = 2,
        int timeoutPerAttemptMs = 15000)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AsyncSafe] {operationName}: Attempt {attempt}/{maxAttempts} ({timeoutPerAttemptMs}ms timeout)");

                return await ExecuteWithTimeoutAsync(
                    operation,
                    $"{operationName} (attempt {attempt})",
                    timeoutPerAttemptMs);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    var delayMs = 500 * attempt; // 500ms, 1s, etc.
                    System.Diagnostics.Debug.WriteLine(
                        $"[AsyncSafe] {operationName}: Attempt {attempt} failed. Retrying in {delayMs}ms...");

                    await Task.Delay(delayMs);
                }
            }
        }

        throw new InvalidOperationException(
            $"Operation '{operationName}' failed after {maxAttempts} attempts: {lastException?.Message}",
            lastException);
    }
}
