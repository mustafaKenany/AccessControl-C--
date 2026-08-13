using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId);
    /// <summary>All movements tied to a purchase order — used to reverse exactly what the PO actually added when deleting it.</summary>
    Task<IEnumerable<StockMovement>> GetByPurchaseOrderAsync(int purchaseOrderId);
    Task AddAsync(StockMovement movement);
    /// <summary>All movements (sale lines + any refunds) for the N most recent POS-sale receipts,
    /// with Product included — for the "refund from a recent sale" picker.</summary>
    Task<IEnumerable<StockMovement>> GetRecentSaleMovementsAsync(int receiptCount);
    /// <summary>All movements tied to one receipt (sale lines + refunds), Product included.</summary>
    Task<IEnumerable<StockMovement>> GetByReceiptNoAsync(string receiptNo);
    /// <summary>Movements in [from, to) with Product included — for the product-sales report/dashboard.</summary>
    Task<IEnumerable<StockMovement>> GetByDateRangeAsync(DateTime from, DateTime to);
    /// <summary>Removes all movements tied to a purchase order — used when a PO is edited and its movements are rebuilt.</summary>
    Task DeleteByPurchaseOrderAsync(int purchaseOrderId);
}
