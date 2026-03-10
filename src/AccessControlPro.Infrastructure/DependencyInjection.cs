using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Infrastructure.Logging;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.Infrastructure.Persistence.Repositories;
using AccessControlPro.SDK.Wrapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Database - switch provider here (SqlServer → MySQL)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDoorRepository, DoorRepository>();
        services.AddScoped<IAccessEventRepository, AccessEventRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IAccessCardRepository, AccessCardRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDeletedEmployeeRepository, DeletedEmployeeRepository>();
        services.AddScoped<IFreezeHistoryRepository, FreezeHistoryRepository>();

        // SDK
        services.AddSingleton<IAccessControlSdk, AccessControlSdkWrapper>();

        // Logging
        services.AddSingleton<ISessionLogger, SessionFileLogger>();

        return services;
    }
}
