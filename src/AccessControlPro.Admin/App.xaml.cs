using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Infrastructure;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using AccessControlPro.Admin.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.Admin;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private readonly ServiceProvider _serviceProvider;
    private DispatcherTimer? _backupTimer;
    private DispatcherTimer? _cleanupDailyTimer;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "admin_crash_log.txt");

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            e.Handled = true;

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
            e.SetObserved();
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
                      $"Stack:\n{ex?.StackTrace}\n";
            File.AppendAllText(CrashLogPath, msg);
        }
        catch { }
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

        services.AddInfrastructure(connectionString);

        services.AddSingleton<CurrentUserService>();
        services.AddSingleton<ILicenseService, LicenseService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAppSettingsService, AppSettingsService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ITimeGroupService, TimeGroupService>();

        // Backup
        services.AddSingleton<IBackupService>(sp => new BackupService(connectionString));

        // QR Pool
        services.AddSingleton<IQrPoolService>(sp => new QrPoolService(connectionString));

        services.AddTransient<AdminMainViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<PurchaseOrdersViewModel>();
        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<TimeGroupViewModel>();

        services.AddTransient<QrPoolViewModel>(sp =>
            new QrPoolViewModel(
                sp.GetRequiredService<IQrPoolService>(),
                connectionString));
        services.AddTransient<SubscriptionPlansViewModel>(sp =>
            new SubscriptionPlansViewModel(connectionString));

        services.AddTransient<AdminMainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "AccessControlPro_Admin_SingleInstance", out bool isNew);
        if (!isNew)
        {
            CustomMessageBox.Show(
                "Admin Panel is already running.",
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
            RestartApp();
            return;
        }

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DatabaseMigrator.EnsureSchemaUpToDate(db);
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_Db", ex);

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
                    $"Database error:\n{ex.Message}",
                    "Startup Error", MsgType.Error);
            }

            Shutdown();
            return;
        }

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // ── License check ──
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
                licenseStatus = licenseService.CheckLicense();
            }

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

            using var loginScope = _serviceProvider.CreateScope();
            var authService = loginScope.ServiceProvider.GetRequiredService<IAuthService>();
            var settingsService = loginScope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var currentUser = _serviceProvider.GetRequiredService<CurrentUserService>();
            var loginWindow = new LoginWindow(
                authService, currentUser, settingsService,
                appName: "HM-GymManagement Admin",
                appIcon: FontAwesome.WPF.FontAwesomeIcon.Cogs,
                gradientStart: System.Windows.Media.Color.FromRgb(0x8B, 0x45, 0x13),
                gradientEnd: System.Windows.Media.Color.FromRgb(0xD2, 0x69, 0x1E));
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Check admin access permission
            if (!currentUser.HasPermission(AccessControlPro.Domain.Enums.AppPermission.AccessAdmin))
            {
                CustomMessageBox.Show(
                    "Access denied. You do not have permission to use the Admin Panel.",
                    "Access Denied", MsgType.Warning);
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<AdminMainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            // Automated backup timer — checks every 30 minutes, runs at 2:00 AM and 2:00 PM, retries on failure
            _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _backupTimer.Tick += async (_, _) =>
            {
                var now = DateTime.Now;
                var hour = now.Hour;
                var minute = now.Minute;

                var status = BackupService.LoadStatus();

                // Normal schedule: 2AM or 2PM
                bool isScheduledTime = (hour == 2 || hour == 14) && minute < 30;

                // Retry: if last backup failed and less than 3 consecutive failures
                bool isRetry = status.ConsecutiveFailures > 0 && status.ConsecutiveFailures < 3
                    && status.LastAttempt.HasValue
                    && (DateTime.Now - status.LastAttempt.Value).TotalMinutes >= 30;

                if (isScheduledTime || isRetry)
                {
                    // Check if already ran this hour for scheduled runs (prevent duplicate runs)
                    if (isScheduledTime && !isRetry)
                    {
                        var markerPath = Path.Combine(AppContext.BaseDirectory, ".last_backup");
                        var lastRun = File.Exists(markerPath) ? File.ReadAllText(markerPath).Trim() : "";
                        var currentKey = $"{now:yyyy-MM-dd-HH}";
                        if (lastRun == currentKey) return; // Already ran this hour
                        File.WriteAllText(markerPath, currentKey);
                    }

                    try
                    {
                        var backupService = _serviceProvider.GetRequiredService<IBackupService>();
                        var result = await Task.Run(() => backupService.RunBackupAsync());
                        AdminStartupLog($"AutoBackup{(isRetry ? " (retry)" : "")}: {result}");
                    }
                    catch (Exception ex)
                    {
                        AdminStartupLog($"AutoBackup error: {ex.Message}");
                    }
                }
            };
            _backupTimer.Start();

            // Daily cleanup timer — archives inactive players (6+ months expired) and deletes old events
            _cleanupDailyTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
            _cleanupDailyTimer.Tick += async (_, _) =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
                    var players = await Task.Run(() => cleanup.CleanupInactivePlayersAsync());
                    var events = await Task.Run(() => cleanup.CleanupOldEventsAsync());
                    AdminStartupLog($"DailyCleanup: {players} inactive players, {events} old events removed");
                }
                catch (Exception ex)
                {
                    AdminStartupLog($"DailyCleanup error: {ex.Message}");
                }
            };
            _cleanupDailyTimer.Start();

            // Run initial cleanup after 2 minutes
            _ = Task.Run(async () =>
            {
                await Task.Delay(120000);
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
                    var players = await cleanup.CleanupInactivePlayersAsync();
                    var events = await cleanup.CleanupOldEventsAsync();
                    AdminStartupLog($"DailyCleanup (initial): {players} inactive players, {events} old events removed");
                }
                catch (Exception ex)
                {
                    AdminStartupLog($"DailyCleanup (initial) error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup_Login", ex);
            CustomMessageBox.Show(
                $"Login error:\n{ex.Message}",
                "Startup Error", MsgType.Error);
            Shutdown();
        }
    }

    private static readonly string AdminStartupLogPath = Path.Combine(AppContext.BaseDirectory, "admin_startup_log.txt");

    private static void AdminStartupLog(string msg)
    {
        try { File.AppendAllText(AdminStartupLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); }
        catch { }
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
            System.Diagnostics.Process.Start(exePath);
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _backupTimer?.Stop();
        _backupTimer = null;
        _cleanupDailyTimer?.Stop();
        _cleanupDailyTimer = null;
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
