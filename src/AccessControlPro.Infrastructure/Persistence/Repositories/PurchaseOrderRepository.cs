using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PurchaseOrderRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<PurchaseOrder>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdWithItemsAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
    }

    public async Task AddAsync(PurchaseOrder order)
    {
        await using var db = _factory.CreateDbContext();
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PurchaseOrder order)
    {
        await using var db = _factory.CreateDbContext();
        db.PurchaseOrders.Update(order);
        await db.SaveChangesAsync();
    }

    public async Task UpdateWithItemsAsync(PurchaseOrder order)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == order.Id)
            ?? throw new InvalidOperationException($"Purchase order with ID {order.Id} not found.");

        existing.SupplierId = order.SupplierId;
        existing.OrderDate = order.OrderDate;
        existing.TotalAmount = order.TotalAmount;
        existing.Discount = order.Discount;
        existing.AmountPaid = order.AmountPaid;
        existing.PaymentStatus = order.PaymentStatus;
        existing.Notes = order.Notes;

        // Replace line items: clearing the tracked collection deletes the old rows (orphans),
        // then we add the new set. Done on a tracked entity so EF emits the right delete/insert.
        existing.Items.Clear();
        foreach (var it in order.Items)
            existing.Items.Add(new PurchaseOrderItem
            {
                ProductId = it.ProductId,
                Quantity = it.Quantity,
                UnitCost = it.UnitCost
            });

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == id);
        if (existing == null) return;
        db.PurchaseOrders.Remove(existing); // line items cascade-delete with the order
        await db.SaveChangesAsync();
    }
}
