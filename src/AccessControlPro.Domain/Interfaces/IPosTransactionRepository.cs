using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

/// <summary>
/// Atomic write engine for POS money/stock mutations. Everything that moves stock, card balance,
/// or player debt (sell, refund, refund-by-receipt, top-up, settle-debt, stock-take) flows through
/// <see cref="PersistAtomicAsync"/> so the whole unit commits or rolls back together, under row-locks
/// that serialize concurrent terminals (no oversell to negative stock, no overdrawn card balance).
/// </summary>
public interface IPosTransactionRepository
{
    /// <param name="stockChanges">(ProductId, signed stock delta) — negative sells, positive returns/receives.</param>
    /// <param name="movements">Stock-movement audit rows to insert.</param>
    /// <param name="transaction">Optional money transaction to insert (sale income, refund expense, …).</param>
    /// <param name="employeeId">Player whose card balance / debt is affected (null = walk-in cash).</param>
    /// <param name="cardBalanceDelta">Signed change to the player's prepaid card balance.</param>
    /// <param name="debtDelta">Signed change to the player's POS debt (also maintains DebtSince aging).</param>
    Task PersistAtomicAsync(
        IReadOnlyList<(int ProductId, int StockDelta)> stockChanges,
        IReadOnlyList<StockMovement> movements,
        Transaction? transaction,
        int? employeeId,
        decimal cardBalanceDelta,
        decimal debtDelta = 0);
}
