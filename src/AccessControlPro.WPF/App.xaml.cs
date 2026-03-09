using System.IO;
using System.Reflection;
using System.Windows;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Infrastructure;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.ViewModels;
using AccessControlPro.WPF.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF;

public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");

    public App()
    {
        // Global exception handlers — write crash log before app dies
        DispatcherUnhandledException += (s, e) =>
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // Prevent crash — show message instead
            System.Windows.MessageBox.Show(
                $"An error occurred. Details saved to:\n{CrashLogPath}\n\n{e.Exception.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            WriteCrashLog("UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            WriteCrashLog("UnobservedTaskException", e.Exception);
            e.SetObserved(); // Prevent crash
        };

        // Resolve .NET Framework assemblies (FCardCDrive.dll) from app directory
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
            return null;
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var msg = $"\n=== [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source} ===\n" +
                      $"{ex?.GetType().FullName}: {ex?.Message}\n" +
                      $"Stack:\n{ex?.StackTrace}\n" +
                      (ex?.InnerException != null
                          ? $"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n"
                          : "");
            File.AppendAllText(CrashLogPath, msg);
        }
        catch { /* Last resort — can't even write log */ }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Connection string - update with your SQL Server details
        var connectionString = "Server=localhost;Database=AccessControlPro;User Id=sa;Password=123;TrustServerCertificate=True;";

        // Infrastructure (DB + SDK + Repositories)
        services.AddInfrastructure(connectionString);

        // Singleton - current logged-in user
        services.AddSingleton<CurrentUserService>();

        // Application Services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDoorService, DoorService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDeletedEmployeeService, DeletedEmployeeService>();
        services.AddScoped<IAuthService, AuthService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DoorsViewModel>();
        services.AddTransient<EmployeesViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DeletedRecordsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Auto-create/migrate database on startup
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();

                // Seed default admin user synchronously (avoids async deadlock on UI thread)
                if (!db.Users.Any(u => u.Username == "admin"))
                {
                    db.Users.Add(new AppUser
                    {
                        Username = "admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                        DisplayName = "Administrator",
                        Role = "Admin",
                        IsActive = true
                    });
                    db.SaveChanges();
                }
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_DbMigration", ex);
            System.Windows.MessageBox.Show(
                $"Database error:\n{ex.Message}\n\nInner: {ex.InnerException?.Message}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        try
        {
            // Prevent auto-shutdown when LoginWindow closes (it's the only window at that point)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Show login window — use a scope for proper DbContext lifecycle
            using var loginScope = _serviceProvider.CreateScope();
            var authService = loginScope.ServiceProvider.GetRequiredService<IAuthService>();
            var currentUser = _serviceProvider.GetRequiredService<CurrentUserService>();
            var loginWindow = new LoginWindow(authService, currentUser);
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_LoginWindow", ex);
            System.Windows.MessageBox.Show(
                $"Login window error:\n{ex.Message}\n\nInner: {ex.InnerException?.Message}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
