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
    private DispatcherTimer? _expiryMonitorTimer;
    private DispatcherTimer? _backupTimer;
    private DispatcherTimer? _cloudSyncTimer;
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

        // SDK operation helper (singleton — shared lock for all SDK operations)
        services.AddSingleton<Application.Helpers.DeviceOperationHelper>();

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
        services.AddScoped<IExpiryMonitorService, ExpiryMonitorService>();
        services.AddScoped<IQrPassService, QrPassService>();
        services.AddScoped<Domain.Interfaces.IMonitorLockService, AccessControlPro.Infrastructure.Persistence.MonitorLockService>();
        services.AddScoped<ITimeGroupService, TimeGroupService>();
        // POS moved to separate AccessControlPro.POS app

        // Backup
        services.AddSingleton<IBackupService>(sp => new BackupService(connectionString));

        // Cloud sync
        services.AddSingleton<ICloudSyncService>(sp => new CloudSyncService(connectionString));

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<QrPassViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<DoorsViewModel>();
        services.AddTransient<EmployeesViewModel>();
        services.AddTransient<EventsViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DeletedRecordsViewModel>();
        services.AddSingleton<MonitorViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<CashFlowViewModel>();
        // PosViewModel moved to AccessControlPro.POS

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginWindow>();
    }

    private static readonly string StartupLogPath = Path.Combine(AppContext.BaseDirectory, "startup_log.txt");

    private static void StartupLog(string msg)
    {
        try { File.AppendAllText(StartupLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupLog("=== APP STARTING ===");

        // ── Disable WiFi to prevent routing conflicts with SDK ──
        try
        {
            StartupLog("Disabling WiFi...");
            Helpers.WifiManager.DisableWifi();
            StartupLog("WiFi disabled OK");
        }
        catch (Exception ex)
        {
            StartupLog($"WiFi disable failed (non-critical): {ex.Message}");
        }

        // ── Single-instance guard: kill any stale processes from previous runs ──
        StartupLog("Killing old instances...");
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

        StartupLog("Checking setup wizard...");
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
            StartupLog("Migrating database...");
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
            StartupLog("DB migration OK. Loading login...");
            // Prevent auto-shutdown when LoginWindow closes (it's the only window at that point)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load saved language preference (from setup wizard or previous session)
            Helpers.LanguageManager.Instance.LoadSavedLanguage();

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

            bool autoLoggedIn = false;

            // Check for pending operation — auto-login using saved username
            if (PendingOperationHelper.HasPending())
            {
                var pendingOp = PendingOperationHelper.Load();
                if (pendingOp != null && !string.IsNullOrEmpty(pendingOp.Username))
                {
                    StartupLog($"Auto-login for pending operation: {pendingOp.Username}");
                    try
                    {
                        var user = Task.Run(() => authService.GetUserByUsernameAsync(pendingOp.Username)).GetAwaiter().GetResult();
                        if (user != null && user.IsActive)
                        {
                            currentUser.Username = user.Username;
                            currentUser.DisplayName = user.DisplayName;
                            currentUser.Role = user.Role;
                            currentUser.SetPermissions(user.Permissions);
                            autoLoggedIn = true;
                            StartupLog("Auto-login successful");
                        }
                        else
                        {
                            StartupLog("Auto-login failed: user not found or inactive");
                            PendingOperationHelper.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        StartupLog($"Auto-login error: {ex.Message}");
                        PendingOperationHelper.Clear();
                    }
                }
            }

            // Normal login flow if auto-login didn't happen
            if (!autoLoggedIn)
            {
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
            }

            // Check main app access permission
            if (!currentUser.HasPermission(Domain.Enums.AppPermission.AccessMainApp))
            {
                CustomMessageBox.Show(
                    "Access denied. You do not have permission to use this application.",
                    "Access Denied", MsgType.Warning);
                PendingOperationHelper.Clear();
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            // Execute pending operation after restart (fresh SDK session)
            if (PendingOperationHelper.HasPending())
            {
                _ = ExecutePendingOperationAsync();
            }

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

            // Start expiry monitor — checks every 10 seconds for expired players (parallel + lightweight)
            _expiryMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _expiryMonitorTimer.Tick += async (_, _) =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var monitor = scope.ServiceProvider.GetRequiredService<IExpiryMonitorService>();
                    var result = await Task.Run(() => monitor.CheckAndExpireAsync());
                    if (result.DateExpired > 0 || result.VisitExpired > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ExpiryMonitor] Expired: {result.DateExpired} date-based, {result.VisitExpired} visit-based, {result.Errors} errors");
                    }
                }
                catch (Exception ex)
                {
                    WriteCrashLog("ExpiryMonitorTimer", ex);
                }
            };
            _expiryMonitorTimer.Start();

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
                        StartupLog($"AutoBackup{(isRetry ? " (retry)" : "")}: {result}");
                    }
                    catch (Exception ex)
                    {
                        StartupLog($"AutoBackup error: {ex.Message}");
                    }
                }
            };
            _backupTimer.Start();

            // Cloud sync timer — syncs local data to cloud PostgreSQL every 5 minutes
            // Only runs if license tier is Pro or Enterprise
            if (licenseStatus.Tier == "Pro" || licenseStatus.Tier == "Enterprise")
            {
                var cloudSync = new CloudSyncService(LoadConnectionString());
                if (cloudSync.IsCloudEnabled())
                {
                    _cloudSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
                    _cloudSyncTimer.Tick += async (_, _) =>
                    {
                        try
                        {
                            var result = await Task.Run(() => cloudSync.SyncToCloudAsync());
                            StartupLog($"CloudSync: {result}");
                        }
                        catch (Exception ex2)
                        {
                            StartupLog($"CloudSync error: {ex2.Message}");
                        }
                    };
                    _cloudSyncTimer.Start();

                    // Run initial sync after 30 seconds
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(30000);
                        try
                        {
                            var result = await cloudSync.SyncToCloudAsync();
                            StartupLog($"CloudSync (initial): {result}");
                        }
                        catch (Exception ex2)
                        {
                            StartupLog($"CloudSync (initial) error: {ex2.Message}");
                        }
                    });
                }
            }
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

    private async Task ExecutePendingOperationAsync()
    {
        var op = PendingOperationHelper.Load();
        if (op == null) { PendingOperationHelper.Clear(); return; }

        try
        {
            StartupLog($"Executing pending operation: {op.Type} for player {op.PlayerId}");

            // Small delay to let main window fully initialize
            await Task.Delay(2000);

            using var scope = _serviceProvider.CreateScope();
            var employeeService = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

            switch (op.Type)
            {
                case "Renew":
                    await employeeService.RenewSubscriptionAsync(
                        op.PlayerId,
                        op.SubscriptionType ?? "",
                        op.Months,
                        op.CustomDays,
                        op.Fee,
                        op.AmountPaid,
                        op.DoorPermissions ?? "",
                        op.EffectiveTimes,
                        op.DeviceIds);
                    break;

                case "MonitorRestart":
                    // App was restarted to clear SDK state after monitoring — nothing else to do
                    StartupLog("MonitorRestart: SDK state refreshed");
                    break;

                default:
                    StartupLog($"Unknown pending operation type: {op.Type}");
                    break;
            }

            StartupLog($"Pending operation {op.Type} completed successfully");

            // MonitorRestart doesn't need a success dialog — the restart itself was the goal
            if (op.Type != "MonitorRestart")
            {
                Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(
                        $"Card synced to device successfully after restart!\n(Player ID: {op.PlayerId})",
                        op.Type, MsgType.Success,
                        MainWindow);
                });
            }
        }
        catch (Exception ex)
        {
            StartupLog($"Pending operation failed: {ex.Message}");

            var errorMsg = ex.Message;
            if (errorMsg.Contains("PARTIAL_SUCCESS:"))
                errorMsg = errorMsg.Replace("PARTIAL_SUCCESS:", "");

            Dispatcher.Invoke(() =>
            {
                CustomMessageBox.Show(
                    $"Auto-retry after restart failed:\n{errorMsg}",
                    "Error", MsgType.Error,
                    MainWindow);
            });
        }
        finally
        {
            PendingOperationHelper.Clear();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cleanupTimer?.Stop();
        _cleanupTimer = null;
        _expiryMonitorTimer?.Stop();
        _expiryMonitorTimer = null;
        _backupTimer?.Stop();
        _backupTimer = null;
        _cloudSyncTimer?.Stop();
        _cloudSyncTimer = null;

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

        // Kill any lingering CardSync subprocesses
        try
        {
            foreach (var proc in Process.GetProcessesByName("AccessControlPro.CardSync"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }

        // Force close all TCP connections to device port 8000
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface ip delete arpcache",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit(3000);
        }
        catch { }

        // Re-enable WiFi on app exit
        try { Helpers.WifiManager.EnableWifi(); } catch { }

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
