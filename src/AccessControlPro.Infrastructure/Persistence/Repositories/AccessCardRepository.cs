using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AccessCardRepository : IAccessCardRepository
{
    private readonly AppDbContext _context;

    public AccessCardRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<AccessCard>> GetAllWithEmployeeAsync()
        => await _context.AccessCards.Include(c => c.Employee).ToListAsync();

    public async Task<AccessCard?> GetByIdAsync(int id)
        => await _context.AccessCards.Include(c => c.Employee).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<AccessCard?> GetByCardNumberAsync(string cardNumber)
        => await _context.AccessCards.FirstOrDefaultAsync(c => c.CardNumber == cardNumber);

    public async Task<IEnumerable<AccessCard>> GetByEmployeeIdAsync(int employeeId)
        => await _context.AccessCards.Where(c => c.EmployeeId == employeeId).ToListAsync();

    public async Task AddAsync(AccessCard card)
    {
        _context.AccessCards.Add(card);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AccessCard card)
    {
        _context.AccessCards.Update(card);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var card = await _context.AccessCards.FindAsync(id);
        if (card != null)
        {
            _context.AccessCards.Remove(card);
            await _context.SaveChangesAsync();
        }
    }
}
