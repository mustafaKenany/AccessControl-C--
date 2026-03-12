using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
