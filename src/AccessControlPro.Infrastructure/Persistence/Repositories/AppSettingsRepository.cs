using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AppSettingsRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<AppSettings?> GetAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.AppSettings.FirstOrDefaultAsync();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.AppSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            db.AppSettings.Add(settings);
        }
        else
        {
            existing.CompanyName = settings.CompanyName;
            existing.GymName = settings.GymName;
            existing.LogoPath = settings.LogoPath;
            existing.Phone = settings.Phone;
            existing.Address = settings.Address;
            existing.Owner = settings.Owner;
        }
        await db.SaveChangesAsync();
    }
}
