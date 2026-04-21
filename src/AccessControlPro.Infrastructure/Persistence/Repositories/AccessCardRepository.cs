using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AccessCardRepository : IAccessCardRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AccessCardRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<AccessCard>> GetAllWithEmployeeAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessCards.Include(c => c.Employee).ToListAsync();
    }

    public async Task<AccessCard?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessCards.Include(c => c.Employee).FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<AccessCard?> GetByCardNumberAsync(string cardNumber)
    {
        await using var db = _factory.CreateDbContext();
        // Try exact match first, then try without leading zeros (readers add leading zeros)
        var card = await db.AccessCards.Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);
        if (card == null && cardNumber.StartsWith('0'))
        {
            var trimmed = cardNumber.TrimStart('0');
            if (trimmed.Length > 0)
                card = await db.AccessCards.Include(c => c.Employee)
                    .FirstOrDefaultAsync(c => c.CardNumber == trimmed);
        }
        return card;
    }

    public async Task<IEnumerable<AccessCard>> GetByEmployeeIdAsync(int employeeId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessCards.Where(c => c.EmployeeId == employeeId).ToListAsync();
    }

    public async Task AddAsync(AccessCard card)
    {
        await using var db = _factory.CreateDbContext();
        db.AccessCards.Add(card);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AccessCard card)
    {
        await using var db = _factory.CreateDbContext();
        db.AccessCards.Update(card);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var card = await db.AccessCards.FindAsync(id);
        if (card != null)
        {
            db.AccessCards.Remove(card);
            await db.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<string>> GetAllActiveCardNumbersAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessCards
            .Where(c => c.IsActive)
            .Select(c => c.CardNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccessCard>> GetAllActiveForSyncAsync()
    {
        await using var db = _factory.CreateDbContext();
        // Load cards WITHOUT Employee navigation (no photos in memory)
        return await db.AccessCards
            .Where(c => c.IsActive)
            .ToListAsync();
    }
}
