using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<IEnumerable<PurchaseOrder>> GetAllAsync();
    Task<PurchaseOrder?> GetByIdWithItemsAsync(int id);
    Task AddAsync(PurchaseOrder order);
    Task UpdateAsync(PurchaseOrder order);
}
