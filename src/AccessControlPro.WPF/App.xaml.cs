using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Infrastructure;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.ViewModels;
using AccessControlPro.WPF.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private readonly ServiceProvider _serviceProvider;
    private DispatcherTimer? _cleanupTimer;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");

    public App()
    {
        // Global exception handlers — write crash log before app dies
        DispatcherUnhandledException += (s, e) =>
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // Prevent crash — show message instead

            if (DbConnectionHelper.IsConnectionError(e.Exception))
            {
                CustomMessageBox.Show(
                    "Database server connection lost.\nPlease check that the database server is running and the network is connected.",
                    "Connection Lost", MsgType.Error);
            }
            else
            {
                CustomMessageBox.Show(
                    $"An error occurred. Details saved to:\n{CrashLogPath}\n\n{e.Exception.Message}",
                    "Error", MsgType.Error);
            }
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

    private static string LoadConnectionString()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                cs.TryGetProperty("DefaultConnection", out var conn))
                return conn.GetString() ?? "";
        }
        return "Server=localhost;Database=AccessControlPro;User Id=sa;Password=123;TrustServerCertificate=True;";
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var connectionString = LoadConnectionString();

        // Infrastructure (DB + SDK + Repositories)
        services.AddInfrastructure(connectionString);

        // Singleton - current logged-in user
        services.AddSingleton<CurrentUserService>();

        // License
        services.AddSingleton<ILicenseService, LicenseService>();

        // Application Services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDoorService, DoorService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDeletedEmployeeService, DeletedEmployeeService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAppSettingsService, AppSettingsService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IAccessEventService, AccessEventService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ICashFlowService, CashFlowService>();
        services.AddScoped<IMigrationService, MigrationService>();
        // POS moved to separate AccessControlPro.POS app

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DoorsViewModel>();
        services.AddTransient<EmployeesViewModel>();
        services.AddTransient<EventsViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DeletedRecordsViewModel>();
        services.AddTransient<MonitorViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<CashFlowViewModel>();
        // PosViewModel moved to AccessControlPro.POS

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Single-instance guard: kill any stale processes from previous runs ──
        KillOtherInstances();
        _singleInstanceMutex = new Mutex(true, "AccessControlPro_SingleInstance", out bool isNew);
        if (!isNew)
        {
            CustomMessageBox.Show(
                "HM-GymManagement is already running.",
                "Already Running", MsgType.Info);
            Shutdown();
            return;
        }

        // ── First-run setup wizard ──
        if (!SetupWizardWindow.IsSetupComplete())
        {
            var wizard = new SetupWizardWindow();
            if (wizard.ShowDialog() != true || !wizard.SetupCompleted)
            {
                Shutdown();
                return;
            }
            // Restart app to load new appsettings.json (connection string changed)
            RestartApp();
            return;
        }

        try
        {
            // Auto-create/migrate database on startup
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DatabaseMigrator.EnsureSchemaUpToDate(db);
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_DbMigration", ex);

            if (DbConnectionHelper.IsConnectionError(ex))
            {
                var connStr = LoadConnectionString();
                ParseConnectionString(connStr, out var srv, out var db, out var usr);
                var setupWindow = new ConnectionSetupWindow(srv, db, usr,
                    "Cannot connect to database server. Please verify the connection settings.");
                if (setupWindow.ShowDialog() == true && setupWindow.IsSaved)
                {
                    RestartApp();
                    return;
                }
            }
            else
            {
                CustomMessageBox.Show(
                    $"Database error:\n{ex.Message}\n\nInner: {ex.InnerException?.Message}",
                    "Startup Error", MsgType.Error);
            }

            Shutdown();
            return;
        }

        try
        {
            // Prevent auto-shutdown when LoginWindow closes (it's the only window at that point)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // ── License check: must activate before login ──
            var licenseService = _serviceProvider.GetRequiredService<ILicenseService>();
            var licenseStatus = licenseService.CheckLicense();
            if (!licenseStatus.IsValid)
            {
                var activationWindow = new ActivationWindow(licenseService, licenseStatus);
                if (activationWindow.ShowDialog() != true || !activationWindow.IsActivated)
                {
                    Shutdown();
                    return;
                }
                // Re-check after activation
                licenseStatus = licenseService.CheckLicense();
            }

            // Warn if license expires within 15 days
            if (licenseStatus.IsValid && licenseStatus.DaysRemaining <= 15)
            {
                var devInfo = ILicenseService.LoadDeveloperInfo();
                var contact = !string.IsNullOrWhiteSpace(devInfo.Phone)
                    ? $"\nContact: {devInfo.CompanyName} - {devInfo.Phone}"
                    : "\nContact your software provider.";
                CustomMessageBox.Show(
                    $"License expires in {licenseStatus.DaysRemaining} days ({licenseStatus.ExpiryDate:yyyy-MM-dd}).{contact}",
                    "License Expiring Soon", MsgType.Warning);
            }

            // Show login window — use a scope for proper DbContext lifecycle
            using var loginScope = _serviceProvider.CreateScope();
            var authService = loginScope.ServiceProvider.GetRequiredService<IAuthService>();
            var settingsService = loginScope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var currentUser = _serviceProvider.GetRequiredService<CurrentUserService>();
            var loginWindow = new LoginWindow(
                authService, currentUser, settingsService,
                appName: "HM-GymManagement",
                appIcon: FontAwesome.WPF.FontAwesomeIcon.Shield,
                gradientStart: System.Windows.Media.Color.FromRgb(0x24, 0x7B, 0x7B),
                gradientEnd: System.Windows.Media.Color.FromRgb(0x44, 0xA1, 0xA0));
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Check main app access permission
            if (!currentUser.HasPermission(Domain.Enums.AppPermission.AccessMainApp))
            {
                CustomMessageBox.Show(
                    "Access denied. You do not have permission to use this application.",
                    "Access Denied", MsgType.Warning);
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            // Start hourly auto-cleanup of old events (older than 6 months)
            _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _cleanupTimer.Tick += async (_, _) =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var eventService = scope.ServiceProvider.GetRequiredService<IAccessEventService>();
                    await eventService.CleanupOldEventsAsync();
                }
                catch (Exception ex)
                {
                    WriteCrashLog("CleanupTimer", ex);
                }
            };
            _cleanupTimer.Start();
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_LoginWindow", ex);
            CustomMessageBox.Show(
                $"Login window error:\n{ex.Message}\n\nInner: {ex.InnerException?.Message}",
                "Startup Error", MsgType.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cleanupTimer?.Stop();
        _cleanupTimer = null;

        // Stop SDK monitoring and shutdown to prevent background thread crashes
        try
        {
            var sdk = _serviceProvider.GetService<SDK.Wrapper.IAccessControlSdk>();
            if (sdk != null)
            {
                sdk.StopMonitoring();
                sdk.Shutdown();
            }
        }
        catch { /* ignore shutdown errors */ }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _serviceProvider.Dispose();
        base.OnExit(e);
    }

    private static void ParseConnectionString(string connStr, out string server, out string database, out string userId)
    {
        server = "localhost";
        database = "AccessControlPro";
        userId = "sa";
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
            server = builder.DataSource;
            database = builder.InitialCatalog;
            userId = builder.UserID;
        }
        catch { }
    }

    private static void RestartApp()
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(exePath);
        Environment.Exit(0);
    }

    /// <summary>
    /// Kill any other running instances of this app (stale processes from previous runs).
    /// </summary>
    private static void KillOtherInstances()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id);
            foreach (var p in others)
            {
                try { p.Kill(); } catch { /* ignore if already exiting */ }
            }
        }
        catch { /* best effort */ }
    }
}
