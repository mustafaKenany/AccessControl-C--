using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllWithCardsAsync();
    Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
    Task<Employee?> GetByIdWithCardsAsync(int id);
    Task<Employee?> GetByEmployeeCodeAsync(string cardNo);
    Task<Employee?> GetByPhoneAsync(string phone);
    Task<bool> ExistsByNameAsync(string fullNameEn, int? excludeId = null);
    Task AddAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
    Task<IEnumerable<Employee>> GetBySubscriptionEndDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<Employee>> GetByStartDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<Employee>> GetFrozenAsync();
    Task<IEnumerable<Employee>> GetExpiredAsync();
}
