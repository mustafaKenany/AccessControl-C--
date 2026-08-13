using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

/// <summary>
/// Ported from the HikVision fork's PosTransactionRepository. Adapts the fork's PosDebt to our
/// Employee.Debt. This is the single load-bearing atomic engine for the POS retail chain.
/// </summary>
public class PosTransactionRepository : IPosTransactionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PosTransactionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task PersistAtomicAsync(
        IReadOnlyList<(int ProductId, int StockDelta)> stockChanges,
        IReadOnlyList<StockMovement> movements,
        Transaction? transaction,
        int? employeeId,
        decimal cardBalanceDelta,
        decimal debtDelta = 0)
    {
        await using var db = _factory.CreateDbContext();

        // The context enables retry-on-failure, which forbids a raw user transaction — the whole
        // unit must run inside an execution strategy so a transient failure retries the ENTIRE
        // transaction as one retriable block.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Start each (rare) retry from a clean slate so the passed-in movements/transaction
            // aren't double-tracked from a previous rolled-back attempt.
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // Re-read the products inside the transaction and apply the deltas here, so the write
                // reflects the real current stock (not a value read earlier on another connection).
                // UPDLOCK takes an update lock on each row so two terminals selling the SAME product at
                // the same time SERIALIZE — the second waits, then reads the already-decremented stock
                // and correctly rejects if it's now insufficient (no oversell to negative). Rows are
                // read in a stable id order so concurrent multi-item carts can't deadlock each other.
                if (stockChanges.Count > 0)
                {
                    var ids = stockChanges.Select(s => s.ProductId).Distinct().OrderBy(x => x).ToList();
                    var idList = string.Join(",", ids);   // ints only — no injection surface
                    var products = await db.Products
                        .FromSqlRaw($"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id IN ({idList})")
                        .ToListAsync();
                    var map = products.ToDictionary(p => p.Id);
                    foreach (var (productId, delta) in stockChanges)
                    {
                        if (!map.TryGetValue(productId, out var product))
                            throw new InvalidOperationException($"Product {productId} no longer exists.");
                        product.Stock += delta;
                        if (product.Stock < 0)
                            throw new InvalidOperationException($"Insufficient stock for product {productId}.");
                    }
                }

                if (employeeId.HasValue && (cardBalanceDelta != 0m || debtDelta != 0m))
                {
                    // Same UPDLOCK reasoning for the member's wallet: two concurrent card sales must not
                    // both pass a stale balance check and drive CardBalance negative.
                    var employee = await db.Employees
                        .FromSqlRaw("SELECT * FROM Employees WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", employeeId.Value)
                        .FirstOrDefaultAsync();
                    if (employee != null)
                    {
                        // Guard the wallet inside the lock: a card sale (negative delta) can't overdraw.
                        if (cardBalanceDelta < 0 && employee.CardBalance + cardBalanceDelta < 0)
                            throw new InvalidOperationException("Insufficient card balance.");
                        employee.CardBalance += cardBalanceDelta;
                        var debtBefore = employee.Debt;
                        employee.Debt += debtDelta;
                        if (employee.Debt < 0) employee.Debt = 0;   // never carry a negative debt
                        // Track the debt-age window: stamp when it first goes positive, clear when settled.
                        // Stored UTC to match Transaction.CreatedAt (system-wide "store UTC, display +3").
                        if (debtBefore <= 0 && employee.Debt > 0) employee.DebtSince = DateTime.UtcNow;
                        else if (employee.Debt <= 0) employee.DebtSince = null;
                    }
                }

                if (movements.Count > 0)
                    db.StockMovements.AddRange(movements);
                if (transaction != null)
                    db.Transactions.Add(transaction);

                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }
}
