using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IAccessCardRepository
{
    Task<IEnumerable<AccessCard>> GetAllWithEmployeeAsync();
    Task<AccessCard?> GetByIdAsync(int id);
    Task<AccessCard?> GetByCardNumberAsync(string cardNumber);
    Task<IEnumerable<AccessCard>> GetByEmployeeIdAsync(int employeeId);
    Task AddAsync(AccessCard card);
    Task UpdateAsync(AccessCard card);
    Task DeleteAsync(int id);
    /// <summary>Get all active card numbers only (lightweight, no navigation properties)</summary>
    Task<IEnumerable<string>> GetAllActiveCardNumbersAsync();
    /// <summary>Get all active cards for device sync (no Employee photos loaded)</summary>
    Task<IEnumerable<AccessCard>> GetAllActiveForSyncAsync();
}
