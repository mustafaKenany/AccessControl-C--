using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Infrastructure.DeviceManagement;
using AccessControlPro.Infrastructure.Logging;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.Infrastructure.Persistence.Repositories;
using AccessControlPro.Infrastructure.Security;
using AccessControlPro.Infrastructure.Transactions;
using AccessControlPro.Infrastructure.Validation;
using AccessControlPro.SDK.Wrapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Add MARS to prevent "open DataReader" errors in concurrent scenarios
        var marsConn = connectionString.TrimEnd(';') + ";MultipleActiveResultSets=True;";

        // Retry policy: survives transient SQL Server outages (Windows Update restarts the
        // SQL Server service, brief network hiccups, deadlock victims). EF Core uses
        // exponential backoff between retries (~2s, ~4s, ~8s) and only retries SQL errors
        // tagged as transient — never retries syntax errors, constraint violations, etc.
        static void ConfigureSql(Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder b)
        {
            b.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);

            // Pin SQL generation to SQL Server 2014 (compat 120). EF Core 8 otherwise
            // emits OPENJSON(...) WITH (...) for multi-row inserts (e.g. a purchase order
            // with several items), which requires SQL Server 2016+ at compat level 130 and
            // fails with "Incorrect syntax near the keyword 'WITH'" on older installs.
            // Level 120 makes EF use the classic multi-row insert that runs everywhere.
            b.UseCompatibilityLevel(120);
        }

        // DbContextFactory for WPF desktop — each repo method creates its own context
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(marsConn, ConfigureSql));

        // Also register AppDbContext directly (used by DatabaseMigrator via scope)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(marsConn, ConfigureSql), ServiceLifetime.Transient);

        // Repositories — singleton is safe because they use factory per-method
        services.AddSingleton<IDeviceRepository, DeviceRepository>();
        services.AddSingleton<IDoorRepository, DoorRepository>();
        services.AddSingleton<IAccessEventRepository, AccessEventRepository>();
        services.AddSingleton<IEmployeeRepository, EmployeeRepository>();
        services.AddSingleton<IAccessCardRepository, AccessCardRepository>();
        services.AddSingleton<IAuditLogRepository, AuditLogRepository>();
        services.AddSingleton<IDeletedEmployeeRepository, DeletedEmployeeRepository>();
        services.AddSingleton<IFreezeHistoryRepository, FreezeHistoryRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        services.AddSingleton<ILookupRepository, LookupRepository>();
        services.AddSingleton<ISupplierRepository, SupplierRepository>();
        services.AddSingleton<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<ICardDeviceSyncRepository, CardDeviceSyncRepository>();
        services.AddSingleton<IQrPassRepository, QrPassRepository>();
        services.AddSingleton<ITimeGroupRepository, TimeGroupRepository>();
        services.AddSingleton<IPosShiftRepository, PosShiftRepository>();

        // SDK
        services.AddSingleton<IAccessControlSdk, AccessControlSdkWrapper>();

        // Device Management & Communication Safety
        services.AddSingleton<IDevicePasswordEncryption, DevicePasswordEncryption>();
        services.AddSingleton<IDeviceParameterValidator, DeviceParameterValidator>();
        services.AddSingleton<ICardOperationRateLimiter, CardOperationRateLimiter>();
        services.AddSingleton<ISdkRetryPolicy, SdkRetryPolicy>();
        services.AddSingleton<ICardDoorPermissionsValidator, CardDoorPermissionsValidator>();
        services.AddSingleton<IDeviceStatusMonitor, DeviceStatusMonitor>();
        services.AddSingleton<ISafeDeviceCommunication, SafeDeviceCommunication>();
        services.AddSingleton<ISafeEnumParser, SafeEnumParser>();
        services.AddSingleton<IBatchOperationExecutor, BatchOperationExecutor>();
        services.AddSingleton<IAsyncOperationSafeExecutor, AsyncOperationSafeExecutor>();

        // Database & Data Integrity
        services.AddScoped<ITransactionSafetyManager, TransactionSafetyManager>();

        // Validation Services
        services.AddSingleton<IUniversalDataValidator, UniversalDataValidator>();
        services.AddSingleton<IFinancialSafetyValidator, FinancialSafetyValidator>();

        // Security Services
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IDataEncryptionService, DataEncryptionService>();
        services.AddSingleton<IEnhancedAuthenticationService, EnhancedAuthenticationService>();
        services.AddSingleton<IEnhancedAuditLogger, EnhancedAuditLogger>();

        // Cleanup
        services.AddScoped<Domain.Interfaces.ICleanupService, Persistence.CleanupService>();

        // Logging
        services.AddSingleton<ISessionLogger, Logging.SessionFileLogger>();

        return services;
    }
}
