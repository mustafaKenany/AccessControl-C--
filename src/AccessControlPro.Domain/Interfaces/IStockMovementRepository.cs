using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId);
    Task AddAsync(StockMovement movement);
}
