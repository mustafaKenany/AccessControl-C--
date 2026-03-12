using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Transactions;

/// <summary>
/// Ensures database transactions are atomic - either all succeed or all fail.
/// Prevents partial updates (e.g., stock reduced but income not recorded).
/// Also implements optimistic concurrency using RowVersion.
/// </summary>
public interface ITransactionSafetyManager
{
    /// <summary>
    /// Execute multiple operations atomically - all succeed or all rollback
    /// </summary>
    Task<T> ExecuteAtomicAsync<T>(
        Func<Task<T>> operation,
        string operationName);

    /// <summary>
    /// Execute with optimistic concurrency check using RowVersion
    /// </summary>
    Task<bool> ExecuteWithConcurrencyCheckAsync<TEntity>(
        Func<Task<bool>> operation,
        TEntity entity,
        byte[] expectedRowVersion,
        string operationName) where TEntity : class;

    /// <summary>
    /// Verify RowVersion hasn't changed before update
    /// </summary>
    Task<(bool isValid, string? error)> VerifyConcurrencyAsync<TEntity>(
        TEntity entity,
        byte[] expectedRowVersion) where TEntity : class;
}

public class TransactionSafetyManager : ITransactionSafetyManager
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TransactionSafetyManager(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<T> ExecuteAtomicAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        using (var context = _contextFactory.CreateDbContext())
        {
            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    System.Diagnostics.Debug.WriteLine($"[Atomic] {operationName} committed successfully");
                    return result;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    System.Diagnostics.Debug.WriteLine($"[Atomic] {operationName} rolled back: {ex.Message}");
                    throw new InvalidOperationException(
                        $"Transaction '{operationName}' failed and was rolled back. {ex.Message}", ex);
                }
            }
        }
    }

    public async Task<bool> ExecuteWithConcurrencyCheckAsync<TEntity>(
        Func<Task<bool>> operation,
        TEntity entity,
        byte[] expectedRowVersion,
        string operationName) where TEntity : class
    {
        var (isValid, error) = await VerifyConcurrencyAsync(entity, expectedRowVersion);
        if (!isValid)
            throw new InvalidOperationException($"Concurrency conflict: {error}");

        return await ExecuteAtomicAsync(operation, operationName);
    }

    public async Task<(bool isValid, string? error)> VerifyConcurrencyAsync<TEntity>(
        TEntity entity,
        byte[] expectedRowVersion) where TEntity : class
    {
        using (var context = _contextFactory.CreateDbContext())
        {
            var entry = context.Entry(entity);
            var rowVersionProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
            if (rowVersionProp == null)
                return (true, null); // Entity doesn't use RowVersion

            var currentRowVersion = rowVersionProp.CurrentValue as byte[];

            if (currentRowVersion == null || expectedRowVersion == null)
                return (false, "RowVersion cannot be null");

            if (!currentRowVersion.SequenceEqual(expectedRowVersion))
                return (false, "Entity was modified by another user. Please refresh and try again.");

            return (true, null);
        }
    }
}
