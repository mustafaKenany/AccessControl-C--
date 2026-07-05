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

        // Exact match — works for registered cards typed identically to what the device reads.
        var card = await db.AccessCards.Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);
        if (card != null) return card;

        // Leading-zero variant — handles "0366549" vs "366549" (registered without leading 0).
        if (cardNumber.StartsWith('0'))
        {
            var trimmed = cardNumber.TrimStart('0');
            if (trimmed.Length > 0)
            {
                card = await db.AccessCards.Include(c => c.Employee)
                    .FirstOrDefaultAsync(c => c.CardNumber == trimmed);
                if (card != null) return card;
            }
        }

        // Wiegand 8H10D variant — the device reads cards with an extra trailing digit
        // (check/parity bit). E.g. card registered as "0366549" gets scanned as "3665490";
        // card registered as "0374205" gets scanned as "3742052". Diving the scanned value
        // by 10 and stripping leading zeros recovers the registered number.
        // Verified pattern: 3665490 / 10 == 366549 (== "0366549" without leading zero).
        if (cardNumber.Length > 1 && cardNumber.All(char.IsDigit))
        {
            if (long.TryParse(cardNumber, out var asInt))
            {
                var truncated = (asInt / 10).ToString();
                if (truncated.Length > 0)
                {
                    // Try truncated (e.g. "366549") AND truncated with leading 0 (e.g. "0366549")
                    var paddedTruncated = "0" + truncated;
                    card = await db.AccessCards.Include(c => c.Employee)
                        .FirstOrDefaultAsync(c => c.CardNumber == truncated || c.CardNumber == paddedTruncated);
                    if (card != null) return card;
                }
            }
        }

        return null;
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
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // The card row no longer matches (0 rows affected) — a concurrent cloud sync or
            // migration re-keyed/removed it while this detached entity was held in memory.
            // AccessCard carries no RowVersion token, so this can only mean "row gone", and
            // there is nothing left to persist. Swallow rather than crash the caller.
        }
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
