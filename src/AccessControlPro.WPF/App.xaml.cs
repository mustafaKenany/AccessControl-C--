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
    private Views.LockWindow? _lockWindow;
    private string? _pendingLockWarning;
    private DispatcherTimer? _cleanupDailyTimer;
    private DispatcherTimer? _qrPoolTimer;
    private DispatcherTimer? _memoryMonitorTimer;
    private DispatcherTimer? _restartBannerTimer;
    private DispatcherTimer? _deviceWatchdogTimer;
    private DispatcherTimer? _scheduledRestartTimer;
    // Tracks last-known reachability per device IP so the watchdog only alerts on a state change.
    private readonly Dictionary<string, bool> _deviceReachable = new();
    private static readonly DateTime _appStartedAt = DateTime.Now;
    // Latest working-set sample (MB) from the memory monitor. Read by the restart-nudge
    // timer so it can prompt a graceful restart when memory creeps high — before the
    // receptionist force-kills a "heavy" app (the observed Basmia pattern).
    private static volatile int _lastWorkingSetMb;
    // OOM auto-recovery: count OutOfMemoryExceptions this session; after a few, restart cleanly
    // at the next safe moment (no dialog open) instead of limping toward a hard kill.
    private static int _oomCount;
    // Scheduled daily app-restart slots already fired today (keyed "yyyy-MM-dd:HH:mm") so each
    // slot fires at most once/day even though the watchdog ticks every minute.
    private static readonly HashSet<string> _restartSlotsDone = new();
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
    private static readonly string MemoryLogPath = Path.Combine(AppContext.BaseDirectory, "memory_log.txt");
    // Managed-heap histogram log (the file name contains "log" so the diagnostics bundler picks it
    // up automatically). Captured when memory is elevated so a bundle shows WHAT is leaking.
    private static readonly string HeapLogPath = Path.Combine(AppContext.BaseDirectory, "heap_log.txt");
    private static DateTime _lastHeapCaptureAt = DateTime.MinValue;
    private static int _heapCaptureRunning; // 0/1 guard so two captures never overlap

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
                // Self-heal: after a few OOMs in one session the process is doomed to keep dying —
                // restart cleanly at the next safe moment (no dialog open) instead of limping into
                // a hard kill. The nightly scheduled restart also resets memory; this is the
                // fast-path when the leak fills memory within a single session.
                _oomCount++;
                try
                {
                    RollingLogFile.Append(MemoryLogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [error] OutOfMemoryException #{_oomCount} (ws~{_lastWorkingSetMb}MB)\n");
                }
                catch { }
                if (_oomCount >= 3 && IsSafeToRestart())
                {
                    try { RollingLogFile.Append(MemoryLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [error] {_oomCount} OOMs — auto-restarting to reset memory\n"); } catch { }
                    RestartApp("OOM auto-recovery");
                }
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

        // Auto-update — checks VPS on launch, surfaces prompt, downloads + verifies + stages
        services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
        services.AddSingleton<IUpdateInstallerService>(sp =>
            new UpdateInstallerService(sp.GetRequiredService<IBackupService>()));

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<QrPassViewModel>();
        services.AddTransient<RemindersViewModel>();
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
            // ALWAYS run the migrator. It is idempotent (every statement is guarded by an
            // IF NOT EXISTS) and fast, so it is safe to run on every startup — and it MUST,
            // so that columns added in later releases reach EXISTING installs. A previous
            // ".migration_v" marker pinned to "4.4" permanently skipped the migrator after the
            // first run, so any newer column (e.g. Discount) never got added to old databases →
            // "Invalid column name" errors. Never gate incremental schema migrations behind a
            // one-time marker again. (Admin & POS already call it unconditionally.)
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DatabaseMigrator.EnsureSchemaUpToDate(db);
                StartupLog("DB migration ensured");
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

            // Startup backup safety net: if last backup is stale (>12h), run silent backup
            // before showing the login window. Catches the case where the customer turns the
            // PC off at night and the 02:00 AM scheduled backup never fires.
            try { RunStartupBackupIfStale(); }
            catch (Exception ex) { StartupLog($"Startup backup failed (non-critical): {ex.Message}"); }

            // Post-update "What's new" dialog: if Updater.exe left a .post_update marker,
            // show the release notes once then delete the marker. Done BEFORE the update
            // check so we don't show "What's new" and "Update available" simultaneously.
            try { ShowPostUpdateNotesIfAny(); }
            catch (Exception ex) { StartupLog($"Post-update notes failed (non-critical): {ex.Message}"); }

            // Auto-update check: fire-and-forget HTTP GET to /api/version/latest. If a newer
            // build is available and the customer hasn't snoozed/skipped this version, show
            // a non-blocking prompt after login. Fully non-fatal — login proceeds either way.
            // Skipped entirely on offline / local-only installs (no cloud calls).
            if (CloudSyncService.IsCloudSyncEnabledFlag())
            {
                _ = Task.Run(async () =>
                {
                    try { await CheckForUpdateAndPromptAsync(); }
                    catch (Exception ex) { StartupLog($"Update check failed (non-critical): {ex.Message}"); }
                });
            }

            // Prevent auto-shutdown when LoginWindow closes (it's the only window at that point).
            // Guard: if the app is already shutting down (a near-simultaneous second launch, or a
            // Windows session-end during the ~10s startup), setting ShutdownMode throws
            // InvalidOperationException — which was being logged as a crash and triggering a
            // spurious crash-recovery bundle. Abort startup cleanly instead.
            try { ShutdownMode = ShutdownMode.OnExplicitShutdown; }
            catch (InvalidOperationException)
            {
                StartupLog("App already shutting down during startup — aborting cleanly.");
                return;
            }

            // Load saved language preference (from setup wizard or previous session)
            Helpers.LanguageManager.Instance.LoadSavedLanguage();

            // ── License check: must activate before login ──
            var licenseService = _serviceProvider.GetRequiredService<ILicenseService>();
            var licenseStatus = licenseService.CheckLicense();
            if (!licenseStatus.IsValid)
            {
                // Make an EXPIRED license explicit (contact + renewal) before the key-entry window.
                if (licenseStatus.Message == "EXPIRED")
                {
                    var dev = ILicenseService.LoadDeveloperInfo();
                    var who = !string.IsNullOrWhiteSpace(dev.Phone)
                        ? $"{dev.CompanyName} — {dev.Phone}" : "your software provider";
                    CustomMessageBox.Show(
                        $"انتهت صلاحية ترخيص البرنامج. يرجى التواصل مع {who} للحصول على رمز تجديد.\n\n" +
                        $"Your license has expired. Please contact {who} for a renewal code.",
                        "License expired", MsgType.Warning);
                }
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

            // Cache the gym identity (name/phone) for printed receipts, renewal slips
            // and member ID cards. Read by the print dialogs via Helpers.GymProfile.
            try
            {
                var gymSettings = Task.Run(() => settingsService.GetSettingsAsync()).GetAwaiter().GetResult();
                Helpers.GymProfile.Update(gymSettings);

                // Back-fill the cloud key into the DB (once) so a future reinstall — which keeps
                // the database — recovers the SAME key instead of generating a new one the cloud
                // rejects. Only writes when the DB has no key yet.
                if (gymSettings != null && string.IsNullOrWhiteSpace(gymSettings.CloudApiKey))
                {
                    var fileKey = "";
                    try
                    {
                        var p = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                        if (System.IO.File.Exists(p))
                        {
                            using var d = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(p));
                            if (d.RootElement.TryGetProperty("CloudApiKey", out var k)) fileKey = k.GetString() ?? "";
                        }
                    }
                    catch { }

                    if (!string.IsNullOrWhiteSpace(fileKey))
                    {
                        gymSettings.CloudApiKey = fileKey;
                        Task.Run(() => settingsService.SaveSettingsAsync(gymSettings)).GetAwaiter().GetResult();
                        StartupLog("Backfilled CloudApiKey into AppSettings (reinstall recovery).");
                    }
                }
            }
            catch (Exception ex) { StartupLog($"GymProfile load skipped: {ex.Message}"); }

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

            // Remote payment lock — if this gym has been locked from the cloud, block the app
            // before the main window. EvaluateAsync runs on a worker thread (no UI deadlock) and
            // applies the 7-day offline grace. The LockWindow re-checks every 60s and closes
            // itself once unlocked. It only blocks usage — it never touches the customer's data.
            // CRITICAL: only applies to ONLINE installs. An offline-mode install (cloud disabled at
            // setup) intentionally never reaches the cloud, so the online-verification grace must NOT
            // catch it — otherwise it self-locks after 7 days with no way to verify (the Demo trap).
            if (AccessControlPro.Application.Services.CloudSyncService.IsCloudSyncEnabledFlag())
            {
                try
                {
                    var lockResult = Task.Run(async () => await Helpers.RemoteLockService.EvaluateAsync())
                                         .GetAwaiter().GetResult();
                    if (lockResult.Locked)
                    {
                        StartupLog("Remote lock active — showing lock screen.");
                        new Views.LockWindow(lockResult.Message).ShowDialog();
                    }
                    else if (!string.IsNullOrWhiteSpace(lockResult.Warning))
                    {
                        // Non-blocking: nudge them to reconnect before the 30-day grace runs out.
                        StartupLog("Subscription not verified online — showing reconnect warning.");
                        _pendingLockWarning = lockResult.Warning;
                    }
                }
                catch (Exception exLock) { StartupLog($"Remote lock check failed (non-critical): {exLock.Message}"); }
            }
            else
            {
                StartupLog("Offline install — remote lock check skipped.");
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
            try { ShutdownMode = ShutdownMode.OnMainWindowClose; }
            catch (InvalidOperationException)
            {
                StartupLog("App shutting down before main window — aborting cleanly.");
                return;
            }
            mainWindow.Show();

            // Non-blocking subscription-outage warning (online gym that hasn't reached the cloud in a while).
            if (!string.IsNullOrWhiteSpace(_pendingLockWarning))
            {
                var arWarn = Helpers.LanguageManager.Instance.IsArabic;
                Helpers.ToastNotification.Show(
                    arWarn ? "تنبيه الاشتراك" : "Subscription notice",
                    _pendingLockWarning!, isError: false);
                _pendingLockWarning = null;
            }

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

                // QR pool is opt-in (Setup wizard or SuperAdmin). Skip all generation/upload when off,
                // so installs that don't use guest/daily QR never load codes onto the gate.
                if (!AccessControlPro.Application.Services.FeatureFlags.IsQrPoolEnabled())
                {
                    StartupLog("QR pool disabled for this gym — skipping generation/upload (enable via Setup or SuperAdmin).");
                    return;
                }
                try
                {
                    using var qrScope = _serviceProvider.CreateScope();
                    var qrPool = qrScope.ServiceProvider.GetRequiredService<IQrPoolService>();
                    var available = await qrPool.GetAvailableCountAsync();
                    int generated = 0;
                    if (available == 0)
                    {
                        StartupLog($"Generating initial QR pool ({qrPool.ConfigPoolSize} local codes from {qrPool.ConfigRangeStart})...");
                        generated = await qrPool.GeneratePoolAsync(qrPool.ConfigPoolSize, qrPool.ConfigRangeStart, "Local");
                        StartupLog($"QR pool generated: {generated} codes");
                    }
                    else if (available < 500)
                    {
                        StartupLog($"QR pool low ({available} available)");
                    }

                    // Visitor (cloud) segment: a separate block of codes the OWNER PORTAL hands
                    // out to named guests. Generated once at a high, non-overlapping offset and
                    // uploaded to the gate alongside the local pool, so a cloud-issued visitor QR
                    // opens the door. The pool sync carries these up to the cloud (Source='Cloud').
                    var cloudAvailable = await qrPool.GetAvailableCountAsync("Cloud");
                    if (cloudAvailable == 0)
                    {
                        StartupLog("Generating visitor (cloud) QR segment (1000 codes)...");
                        var cloudGen = await qrPool.GeneratePoolAsync(1000, qrPool.ConfigRangeStart + 10000, "Cloud");
                        StartupLog($"Visitor (cloud) QR segment generated: {cloudGen} codes");
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

                                // Visible feedback for the (otherwise silent) background upload: a start
                                // toast, quarter-progress toasts, and a completion toast — so the operator
                                // can see the gate is being loaded and know when it's done.
                                bool arQr = Helpers.LanguageManager.Instance.IsArabic;
                                Helpers.ToastNotification.Show(
                                    arQr ? "تحميل رموز الدخول" : "Loading access codes",
                                    arQr ? $"يجري تحميل {pendingCount} رمز إلى البوابة بالخلفية…"
                                         : $"Uploading {pendingCount} codes to the gate in the background…",
                                    isError: false, seconds: 8);
                                int lastPct = 0;
                                var qrProgress = new Progress<(int done, int total)>(p =>
                                {
                                    if (p.total <= 0) return;
                                    int pct = (int)(p.done * 100L / p.total);
                                    if (pct >= lastPct + 25 && p.done < p.total)
                                    {
                                        lastPct = pct;
                                        Helpers.ToastNotification.Show(
                                            arQr ? "تحميل رموز الدخول" : "Loading access codes",
                                            $"{p.done} / {p.total}", isError: false, seconds: 5);
                                    }
                                });

                                var (uploaded, cleaned, regen) = await qrPool.SyncQrPoolToDeviceAsync(sdk, deviceInfos, qrProgress);
                                StartupLog($"QR Pool upload: {uploaded} uploaded, {cleaned} cleaned, {regen} regenerated to {allDevices.Count} device(s)");
                                Helpers.ToastNotification.Show(
                                    arQr ? "اكتمل تحميل رموز الدخول ✓" : "Access codes loaded ✓",
                                    arQr ? $"تم تحميل {uploaded} رمز إلى البوابة."
                                         : $"{uploaded} access codes loaded to the gate.",
                                    isError: false, seconds: 8);
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

            // Device connectivity watchdog — pings each configured device every minute. If a
            // device drops offline the customer gets a non-blocking corner alert immediately
            // (so a silent disconnect that stops events/entries is noticed at once, not days
            // later), and a "back online" note when it recovers. Silent while everything is OK.
            _deviceWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _deviceWatchdogTimer.Tick += async (_, _) => await RunDeviceWatchdogAsync();
            _deviceWatchdogTimer.Start();
            // First check ~45s after startup so SQL Server + devices are ready.
            _ = Task.Run(async () => { await Task.Delay(45000); await Dispatcher.InvokeAsync(async () => await RunDeviceWatchdogAsync()); });

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
                            // SuperAdmin can switch a gym's online subscription off — then we stop pushing
                            // data, but the lock poll below keeps running so it can be switched back on.
                            if (AccessControlPro.Application.Services.FeatureFlags.IsOnlineEnabled())
                            {
                                var result = await Task.Run(() => cloudSync.SyncToCloudAsync());
                                StartupLog($"CloudSync: {result}");
                            }
                            else StartupLog("CloudSync skipped — online subscription disabled by SuperAdmin.");
                        }
                        catch (Exception ex2)
                        {
                            StartupLog($"CloudSync error: {ex2.Message}");
                        }

                        // Remote payment lock check (rides the 5-min sync timer).
                        try
                        {
                            var lockResult = await Helpers.RemoteLockService.EvaluateAsync();
                            if (lockResult.Locked)
                                Dispatcher.Invoke(() => ShowLockWindowIfNeeded(lockResult.Message));
                        }
                        catch { /* non-critical */ }
                    };
                    _cloudSyncTimer.Start();

                    // Run initial sync after 30 seconds
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(30000);
                        try
                        {
                            if (AccessControlPro.Application.Services.FeatureFlags.IsOnlineEnabled())
                            {
                                var result = await cloudSync.SyncToCloudAsync();
                                StartupLog($"CloudSync (initial): {result}");
                            }
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
            // Both upload to the cloud, so they're skipped on offline / local-only installs.
            if (CloudSyncService.IsCloudSyncEnabledFlag())
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
                    _lastWorkingSetMb = (int)ws;

                    // Open-window count is the clearest WPF-leak signal: if it climbs over hours,
                    // dialogs/windows are being retained (not GC'd). Runs on the UI dispatcher thread.
                    var winCount = System.Windows.Application.Current?.Windows.Count ?? 0;
                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [info] " +
                               $"ws={ws}MB private={priv}MB heap={heap}MB " +
                               $"gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)} " +
                               $"windows={winCount} uptime={uptime.TotalHours:F1}h\n";
                    RollingLogFile.Append(MemoryLogPath, line);

                    // When memory is elevated, capture a managed-heap histogram into heap_log.txt (which
                    // the diagnostics bundle auto-includes) so we can see WHAT is leaking without a
                    // multi-GB dump. Runs OFF the UI thread (the snapshot walks the whole heap, ~seconds),
                    // throttled to hourly, and only in a SAFE window: high enough to be diagnostic (>600 MB)
                    // but with headroom left so the capture itself doesn't tip a 32-bit process over (<1500 MB).
                    if (ws > 600 && ws < 1500
                        && (DateTime.Now - _lastHeapCaptureAt) > TimeSpan.FromMinutes(60)
                        && System.Threading.Interlocked.CompareExchange(ref _heapCaptureRunning, 1, 0) == 0)
                    {
                        _lastHeapCaptureAt = DateTime.Now;
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try { RollingLogFile.Append(HeapLogPath, HeapHistogram.CaptureTopTypes(25)); }
                            catch { }
                            finally { System.Threading.Interlocked.Exchange(ref _heapCaptureRunning, 0); }
                        });
                    }

                    // Warning thresholds: at >500 MB working set we shout, at >700 MB we
                    // proactively trigger a Gen2 compacting GC and log a critical entry.
                    if (ws > 700)
                    {
                        RollingLogFile.Append(MemoryLogPath,
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [error] working set high ({ws} MB) — forcing Gen2 compacting GC\n");
                        // GCCollectionMode.Aggressive requires blocking:true — passing false throws
                        // "AggressiveGC requires setting the blocking parameter to true", which made
                        // this emergency compaction fail every time it was needed (the very moment
                        // memory was highest). Blocking here is fine: it runs on the monitor's
                        // background timer thread, not the UI thread.
                        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
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

            // Scheduled twice-daily FULL PC REBOOT — the surest reset for the 32-bit address-space
            // leak: it clears the whole process AND the native SDK's handles/memory, not just the
            // managed heap. Default ON at 17:00 + 22:00; configurable per gym via appsettings
            // "AppRestartTimes":["17:00","22:00"] (empty array turns it off). Windows shows a ~2-minute
            // warning before rebooting so staff can finish a sale (or cancel with `shutdown /a`). It
            // is SKIPPED if the app only started recently (a fresh process has no leak worth a reboot —
            // this also covers the app relaunching after the reboot). The persisted slot marker stops
            // it re-firing once the machine comes back up.
            var restartTimes = LoadRestartTimes();
            if (restartTimes.Count > 0)
            {
                // Since the reboot is enabled, make sure the app comes back up after the machine
                // restarts (needs the PC to auto-login to the desktop as well).
                EnsureAutoStartOnBoot();
                _scheduledRestartTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                _scheduledRestartTimer.Tick += (_, _) =>
                {
                    try
                    {
                        var now = DateTime.Now;
                        foreach (var (h, m) in restartTimes)
                        {
                            var slotKey = $"{now:yyyy-MM-dd}:{h:D2}:{m:D2}";
                            // Skip if we already rebooted for this slot. The marker is PERSISTED to disk —
                            // otherwise the reboot wipes the in-memory set and the relaunched app could
                            // re-trigger inside the same window.
                            if (_restartSlotsDone.Contains(slotKey) || LoadLastRestartSlot() == slotKey) continue;
                            var slot = now.Date.AddHours(h).AddMinutes(m);
                            if (now < slot) continue;                                  // not time yet
                            if (now >= slot.AddMinutes(30)) { _restartSlotsDone.Add(slotKey); continue; } // missed the window
                            // Don't reboot a machine whose app only just started — memory is already fresh,
                            // so a reboot would be pure disruption. Also skips the post-reboot relaunch.
                            if (ProcessUptime() < TimeSpan.FromHours(2)) { _restartSlotsDone.Add(slotKey); continue; }
                            _restartSlotsDone.Add(slotKey);
                            SaveLastRestartSlot(slotKey);   // persist BEFORE the reboot so the relaunch doesn't re-fire
                            try { RollingLogFile.Append(MemoryLogPath, $"{now:yyyy-MM-dd HH:mm:ss} [info] scheduled PC reboot ({h:D2}:{m:D2})\n"); } catch { }
                            RebootMachine();
                            return;
                        }
                    }
                    catch { }
                };
                _scheduledRestartTimer.Start();
                StartupLog($"Scheduled PC reboot at: {string.Join(", ", restartTimes.Select(t => $"{t.h:D2}:{t.m:D2}"))}");
            }

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

                    // Nudge a graceful restart when EITHER the app has been up 5+ days OR memory
                    // has crept high (>750 MB working set — above the 700 MB GC ceiling, so it's
                    // genuinely not recoverable). The memory trigger is the important one: Basmia's
                    // receptionist force-kills the app once it feels heavy (~700 MB after a full
                    // day), which produces false "crash-recovery" bundles and loses the clean
                    // shutdown. A polite "please restart" converts that into a graceful restart
                    // that resets memory to ~180 MB.
                    bool highMemory = _lastWorkingSetMb > 750;
                    if (uptime.TotalDays < 5 && !highMemory) return;

                    // First qualifying tick, OR 12+ hours since last reminder.
                    if (!restartShownAlready || (DateTime.Now - lastRestartPrompt).TotalHours >= 12)
                    {
                        restartShownAlready = true;
                        lastRestartPrompt = DateTime.Now;
                        StartupLog($"Restart reminder fired (uptime={uptime.TotalDays:F1}d, ws={_lastWorkingSetMb}MB, highMemory={highMemory})");

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // Memory-driven message avoids saying "X days" when it fired after
                                // only a few hours of heavy use.
                                var msg = LanguageManager.Instance.IsArabic
                                    ? (uptime.TotalDays >= 5
                                        ? $"البرنامج يعمل منذ {(int)uptime.TotalDays} أيام. للحصول على أداء أفضل يُنصح بإغلاق البرنامج ثم فتحه من جديد عندما يناسب ذلك."
                                        : "البرنامج يستهلك ذاكرة كبيرة. للحصول على أداء أفضل يُنصح بإغلاق البرنامج ثم فتحه من جديد عندما يناسب ذلك.")
                                    : (uptime.TotalDays >= 5
                                        ? $"The app has been running for {(int)uptime.TotalDays} days. For best performance, close and reopen it when convenient."
                                        : "The app is using a lot of memory. For best performance, close and reopen it when convenient.");
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
                if (!AccessControlPro.Application.Services.FeatureFlags.IsQrPoolEnabled()) return; // opt-in only
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
        _deviceWatchdogTimer?.Stop();
        _deviceWatchdogTimer = null;
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

    /// <summary>Parses the twice-daily PC-reboot times from appsettings ("AppRestartTimes":
    /// ["17:00","22:00"]). Defaults to 17:00 + 22:00 (ON) when the key is absent; an empty array
    /// turns the scheduled reboot off for that gym. Each entry is "HH:mm" (24-hour).</summary>
    private static List<(int h, int m)> LoadRestartTimes()
    {
        var result = new List<(int, int)>();
        string[] raw = { "17:00", "22:00" }; // default ON — twice-daily PC reboot (5 PM + 10 PM)
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("AppRestartTimes", out var el)
                    && el.ValueKind == System.Text.Json.JsonValueKind.Array)
                    raw = el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray();
            }
        }
        catch { /* use defaults */ }
        foreach (var s in raw)
        {
            var parts = s.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m)
                && h >= 0 && h < 24 && m >= 0 && m < 60)
                result.Add((h, m));
        }
        return result;
    }

    /// <summary>File that records the last scheduled-restart slot we completed (e.g. "2026-07-05:23:00").
    /// Persisting it survives the restart itself, so the relaunched process knows the slot is done and
    /// does NOT restart again — the fix for the every-minute restart loop.</summary>
    private static string ScheduledRestartMarkerPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "AccessControlPro", "scheduled_restart.txt");

    private static string LoadLastRestartSlot()
    {
        try { return File.Exists(ScheduledRestartMarkerPath) ? File.ReadAllText(ScheduledRestartMarkerPath).Trim() : ""; }
        catch { return ""; }
    }

    private static void SaveLastRestartSlot(string slotKey)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScheduledRestartMarkerPath)!);
            File.WriteAllText(ScheduledRestartMarkerPath, slotKey);
        }
        catch { /* best-effort: default-OFF + idle-gate already prevent the loop */ }
    }

    private static void RestartApp(string reason = "restart")
    {
        // Mark the exit as PLANNED so the next launch doesn't fire a false "crash-recovery"
        // bundle (Environment.Exit below skips OnExit → RecordCleanExit never runs).
        try { LastRunStateTracker.RecordPlannedRestart(reason); } catch { }
        ReleaseSingleInstanceMutex();
        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(exePath);
        Environment.Exit(0);
    }

    /// <summary>How long the current app process has been running (large value if unknown, so the
    /// scheduled reboot still proceeds).</summary>
    private static TimeSpan ProcessUptime()
    {
        try { return DateTime.Now - Process.GetCurrentProcess().StartTime; }
        catch { return TimeSpan.FromDays(365); }
    }

    /// <summary>Registers the app to launch automatically after Windows login (HKCU Run key), so a
    /// scheduled PC reboot brings the gate software back up on its own — no manual reopen. HKCU needs
    /// no admin; idempotent (refreshes the exe path each launch). Only meaningful if the PC also has
    /// Windows AUTO-LOGIN enabled so it reaches the desktop without a manual password after reboot.</summary>
    private static void EnsureAutoStartOnBoot()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            key?.SetValue("AccessControlPro", "\"" + exe + "\"");
        }
        catch { /* best-effort — startup registration is non-critical */ }
    }

    /// <summary>Triggers a FULL WINDOWS REBOOT with a ~2-minute warning (for the scheduled memory
    /// reset). Marks the exit PLANNED first so the next boot doesn't log a false crash bundle.
    /// Best-effort — silently no-ops if the account lacks reboot privilege. Staff can abort the
    /// pending reboot with `shutdown /a` during the countdown.</summary>
    private static void RebootMachine()
    {
        try { LastRunStateTracker.RecordPlannedRestart("scheduled PC reboot"); } catch { }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/r /t 120 /c \"Scheduled maintenance restart - اعادة تشغيل الصيانة المجدولة\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { /* best-effort — e.g. insufficient privilege to reboot */ }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>How long since the last keyboard/mouse input on this machine (0 if unavailable).</summary>
    private static TimeSpan UserIdleTime()
    {
        try
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii))
                return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - lii.dwTime));
        }
        catch { }
        return TimeSpan.Zero;
    }

    /// <summary>Safe to auto-restart only when no dialog is open (operator not mid-action). When
    /// <paramref name="requireIdle"/> is set (the scheduled restart), ALSO require the machine to
    /// have had no keyboard/mouse input for 10 minutes — so a scheduled restart can never interrupt
    /// someone actively using the app. OOM self-heal passes false: the app is already failing, so a
    /// no-dialog moment is enough.</summary>
    private static bool IsSafeToRestart(bool requireIdle = false)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app == null) return false;
            if (app.Windows.OfType<System.Windows.Window>().Count(w => w.IsVisible) > 1) return false;
            if (requireIdle && UserIdleTime() < TimeSpan.FromMinutes(10)) return false;
            return true;
        }
        catch { return false; }
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

    // Runs a silent backup on startup if the last successful backup is older than 12 hours.
    // Catches the case where the customer turns the PC off at night and the 02:00 AM
    // scheduled backup never fires. RunBackupAsync internally does:
    //   1. Delete old .bak files (3-day retention)
    //   2. DBCC SHRINKFILE on the transaction log
    //   3. REBUILD indexes + update statistics
    //   4. BACKUP DATABASE
    // So this single call covers all of "shrink + cleanup + backup" silently before login.
    // Shows the full-screen lock window over the running app when a lock is detected at
    // runtime (the window self-closes when the cloud reports unlocked).
    private void ShowLockWindowIfNeeded(string message)
    {
        if (_lockWindow != null) { _lockWindow.UpdateMessage(message); return; }
        _lockWindow = new Views.LockWindow(message);
        _lockWindow.Closed += (_, _) => _lockWindow = null;
        _lockWindow.Show();
        _lockWindow.Activate();
    }

    private void RunStartupBackupIfStale()
    {
        const double STALE_HOURS = 12.0;

        var status = BackupService.LoadStatus();
        if (status.LastSuccess.HasValue)
        {
            var ageHours = (DateTime.Now - status.LastSuccess.Value).TotalHours;
            if (ageHours < STALE_HOURS)
            {
                StartupLog($"Startup backup skipped (last backup {ageHours:F1}h ago, threshold {STALE_HOURS}h)");
                return;
            }
            StartupLog($"Startup backup needed (last backup {ageHours:F1}h ago, > {STALE_HOURS}h)");
        }
        else
        {
            StartupLog("Startup backup needed (no previous successful backup recorded)");
        }

        // Verify a backup path is configured — if not, skip silently (first-run install before setup wizard saves config)
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(settingsPath))
            {
                var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (!doc.RootElement.TryGetProperty("BackupPath", out var bp) || string.IsNullOrWhiteSpace(bp.GetString()))
                {
                    StartupLog("Startup backup skipped (BackupPath not configured)");
                    return;
                }
            }
            else { return; }
        }
        catch { return; }

        // Build a minimal splash window so the customer sees something is happening
        var isArabic = Helpers.LanguageManager.Instance.IsArabic;
        var splash = new Window
        {
            Title = "Backup",
            Width = 460,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x3D, 0x3E))
        };
        var stack = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(30),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = isArabic ? "جاري النسخ الاحتياطي للبيانات..." : "Backing up database...",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = isArabic ? "قد يستغرق حتى دقيقة واحدة. لا تغلق التطبيق." : "This may take up to a minute. Please don't close the app.",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0xC8, 0xC8)),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 22)
        });
        stack.Children.Add(new System.Windows.Controls.ProgressBar
        {
            IsIndeterminate = true,
            Height = 6,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xA1, 0xA3)),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x55, 0x57))
        });
        splash.Content = stack;
        splash.Show();

        // Run the backup on a background thread, pump the dispatcher so the splash stays painted
        var frame = new DispatcherFrame();
        string result = "(unknown)";

        Task.Run(async () =>
        {
            try
            {
                var backupService = _serviceProvider.GetRequiredService<IBackupService>();
                result = await backupService.RunBackupAsync();
            }
            catch (Exception ex)
            {
                result = "ERROR: " + ex.Message;
            }
            finally
            {
                // Marshal back to UI thread to close splash + unblock the frame
                Dispatcher.Invoke(() =>
                {
                    try { splash.Close(); } catch { }
                    frame.Continue = false;
                });
            }
        });

        // Blocks until backup task completes, but keeps the splash window responsive
        Dispatcher.PushFrame(frame);

        StartupLog($"Startup backup: {result}");

        // Surface low-disk warning if the backup flagged it. Done AFTER the splash closes
        // so it doesn't overlap the splash visually. Read the freshly-saved status.
        try
        {
            var postStatus = BackupService.LoadStatus();
            if (postStatus.LowDiskWarning && postStatus.LastFreeSpaceMb > 0)
            {
                var isAr = Helpers.LanguageManager.Instance.IsArabic;
                var freeMb = postStatus.LastFreeSpaceMb;
                var refused = result.StartsWith("Backup REFUSED", StringComparison.OrdinalIgnoreCase);

                string title = isAr ? "تحذير: مساحة التخزين منخفضة" : "Warning: Low disk space";
                string msg = refused
                    ? (isAr
                        ? $"تم إيقاف النسخ الاحتياطي لأن المساحة المتاحة على القرص {freeMb} ميجابايت فقط.\n\nيرجى تحرير مساحة على القرص ثم إعادة تشغيل التطبيق."
                        : $"Backup was refused — only {freeMb} MB free on the backup drive.\n\nFree up disk space and restart the app to retry the backup.")
                    : (isAr
                        ? $"المساحة المتاحة على قرص النسخ الاحتياطي {freeMb} ميجابايت فقط.\n\nالنسخ الاحتياطي لا يزال يعمل، لكن يرجى تحرير مساحة قريباً."
                        : $"The backup drive has only {freeMb} MB free.\n\nBackups are still working, but please free up disk space soon.");

                Views.CustomMessageBox.Show(msg, title, refused ? Views.MsgType.Error : Views.MsgType.Warning);
            }
        }
        catch (Exception ex)
        {
            StartupLog($"Low-disk warning surface failed (non-critical): {ex.Message}");
        }
    }

    // Device connectivity watchdog (runs every minute). Pings each configured device and
    // alerts the customer — via a non-blocking corner toast — only when a device's state
    // CHANGES (online->offline or back), so it never spams. Silent while all is well.
    private async Task RunDeviceWatchdogAsync()
    {
        try
        {
            List<Device> devices;
            using (var scope = _serviceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                devices = (await repo.GetAllAsync()).ToList();
            }

            foreach (var d in devices)
            {
                if (string.IsNullOrWhiteSpace(d.IP)) continue;

                bool reachable = await Helpers.NetworkHelper.PingDeviceAsync(d.IP);
                bool known = _deviceReachable.TryGetValue(d.IP, out var prev);
                _deviceReachable[d.IP] = reachable;

                if (!known)
                {
                    // First observation this session: alert only if it's already offline.
                    if (!reachable) NotifyDeviceState(d, online: false);
                    StartupLog($"DeviceWatchdog: {d.Name} ({d.IP}) initial={(reachable ? "online" : "OFFLINE")}");
                    continue;
                }

                if (prev && !reachable)
                {
                    StartupLog($"DeviceWatchdog: {d.Name} ({d.IP}) went OFFLINE");
                    NotifyDeviceState(d, online: false);
                }
                else if (!prev && reachable)
                {
                    StartupLog($"DeviceWatchdog: {d.Name} ({d.IP}) back ONLINE");
                    NotifyDeviceState(d, online: true);
                }
            }
        }
        catch (Exception ex)
        {
            StartupLog($"DeviceWatchdog error: {ex.Message}");
        }
    }

    private static void NotifyDeviceState(Device d, bool online)
    {
        bool ar = Helpers.LanguageManager.Instance.IsArabic;
        var name = string.IsNullOrWhiteSpace(d.Name) ? d.IP : d.Name;
        if (online)
        {
            Helpers.ToastNotification.Show(
                ar ? "عاد الاتصال بالجهاز" : "Device reconnected",
                ar ? $"الجهاز \"{name}\" يعمل الآن. تم استئناف تسجيل الدخول/الخروج."
                   : $"Device \"{name}\" is back online. Entries/exits are being recorded again.",
                isError: false);
        }
        else
        {
            Helpers.ToastNotification.Show(
                ar ? "انقطع الاتصال بالجهاز" : "Device offline",
                ar ? $"تعذّر الوصول إلى الجهاز \"{name}\" ({d.IP}). لن يتم تسجيل الدخول/الخروج حتى يعود الاتصال — تحقّق من الكهرباء والشبكة."
                   : $"Cannot reach device \"{name}\" ({d.IP}). Entries/exits won't be recorded until it's back — check its power and network.",
                isError: true);
        }
    }

    // Update check — runs in background a few seconds after launch. If the VPS has a
    // newer build and this version isn't snoozed/skipped, shows a CustomMessageBox
    // prompt. Customer choices:
    //   • Yes (Update Now) → download + verify + stage → exit + Updater.exe takes over
    //   • No  (Later)      → snooze for 24h
    //   • Cancel           → if mandatory: re-prompt next launch; else: skip this version
    private async Task CheckForUpdateAndPromptAsync()
    {
        // Wait a bit so the prompt doesn't fight the login window for focus
        await Task.Delay(5000);

        var checker = _serviceProvider.GetRequiredService<IUpdateCheckService>();
        var result = await checker.CheckAsync();

        if (!result.UpdateAvailable || result.Manifest == null)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
                StartupLog($"Update check: {result.ErrorMessage}");
            else
                StartupLog($"Update check: already on latest (v{result.CurrentVersion})");
            return;
        }

        StartupLog($"Update available: v{result.CurrentVersion} → v{result.LatestVersion}, mandatory={result.IsMandatory}");

        // Respect snooze/skip unless it's a mandatory update
        if (!result.IsMandatory && checker.IsSnoozed(result.Manifest.Version))
        {
            StartupLog($"Update v{result.Manifest.Version} is snoozed/skipped — not prompting");
            return;
        }

        // Marshal back to UI thread for the prompt
        Dispatcher.Invoke(() =>
        {
            try { ShowUpdatePrompt(result, checker); }
            catch (Exception ex) { StartupLog($"Update prompt error: {ex.Message}"); }
        });
    }

    private void ShowUpdatePrompt(UpdateCheckResult check, IUpdateCheckService checker)
    {
        var manifest = check.Manifest!;
        var isAr = Helpers.LanguageManager.Instance.IsArabic;
        var notes = isAr ? manifest.ReleaseNotesAr : manifest.ReleaseNotesEn;
        if (string.IsNullOrWhiteSpace(notes)) notes = "—";

        var sizeMb = manifest.SizeBytes > 0 ? $"{manifest.SizeBytes / 1024 / 1024} MB" : "?";

        var title = isAr
            ? (check.IsMandatory ? $"تحديث إلزامي — الإصدار {manifest.Version}" : $"تحديث جديد متاح — الإصدار {manifest.Version}")
            : (check.IsMandatory ? $"Required update — version {manifest.Version}" : $"New update available — version {manifest.Version}");

        var msg = isAr
            ? $"الإصدار الحالي: {check.CurrentVersion}\nالإصدار الجديد: {manifest.Version}\nالحجم: {sizeMb}\n\nما الجديد:\n{notes}\n\nهل تريد التحديث الآن؟"
            : $"Current version: {check.CurrentVersion}\nNew version: {manifest.Version}\nSize: {sizeMb}\n\nWhat's new:\n{notes}\n\nUpdate now?";

        // Mandatory update: a single "OK" button — no decline, no snooze, no skip. The customer
        // is forced to update; any dismissal (incl. the X) still proceeds to the download.
        if (check.IsMandatory)
        {
            var forcedMsg = isAr
                ? $"الإصدار الحالي: {check.CurrentVersion}\nالإصدار الجديد: {manifest.Version}\nالحجم: {sizeMb}\n\nما الجديد:\n{notes}\n\nهذا التحديث إلزامي — اضغط (موافق) ليبدأ التحديث الآن."
                : $"Current version: {check.CurrentVersion}\nNew version: {manifest.Version}\nSize: {sizeMb}\n\nWhat's new:\n{notes}\n\nThis update is required — click OK to update now.";
            MessageBox.Show(forcedMsg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            StartupLog($"Mandatory update v{manifest.Version} — forcing download (no decline offered)");
            _ = Task.Run(() => DownloadStageAndInstallAsync(manifest));
            return;
        }

        // Optional update: Yes=update, No=snooze 24h, Cancel=skip this version forever.
        var choice = MessageBox.Show(msg, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

        if (choice == MessageBoxResult.Yes)
        {
            // Download + stage → exit + Updater.exe takes over
            _ = Task.Run(() => DownloadStageAndInstallAsync(manifest));
        }
        else if (choice == MessageBoxResult.No)
        {
            checker.SnoozeVersion(manifest.Version, TimeSpan.FromHours(24));
            StartupLog($"Update v{manifest.Version} snoozed for 24h");
        }
        else // Cancel
        {
            checker.SkipVersion(manifest.Version);
            StartupLog($"Update v{manifest.Version} skipped (won't prompt again)");
        }
    }

    // Reads .post_update written by Updater.exe and shows a one-time "What's new" dialog.
    // The release notes are stashed in the pending_update.json's stagedAt copy, but since
    // we delete that during install, we fall back to fetching the manifest fresh.
    private void ShowPostUpdateNotesIfAny()
    {
        var markerPath = Path.Combine(AppContext.BaseDirectory, ".post_update");
        if (!File.Exists(markerPath)) return;

        string installedVersion;
        try { installedVersion = File.ReadAllText(markerPath).Trim(); }
        catch { installedVersion = ""; }

        // Always delete the marker, even if we fail to show the dialog — otherwise it
        // would re-prompt forever.
        try { File.Delete(markerPath); } catch { }

        var isAr = Helpers.LanguageManager.Instance.IsArabic;
        var title = isAr ? $"تم التحديث إلى الإصدار {installedVersion}" : $"Updated to version {installedVersion}";

        // Fetch the manifest one more time to get release notes (the staged ZIP is gone).
        // This is best-effort: if the VPS is unreachable, we just show a generic success message.
        string notes;
        try
        {
            var checker = _serviceProvider.GetRequiredService<IUpdateCheckService>();
            var checkTask = Task.Run(() => checker.CheckAsync());
            checkTask.Wait(5000);
            var m = checkTask.IsCompletedSuccessfully ? checkTask.Result.Manifest : null;
            notes = m != null
                ? (isAr ? m.ReleaseNotesAr : m.ReleaseNotesEn)
                : "";
        }
        catch { notes = ""; }

        var body = string.IsNullOrWhiteSpace(notes)
            ? (isAr ? $"تم تثبيت الإصدار {installedVersion} بنجاح." : $"Version {installedVersion} installed successfully.")
            : (isAr ? $"ما الجديد في الإصدار {installedVersion}:\n\n{notes}" : $"What's new in version {installedVersion}:\n\n{notes}");

        MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Information);
        StartupLog($"Post-update dialog shown for v{installedVersion}");
    }

    private async Task DownloadStageAndInstallAsync(VersionManifest manifest)
    {
        var installer = _serviceProvider.GetRequiredService<IUpdateInstallerService>();

        // Show a download splash with a progress bar
        Window? splash = null;
        System.Windows.Controls.ProgressBar? bar = null;
        System.Windows.Controls.TextBlock? statusText = null;
        var isAr = Helpers.LanguageManager.Instance.IsArabic;

        await Dispatcher.InvokeAsync(() =>
        {
            splash = new Window
            {
                Title = "Update",
                Width = 480,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x3D, 0x3E))
            };
            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(30), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = isAr ? $"تنزيل التحديث {manifest.Version}..." : $"Downloading update {manifest.Version}...",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
            statusText = new System.Windows.Controls.TextBlock
            {
                Text = isAr ? "جاري الاتصال..." : "Connecting...",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0xC8, 0xC8)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(statusText);
            bar = new System.Windows.Controls.ProgressBar
            {
                Minimum = 0, Maximum = 100, Value = 0,
                Height = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xA1, 0xA3)),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x55, 0x57))
            };
            stack.Children.Add(bar);
            splash.Content = stack;
            splash.Show();
        });

        var progress = new Progress<UpdateProgress>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                if (bar != null && p.BytesTotal > 0)
                    bar.Value = p.PercentComplete;
                if (statusText != null)
                {
                    statusText.Text = p.Phase switch
                    {
                        "downloading" => isAr
                            ? $"تنزيل... {p.BytesDownloaded / 1024 / 1024} / {p.BytesTotal / 1024 / 1024} ميجابايت"
                            : $"Downloading... {p.BytesDownloaded / 1024 / 1024} / {p.BytesTotal / 1024 / 1024} MB",
                        "verifying" => isAr ? "التحقق من سلامة الملف..." : "Verifying integrity...",
                        "backing-up" => isAr ? "نسخة احتياطية قبل التحديث..." : "Pre-update backup...",
                        "ready" => isAr ? "جاهز — إعادة التشغيل..." : "Ready — restarting...",
                        _ => statusText.Text
                    };
                }
            });
        });

        UpdateInstallResult staged;
        try
        {
            staged = await installer.DownloadAndStageAsync(manifest, progress);
        }
        catch (Exception ex)
        {
            staged = new UpdateInstallResult { Success = false, ErrorMessage = ex.Message };
        }

        Dispatcher.Invoke(() => { try { splash?.Close(); } catch { } });

        if (!staged.Success)
        {
            StartupLog($"Update download failed: {staged.ErrorMessage}");
            // Most failures here are a dropped connection mid-download (EOF / 0 bytes / timeout).
            // Show a clear, reassuring message for those instead of the raw .NET exception.
            var errLower = (staged.ErrorMessage ?? "").ToLowerInvariant();
            bool netErr = errLower.Contains("eof") || errLower.Contains("transport") || errLower.Contains("0 bytes")
                || errLower.Contains("timed out") || errLower.Contains("timeout") || errLower.Contains("connection")
                || errLower.Contains("ssl") || errLower.Contains("remote name") || errLower.Contains("unreachable")
                || errLower.Contains("network") || errLower.Contains("socket") || errLower.Contains("httprequest");
            Dispatcher.Invoke(() =>
            {
                var msg = netErr
                    ? (isAr ? "انقطع الاتصال بالإنترنت أثناء تنزيل التحديث.\n\nتأكد من اتصال الإنترنت — وسيُعاد التنزيل تلقائياً عند فتح البرنامج من جديد (يبدأ من البداية، ولا يُركَّب تحديث ناقص أبداً)."
                            : "The internet connection dropped while downloading the update.\n\nCheck your connection — it will re-download automatically next time you open the app (it restarts from scratch; a partial update is never installed).")
                    : (isAr ? $"فشل تنزيل التحديث:\n{staged.ErrorMessage}" : $"Update download failed:\n{staged.ErrorMessage}");
                MessageBox.Show(msg, isAr ? "خطأ في التحديث" : "Update error",
                    MessageBoxButton.OK, netErr ? MessageBoxImage.Warning : MessageBoxImage.Error);
            });
            return;
        }

        // Launch Updater.exe and exit the WPF app so file locks release
        StartupLog($"Update staged successfully — handing off to Updater.exe");
        // Mark this as a PLANNED exit BEFORE handing off: the Updater kills this process to swap
        // files, which can race (and lose to) OnExit→RecordCleanExit and leave the state at
        // "running" → a spurious "crash-recovery" bundle on the next launch. Recording it here
        // first means the worst case is "planned_restart", never a false crash.
        try { LastRunStateTracker.RecordPlannedRestart("auto-update"); } catch { }
        Dispatcher.Invoke(() =>
        {
            try
            {
                installer.TriggerInstallAndExit(staged);
                Shutdown();
            }
            catch (Exception ex)
            {
                StartupLog($"Failed to launch Updater.exe: {ex.Message}");
                MessageBox.Show(
                    isAr ? $"فشل بدء تثبيت التحديث:\n{ex.Message}" : $"Failed to start update install:\n{ex.Message}",
                    isAr ? "خطأ في التحديث" : "Update error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
}
