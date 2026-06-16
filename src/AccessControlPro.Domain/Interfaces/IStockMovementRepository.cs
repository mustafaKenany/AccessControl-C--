using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId);
    Task AddAsync(StockMovement movement);
    /// <summary>Removes all movements tied to a purchase order — used when a PO is edited and its movements are rebuilt.</summary>
    Task DeleteByPurchaseOrderAsync(int purchaseOrderId);
}
