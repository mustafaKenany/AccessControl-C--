using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public StockMovementRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<StockMovement>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetRecentSaleMovementsAsync(int receiptCount)
    {
        await using var db = _factory.CreateDbContext();
        // The N most recent sale receipts (by last movement time)...
        var receipts = await db.StockMovements
            .Where(m => m.ReceiptNo != "" && m.Reference == "POS Sale")
            .GroupBy(m => m.ReceiptNo)
            .Select(g => new { Receipt = g.Key, Last = g.Max(x => x.CreatedAt) })
            .OrderByDescending(x => x.Last)
            .Take(receiptCount)
            .Select(x => x.Receipt)
            .ToListAsync();
        // ...and ALL their movements (sale lines + any refunds) so remaining-refundable can be computed.
        return await db.StockMovements
            .Include(m => m.Product)
            .Where(m => m.ReceiptNo != "" && receipts.Contains(m.ReceiptNo))
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByReceiptNoAsync(string receiptNo)
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Include(m => m.Product)
            .Where(m => m.ReceiptNo == receiptNo)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(StockMovement movement)
    {
        await using var db = _factory.CreateDbContext();
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByPurchaseOrderAsync(int purchaseOrderId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Where(m => m.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();
    }

    public async Task DeleteByPurchaseOrderAsync(int purchaseOrderId)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.StockMovements
            .Where(m => m.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();
        if (rows.Count == 0) return;
        db.StockMovements.RemoveRange(rows);
        await db.SaveChangesAsync();
    }
}
