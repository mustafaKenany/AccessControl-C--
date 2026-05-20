using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Infrastructure;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.ViewModels;
using AccessControlPro.WPF.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _showInstanceEvent;
    // Session-local (no Global\ prefix) — keeps things working under non-admin users
    // and per-user Windows sessions; also avoids Terminal Services permission issues.
    private const string SingleInstanceMutexName = "AccessControlPro_SingleInstance";
    private const string ShowInstanceEventName = "AccessControlPro_ShowInstance";
    private readonly ServiceProvider _serviceProvider;
    private DispatcherTimer? _cleanupTimer;
    private DispatcherTimer? _expiryMonitorTimer;
    private DispatcherTimer? _backupTimer;
    private DispatcherTimer? _cloudSyncTimer;
    private DispatcherTimer? _cleanupDailyTimer;
    private DispatcherTimer? _qrPoolTimer;
    private DispatcherTimer? _memoryMonitorTimer;
    private DispatcherTimer? _restartBannerTimer;
    private static readonly DateTime _appStartedAt = DateTime.Now;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
    private static readonly string MemoryLogPath = Path.Combine(AppContext.BaseDirectory, "memory_log.txt");

    // === Native debug-dialog suppression =================================================
    // The Hikvision/Dnake SDK (FCardCDrive.dll and friends) is built against the DEBUG
    // MFC runtime. Whenever the SDK re-initializes (e.g., closing/reopening Monitor),
    // its internal asserts fire and pop up "Microsoft Visual C++ Debug Library" dialogs.
    // We can't fix the vendor's DLL, but we can silence the popups so they don't
    // interrupt the user.
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint uMode);
    [DllImport("ucrtbased.dll", EntryPoint = "_CrtSetReportMode")]
    private static extern int _CrtSetReportMode_Debug(int reportType, int reportMode);
    private const int _CRT_WARN = 0;
    private const int _CRT_ERROR = 1;
    private const int _CRT_ASSERT = 2;
    private const int _CRTDBG_MODE_DEBUG = 0x2; // Send to OutputDebugString only — no UI dialog

    private static void SuppressNativeDebugDialogs()
    {
        // Windows-level: suppress critical-error / GP-fault popups
        SetErrorMode(0x0001 /*SEM_FAILCRITICALERRORS*/ | 0x0002 /*SEM_NOGPFAULTERRORBOX*/ | 0x8000 /*SEM_NOOPENFILEERRORBOX*/);

        // CRT-level: route MFC asserts to debug output instead of a dialog.
        // Wrapped in try/catch because ucrtbased.dll only exists on machines that have
        // the debug Universal CRT installed; on plain customer machines it's absent
        // and we don't need it (no debug DLL = no debug asserts).
        try
        {
            _CrtSetReportMode_Debug(_CRT_ASSERT, _CRTDBG_MODE_DEBUG);
            _CrtSetReportMode_Debug(_CRT_ERROR, _CRTDBG_MODE_DEBUG);
            _CrtSetReportMode_Debug(_CRT_WARN, _CRTDBG_MODE_DEBUG);
        }
        catch { /* ucrtbased not present — fine, just no native debug dialogs anyway */ }
    }

    // FCardCDrive SDK writes its own debug log file (ocsm, ocsm_1..ocsm_9 — 10-file rotation
    // by the vendor DLL). The contents are CP936-mojibake socket noise we can't read, can't
    // disable via any C# API, and never need. We delete on startup so they don't accumulate
    // forever; the SDK may recreate them during the session but they'll be cleared again next
    // launch. Failures are non-fatal: a locked file just stays until the next clean restart.
    private static void CleanupSdkDebugLogs()
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "ocsm*"))
            {
                try { File.Delete(path); }
                catch { /* file in use by another instance — leave it */ }
            }
        }
        catch { /* enumerate failed — base dir gone or unreadable, nothing we can do */ }
    }

    public App()
    {
        // Run BEFORE the SDK loads, so its initialization can't pop debug dialogs
        SuppressNativeDebugDialogs();
        CleanupSdkDebugLogs();

        // Global exception handlers — write crash log before app dies
        DispatcherUnhandledException += (s, e) =>
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // Prevent crash — show message instead

            // OutOfMemoryException special-case: do NOT show a CustomMessageBox.
            // The previous code did, and on the Basmia 2026-05-18 crash storm the
            // message box's own render call triggered another OOM, opening another
            // (invisible) message box, opening another... within 35 ms we had 10
            // cascading OOM exceptions with 10 invisible windows piling up. The user
            // saw a frozen window with no error message. By skipping UI on OOM we
            // log the crash and let background tasks (sync, backup) keep running.
            // The next "Restart Recommended" banner check will tell the user to
            // close + reopen the app.
            if (e.Exception is OutOfMemoryException)
            {
                try { GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true); } catch { }
                return;
            }

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
        => CrashContextLogger.Write(CrashLogPath, source, ex);

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

    private static string? LoadCloudConnectionString()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);

            // Check CloudSyncUrl (new API-based sync)
            if (doc.RootElement.TryGetProperty("CloudSyncUrl", out var syncUrl))
            {
                var val = syncUrl.GetString();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Check old CloudConnection (direct PostgreSQL - deprecated)
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                cs.TryGetProperty("CloudConnection", out var conn))
            {
                var val = conn.GetString();
                if (!string.IsNullOrEmpty(val) && !val.Contains("xxxx"))
                    return val;
            }
        }
        catch { }
        return null;
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
        services.AddSingleton<IQrPoolService>(sp =>
        {
            var qrPoolSize = 3500;
            var qrRangeStart = 50001001;
            try
            {
                var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("QrPoolSize", out var sizeEl))
                        qrPoolSize = sizeEl.GetInt32();
                    if (doc.RootElement.TryGetProperty("QrRangeStart", out var startEl))
                        qrRangeStart = startEl.GetInt32();
                }
            }
            catch { /* use defaults */ }
            return new QrPoolService(connectionString, qrPoolSize, qrRangeStart);
        });
        services.AddScoped<Domain.Interfaces.IMonitorLockService, AccessControlPro.Infrastructure.Persistence.MonitorLockService>();
        services.AddScoped<ITimeGroupService, TimeGroupService>();
        // POS moved to separate AccessControlPro.POS app

        // Backup
        services.AddSingleton<IBackupService>(sp => new BackupService(connectionString));

        // Cloud sync
        services.AddSingleton<ICloudSyncService>(sp => new CloudSyncService(connectionString));

        // Diagnostics uploader — manual button + 15-day auto uploader
        services.AddSingleton<IDiagnosticsService>(sp => new DiagnosticsService(connectionString));

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
        AccessControlPro.Application.Services.RollingLogFile.Append(
            StartupLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupLog("=== APP STARTING ===");

        // Diagnose how the previous session ended (clean exit / system shutdown /
        // killed-or-crashed) so silent restarts in the log become explainable.
        StartupLog(LastRunStateTracker.ReadPreviousAndRecordStartup());

        // Catch Windows logoff / shutdown so we can distinguish them from kills.
        SessionEnding += (_, args) =>
            LastRunStateTracker.RecordSystemShutdown(args.ReasonSessionEnding.ToString());

        // ── Disable WiFi only if cloud sync is NOT enabled ──
        // If cloud sync is enabled, WiFi is needed for internet access
        var cloudConn = LoadCloudConnectionString();
        if (string.IsNullOrEmpty(cloudConn))
        {
            try
            {
                StartupLog("Disabling WiFi (no cloud sync)...");
                Helpers.WifiManager.DisableWifi();
                StartupLog("WiFi disabled OK");
            }
            catch (Exception ex)
            {
                StartupLog($"WiFi disable failed (non-critical): {ex.Message}");
            }
        }
        else
        {
            StartupLog("WiFi kept enabled (cloud sync configured)");
        }

        // ── Single-instance guard ──
        // If the app is already running, we DO NOT kill the existing process.
        // Instead we signal it to bring its window to the front, then exit cleanly.
        // This fixes the "113 restarts in 5 days" behavior where every accidental
        // double-click of the app icon killed the old instance and started a new one.
        StartupLog("Checking for existing instance...");

        bool isNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out isNew);
        }
        catch (AbandonedMutexException)
        {
            // Previous process died without releasing — we inherit ownership
            isNew = true;
        }

        if (!isNew)
        {
            // Another instance is alive — signal it to come to the foreground
            try
            {
                var showSignal = EventWaitHandle.OpenExisting(ShowInstanceEventName);
                showSignal.Set();
                StartupLog("Existing instance signaled to come to front.");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Very old existing instance that doesn't know about this event.
                // Tell the user; they can find it in the taskbar.
                StartupLog("Existing instance found but cannot be signaled (older build).");
            }
            catch (Exception ex)
            {
                StartupLog($"Signal to existing instance failed: {ex.Message}");
            }

            Shutdown();
            Environment.Exit(0);
            return;
        }

        // We're the primary instance. Create the signal event and start a background
        // listener thread so future launches can tell us to come to the front.
        _showInstanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowInstanceEventName);
        var signalListener = new Thread(ListenForShowSignal)
        {
            IsBackground = true,
            Name = "SingleInstanceSignalListener"
        };
        signalListener.Start();

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
            // Auto-create/migrate database on startup (skip if already at current version)
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var migrationMarker = Path.Combine(AppContext.BaseDirectory, ".migration_v");
                var currentVersion = "4.4";
                if (File.Exists(migrationMarker) && File.ReadAllText(migrationMarker).Trim() == currentVersion)
                {
                    StartupLog("DB migration skipped (already at v" + currentVersion + ")");
                }
                else
                {
                    DatabaseMigrator.EnsureSchemaUpToDate(db);
                    File.WriteAllText(migrationMarker, currentVersion);
                    StartupLog("DB migration completed to v" + currentVersion);
                }
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

            // Force-clear any stale monitor locks from this machine (after crash/kill/restart)
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var lockService = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IMonitorLockService>();
                Task.Run(() => lockService.ReleaseAsync()).Wait(3000);
                StartupLog("Monitor lock cleared on startup");
            }
            catch (Exception ex) { StartupLog($"Monitor lock cleanup failed (non-critical): {ex.Message}"); }

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

            // Check for pending device operations (shows notification bar after 3s)
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                try
                {
                    var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
                    await mainVm.CheckPendingDeviceOperationsAsync();
                }
                catch { }
            });

            // QR Pool: generate and upload in background (non-blocking)
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Wait 5 seconds for app to fully initialize
                try
                {
                    using var qrScope = _serviceProvider.CreateScope();
                    var qrPool = qrScope.ServiceProvider.GetRequiredService<IQrPoolService>();
                    var available = await qrPool.GetAvailableCountAsync();
                    int generated = 0;
                    if (available == 0)
                    {
                        StartupLog("Generating initial QR pool (3500 local codes)...");
                        generated = await qrPool.GeneratePoolAsync(3500, 50001001, "Local");
                        StartupLog($"QR pool generated: {generated} codes");
                    }
                    else if (available < 500)
                    {
                        StartupLog($"QR pool low ({available} available)");
                    }

                    // Always check for un-uploaded QR codes and upload them
                    // This covers: first run, missed 1st/15th, newly generated codes
                    var pendingCount = await qrPool.GetPendingUploadCountAsync();
                    if (pendingCount > 0)
                    {
                        StartupLog($"QR Pool: {pendingCount} codes pending upload to device...");
                        try
                        {
                            var deviceRepo = qrScope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                            var allDevices = (await deviceRepo.GetAllAsync()).ToList();
                            if (allDevices.Count > 0)
                            {
                                var sdk = _serviceProvider.GetRequiredService<IAccessControlSdk>();
                                var deviceInfos = allDevices.Select(d => new DeviceInfo
                                {
                                    IP = d.IP, MAC = d.MAC, SerialNumber = d.SerialNumber,
                                    TCPPort = d.TCPPort, Password = d.Password,
                                    Gateway = d.Gateway, SubnetMask = d.SubnetMask
                                }).ToList();
                                var (uploaded, cleaned, regen) = await qrPool.SyncQrPoolToDeviceAsync(sdk, deviceInfos);
                                StartupLog($"QR Pool upload: {uploaded} uploaded, {cleaned} cleaned, {regen} regenerated to {allDevices.Count} device(s)");
                            }
                            else
                            {
                                StartupLog("QR Pool: no devices registered, skipping upload");
                            }
                        }
                        catch (Exception ex) { StartupLog($"QR Pool upload error: {ex.Message}"); }
                    }
                    else
                    {
                        StartupLog("QR Pool: all codes already uploaded to device");
                    }
                }
                catch (Exception qrEx)
                {
                    StartupLog($"QR pool error (non-critical): {qrEx.Message}");
                }
            });

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
                    StartupLog($"DailyCleanup: {players} inactive players, {events} old events removed");
                }
                catch (Exception ex)
                {
                    StartupLog($"DailyCleanup error: {ex.Message}");
                }
            };
            _cleanupDailyTimer.Start();

            // Run initial cleanup after 2 minutes (let app fully initialize first)
            _ = Task.Run(async () =>
            {
                await Task.Delay(120000); // 2 minutes after startup
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
                    var players = await cleanup.CleanupInactivePlayersAsync();
                    var events = await cleanup.CleanupOldEventsAsync();
                    StartupLog($"DailyCleanup (initial): {players} inactive players, {events} old events removed");
                }
                catch (Exception ex)
                {
                    StartupLog($"DailyCleanup (initial) error: {ex.Message}");
                }
            });

            // Cloud sync timer — syncs local data to cloud PostgreSQL every 5 minutes
            // Runs if CloudConnection is configured (license check bypassed for now)
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

            // Diagnostics — two triggers run independently on every startup:
            //   1) Crash-recovery (immediate): if LastRunStateTracker says the previous
            //      session died without OnExit, fire an upload right away so support
            //      gets the bundle before the user does anything that overwrites logs.
            //   2) Auto-due (~2 min in): no-op unless 15+ days have passed since the
            //      last successful upload.
            _ = Task.Run(async () =>
            {
                try
                {
                    var diag = new DiagnosticsService(LoadConnectionString());

                    // Wait a short beat so SQL Server is reachable before we snapshot it.
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    var crashResult = await diag.UploadIfPreviousRunCrashedAsync(
                        Helpers.LastRunStateTracker.WasPreviousRunACrash);
                    StartupLog($"Diagnostics crash-check: success={crashResult.Success}, msg={crashResult.Message}");

                    // Auto-due check a couple of minutes in — independent of crash-recovery.
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    var autoResult = await diag.UploadIfDueAsync();
                    StartupLog($"Diagnostics auto: success={autoResult.Success}, msg={autoResult.Message}");
                }
                catch (Exception ex2)
                {
                    StartupLog($"Diagnostics auto error: {ex2.Message}");
                }
            });

            // Memory pressure monitor — logs working set + heap stats every 10 min to
            // memory_log.txt. Picked up by the diagnostics bundler. After the Basmia
            // OOM crashes on 2026-05-18 we want clear breadcrumbs showing memory growth
            // BEFORE the next OOM, not just at the crash moment. Logs are tiny (~80 bytes
            // per sample) so 60-day rolling retention costs <100 KB total.
            _memoryMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
            _memoryMonitorTimer.Tick += (_, _) =>
            {
                try
                {
                    var proc = Process.GetCurrentProcess();
                    var ws = proc.WorkingSet64 / (1024 * 1024);
                    var priv = proc.PrivateMemorySize64 / (1024 * 1024);
                    var heap = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
                    var uptime = DateTime.Now - _appStartedAt;

                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [info] " +
                               $"ws={ws}MB private={priv}MB heap={heap}MB " +
                               $"gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)} " +
                               $"uptime={uptime.TotalHours:F1}h\n";
                    RollingLogFile.Append(MemoryLogPath, line);

                    // Warning thresholds: at >500 MB working set we shout, at >700 MB we
                    // proactively trigger a Gen2 compacting GC and log a critical entry.
                    if (ws > 700)
                    {
                        RollingLogFile.Append(MemoryLogPath,
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [error] working set high ({ws} MB) — forcing Gen2 compacting GC\n");
                        GC.Collect(2, GCCollectionMode.Aggressive, blocking: false, compacting: true);
                    }
                    else if (ws > 500)
                    {
                        RollingLogFile.Append(MemoryLogPath,
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [warn] working set elevated ({ws} MB) — consider closing/reopening the app today\n");
                    }
                }
                catch (Exception ex2)
                {
                    try { RollingLogFile.Append(MemoryLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [error] monitor failed: {ex2.Message}\n"); } catch { }
                }
            };
            _memoryMonitorTimer.Start();
            // Fire one sample on startup so we have a baseline reading
            _memoryMonitorTimer.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var proc = Process.GetCurrentProcess();
                    var ws = proc.WorkingSet64 / (1024 * 1024);
                    RollingLogFile.Append(MemoryLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [info] === STARTUP === ws={ws}MB pid={proc.Id}\n");
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);

            // Restart-recommended reminder. Both leaks (WPF Visual tree + native SDK)
            // are usage-driven but accumulate over time. At 5+ days uptime the customer
            // should close+reopen the app before the next OOM. Soft-prompts once on
            // crossing 5d, then once every 12h after. Easy to dismiss — never blocks work.
            bool restartShownAlready = false;
            DateTime lastRestartPrompt = DateTime.MinValue;
            _restartBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _restartBannerTimer.Tick += (_, _) =>
            {
                try
                {
                    var uptime = DateTime.Now - _appStartedAt;
                    if (uptime.TotalDays < 5) return;

                    // First crossing of 5 days, OR 12+ hours since last reminder.
                    if (!restartShownAlready || (DateTime.Now - lastRestartPrompt).TotalHours >= 12)
                    {
                        restartShownAlready = true;
                        lastRestartPrompt = DateTime.Now;
                        StartupLog($"Restart reminder fired (uptime={uptime.TotalDays:F1}d)");

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var msg = LanguageManager.Instance.IsArabic
                                    ? $"البرنامج يعمل منذ {(int)uptime.TotalDays} أيام. للحصول على أداء أفضل يُنصح بإغلاق البرنامج ثم فتحه من جديد عندما يناسب ذلك."
                                    : $"The app has been running for {(int)uptime.TotalDays} days. For best performance, close and reopen it when convenient.";
                                CustomMessageBox.Show(msg, "AccessControlPro", MsgType.Info, MainWindow);
                            }
                            catch { /* MainWindow might be null during shutdown */ }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                catch (Exception ex2)
                {
                    StartupLog($"Restart banner check failed: {ex2.Message}");
                }
            };
            _restartBannerTimer.Start();

            // QR Pool device sync timer — checks every 12 hours, runs on 1st and 15th of each month
            _qrPoolTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(12) };
            _qrPoolTimer.Tick += async (_, _) =>
            {
                var day = DateTime.Now.Day;
                if (day == 1 || day == 15)
                {
                    // Check if already ran today
                    var markerPath = Path.Combine(AppContext.BaseDirectory, ".qr_pool_sync");
                    var lastRun = File.Exists(markerPath) ? File.ReadAllText(markerPath).Trim() : "";
                    var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
                    if (lastRun == todayKey) return;

                    try
                    {
                        File.WriteAllText(markerPath, todayKey);
                        StartupLog("QR Pool sync: starting device upload...");

                        // Show warning to user
                        Dispatcher.Invoke(() =>
                        {
                            CustomMessageBox.Show(
                                LanguageManager.Instance.IsArabic
                                    ? "جاري مزامنة رموز QR مع الجهاز... يرجى الانتظار"
                                    : "Syncing QR codes to device... Please wait",
                                "QR Sync",
                                MsgType.Info,
                                MainWindow);
                        });

                        using var scope = _serviceProvider.CreateScope();
                        var qrPool = scope.ServiceProvider.GetRequiredService<IQrPoolService>();
                        var sdk = _serviceProvider.GetRequiredService<IAccessControlSdk>();
                        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

                        var allDevices = (await deviceRepo.GetAllAsync()).ToList();
                        var deviceInfos = allDevices.Select(d => new DeviceInfo
                        {
                            IP = d.IP, MAC = d.MAC, SerialNumber = d.SerialNumber,
                            TCPPort = d.TCPPort, Password = d.Password,
                            Gateway = d.Gateway, SubnetMask = d.SubnetMask
                        }).ToList();

                        var (uploaded, deleted, generated) = await Task.Run(() =>
                            qrPool.SyncQrPoolToDeviceAsync(sdk, deviceInfos));

                        StartupLog($"QR Pool sync complete: {uploaded} uploaded, {deleted} cleaned, {generated} generated");
                    }
                    catch (Exception ex)
                    {
                        StartupLog($"QR Pool sync error: {ex.Message}");
                    }
                }
            };
            _qrPoolTimer.Start();
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
        StartupLog($"=== APP EXITING (code={e.ApplicationExitCode}) ===");

        // Record clean exit FIRST — if anything below crashes during teardown, at least
        // we still know we got to OnExit (i.e. it wasn't a force-kill).
        LastRunStateTracker.RecordCleanExit();
        _cleanupTimer?.Stop();
        _cleanupTimer = null;
        _expiryMonitorTimer?.Stop();
        _expiryMonitorTimer = null;
        _backupTimer?.Stop();
        _backupTimer = null;
        _cloudSyncTimer?.Stop();
        _cloudSyncTimer = null;
        _cleanupDailyTimer?.Stop();
        _cleanupDailyTimer = null;
        _qrPoolTimer?.Stop();
        _qrPoolTimer = null;

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

        // Stop the signal-listener thread by disposing the event it waits on
        try { _showInstanceEvent?.Dispose(); } catch { }
        _showInstanceEvent = null;

        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();

        // Dispose services safely
        try { _serviceProvider.Dispose(); } catch { }

        // Force exit after 3 seconds (kills any lingering SDK/background threads)
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            Environment.Exit(0);
        });

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

    /// <summary>
    /// Releases the single-instance mutex so a new process can acquire it.
    /// Called before restarting the app (e.g., after stopping monitoring).
    /// </summary>
    public static void ReleaseSingleInstanceMutex()
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
        catch { /* ignore — mutex may already be released */ }
    }

    private static void RestartApp()
    {
        ReleaseSingleInstanceMutex();
        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(exePath);
        Environment.Exit(0);
    }

    /// <summary>
    /// Background thread: waits for another launch of the app to signal
    /// that we should come to the foreground, then brings MainWindow to front.
    /// </summary>
    private void ListenForShowSignal()
    {
        while (_showInstanceEvent != null)
        {
            try
            {
                // WaitOne(timeout) so the thread can exit cleanly if the app shuts down
                if (!_showInstanceEvent.WaitOne(1000)) continue;

                Dispatcher.Invoke(BringMainWindowToFront);
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex)
            {
                try { StartupLog($"Show-signal listener error: {ex.Message}"); } catch { }
            }
        }
    }

    private void BringMainWindowToFront()
    {
        var window = MainWindow;
        if (window == null) return;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
        // Topmost flash forces focus even when another app is in the foreground.
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
