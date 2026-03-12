using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IAppSettingsRepository
{
    Task<AppSettings?> GetAsync();
    Task SaveAsync(AppSettings settings);
}
