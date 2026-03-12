using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SupplierRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Supplier>> GetAllActiveAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Suppliers.FindAsync(id);
    }

    public async Task AddAsync(Supplier supplier)
    {
        await using var db = _factory.CreateDbContext();
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Supplier supplier)
    {
        await using var db = _factory.CreateDbContext();
        db.Suppliers.Update(supplier);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            supplier.IsActive = false;
            await db.SaveChangesAsync();
        }
    }
}
