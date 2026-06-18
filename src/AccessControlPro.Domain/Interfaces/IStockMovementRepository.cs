using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId);
    /// <summary>All movements tied to a purchase order — used to reverse exactly what the PO actually added when deleting it.</summary>
    Task<IEnumerable<StockMovement>> GetByPurchaseOrderAsync(int purchaseOrderId);
    Task AddAsync(StockMovement movement);
    /// <summary>Removes all movements tied to a purchase order — used when a PO is edited and its movements are rebuilt.</summary>
    Task DeleteByPurchaseOrderAsync(int purchaseOrderId);
}
