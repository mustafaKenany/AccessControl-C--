namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Executes batch operations with error recovery.
/// Prevents partial failures where some operations succeed but others fail.
/// Problem 8: Freeze/Unfreeze cards on multiple devices without losing progress.
/// </summary>
public interface IBatchOperationExecutor
{
    /// <summary>
    /// Execute operation on multiple items with error tracking
    /// </summary>
    Task<BatchOperationResult<T>> ExecuteBatchAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        bool stopOnFirstError = false);

    /// <summary>
    /// Execute operation on multiple items with partial failure strategy
    /// </summary>
    Task<BatchOperationResult<T>> ExecuteBatchWithRetryAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        int maxRetries = 2,
        int delayBetweenRetriesMs = 500);

    /// <summary>
    /// Execute with timeout per item
    /// </summary>
    Task<BatchOperationResult<T>> ExecuteBatchWithTimeoutAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        int timeoutPerItemMs = 30000);
}

public class BatchOperationResult<T>
{
    public int Successful { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
    public List<(T item, Exception exception)> Failures { get; set; } = new();
    public bool AllSucceeded => Failed == 0;
    public bool HasPartialFailure => Successful > 0 && Failed > 0;
    public string Summary => $"Success: {Successful}/{Total}, Failed: {Failed}";
}

public class BatchOperationExecutor : IBatchOperationExecutor
{
    public async Task<BatchOperationResult<T>> ExecuteBatchAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        bool stopOnFirstError = false)
    {
        var itemList = items.ToList();
        var result = new BatchOperationResult<T> { Total = itemList.Count };

        for (int i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Batch] {operationName}: {i + 1}/{itemList.Count}");

                var success = await operation(item);

                if (success)
                    result.Successful++;
                else
                {
                    result.Failed++;
                    result.Failures.Add((item, new Exception($"Operation returned false")));

                    if (stopOnFirstError)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Batch] {operationName}: Stopped at first failure");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Failures.Add((item, ex));

                System.Diagnostics.Debug.WriteLine(
                    $"[Batch] {operationName}: Error on item {i + 1}: {ex.Message}");

                if (stopOnFirstError)
                    break;
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"[Batch] {operationName} completed: {result.Summary}");

        return result;
    }

    public async Task<BatchOperationResult<T>> ExecuteBatchWithRetryAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        int maxRetries = 2,
        int delayBetweenRetriesMs = 500)
    {
        var itemList = items.ToList();
        var result = new BatchOperationResult<T> { Total = itemList.Count };

        for (int i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            bool success = false;
            Exception? lastException = null;

            // Retry loop for each item
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Batch] {operationName}: Item {i + 1}/{itemList.Count}, Attempt {attempt}/{maxRetries}");

                    success = await operation(item);

                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Batch] {operationName}: Item {i + 1} succeeded on attempt {attempt}");
                        break;
                    }

                    if (attempt < maxRetries)
                        await Task.Delay(delayBetweenRetriesMs);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine(
                        $"[Batch] {operationName}: Item {i + 1} failed on attempt {attempt}: {ex.Message}");

                    if (attempt < maxRetries)
                        await Task.Delay(delayBetweenRetriesMs);
                }
            }

            if (success)
                result.Successful++;
            else
            {
                result.Failed++;
                result.Failures.Add((item, lastException ?? new Exception("All retries failed")));
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"[Batch] {operationName} with retry completed: {result.Summary}");

        return result;
    }

    public async Task<BatchOperationResult<T>> ExecuteBatchWithTimeoutAsync<T>(
        IEnumerable<T> items,
        Func<T, Task<bool>> operation,
        string operationName,
        int timeoutPerItemMs = 30000)
    {
        var itemList = items.ToList();
        var result = new BatchOperationResult<T> { Total = itemList.Count };

        for (int i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Batch] {operationName}: {i + 1}/{itemList.Count} (timeout: {timeoutPerItemMs}ms)");

                using (var cts = new System.Threading.CancellationTokenSource(timeoutPerItemMs))
                {
                    var task = operation(item);
                    var success = await task.ConfigureAwait(false);

                    if (success)
                        result.Successful++;
                    else
                    {
                        result.Failed++;
                        result.Failures.Add((item, new Exception("Operation returned false")));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.Failed++;
                result.Failures.Add((item, new TimeoutException(
                    $"Operation timed out after {timeoutPerItemMs}ms")));

                System.Diagnostics.Debug.WriteLine(
                    $"[Batch] {operationName}: Item {i + 1} timed out");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Failures.Add((item, ex));

                System.Diagnostics.Debug.WriteLine(
                    $"[Batch] {operationName}: Item {i + 1} error: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"[Batch] {operationName} with timeout completed: {result.Summary}");

        return result;
    }
}
