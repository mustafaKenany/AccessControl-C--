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
}
