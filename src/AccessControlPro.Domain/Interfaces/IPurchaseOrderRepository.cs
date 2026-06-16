using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<IEnumerable<PurchaseOrder>> GetAllAsync();
    Task<PurchaseOrder?> GetByIdWithItemsAsync(int id);
    Task AddAsync(PurchaseOrder order);
    Task UpdateAsync(PurchaseOrder order);
    /// <summary>Updates a PO's header fields AND replaces its line items (adds/updates/removes) in one transaction.</summary>
    Task UpdateWithItemsAsync(PurchaseOrder order);
    /// <summary>Deletes a PO and its line items (cascade).</summary>
    Task DeleteAsync(int id);
}
