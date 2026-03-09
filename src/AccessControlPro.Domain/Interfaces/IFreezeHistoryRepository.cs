using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IFreezeHistoryRepository
{
    Task AddAsync(FreezeHistory entity);
    Task UpdateAsync(FreezeHistory entity);
    Task<FreezeHistory?> GetActiveFreezeAsync(int employeeId);
}
