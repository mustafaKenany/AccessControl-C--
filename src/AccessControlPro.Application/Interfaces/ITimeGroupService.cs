using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public interface ITimeGroupService
{
    Task<IEnumerable<TimeGroup>> GetAllAsync();
    Task<TimeGroup?> GetByIdAsync(int id);
    Task<TimeGroup> CreateAsync(string nameEn, string nameAr, string scheduleJson);
    Task UpdateAsync(int id, string nameEn, string nameAr, string scheduleJson);
    Task DeleteAsync(int id);
    string BuildTimePiecesString(string scheduleJson);
}
