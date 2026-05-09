using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Infrastructure;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using AccessControlPro.WPF.ViewModels;
using AccessControlPro.POS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.POS;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private readonly ServiceProvider _serviceProvider;
    private static readonly string CrashLogPath = Path.Combine(AppContext.BaseDirectory, "pos_crash_log.txt");
    private DispatcherTimer? _cloudSyncTimer;

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

    private static string? LoadCloudSyncUrl()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("CloudSyncUrl", out var syncUrl))
            {
                var val = syncUrl.GetString();
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { }
        return null;
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
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<IEmployeeService, EmployeeService>();

        // Cloud sync
        services.AddSingleton<ICloudSyncService>(sp => new CloudSyncService(connectionString));

        // Reuse PosViewModel from main WPF project
        services.AddTransient<PosViewModel>();
        services.AddTransient<PosMainViewModel>();

        services.AddTransient<PosMainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "AccessControlPro_POS_SingleInstance", out bool isNew);
        if (!isNew)
        {
            CustomMessageBox.Show(
                "POS Terminal is already running.",
                "Already Running", MsgType.Info);
            Shutdown();
            return;
        }

        // ── First-run: POS only needs DB connection (main app handles full setup) ──
        if (!IsPosSetupComplete())
        {
            var setupWindow = new ConnectionSetupWindow(
                errorMessage: "POS Terminal Setup — Enter the database connection used by the main application.");
            if (setupWindow.ShowDialog() != true || !setupWindow.IsSaved)
            {
                Shutdown();
                return;
            }
            // Mark POS setup as complete
            MarkPosSetupComplete();
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
                appName: "HM-GymManagement POS",
                appIcon: FontAwesome.WPF.FontAwesomeIcon.ShoppingCart,
                gradientStart: System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32),
                gradientEnd: System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Check POS access permission
            if (!currentUser.HasPermission(AccessControlPro.Domain.Enums.AppPermission.AccessPOS))
            {
                CustomMessageBox.Show(
                    "Access denied. You do not have permission to use the POS Terminal.",
                    "Access Denied", MsgType.Warning);
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<PosMainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            // ── Cloud sync timer ──
            StartCloudSync();
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

    private void StartCloudSync()
    {
        try
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
                        WriteCrashLog("CloudSync", new Exception(result));
                    }
                    catch (Exception ex)
                    {
                        WriteCrashLog("CloudSync_Error", ex);
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
                    }
                    catch { }
                });
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("CloudSync_Init", ex);
        }
    }

    private static string GetAppVersion()
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null && File.Exists(exePath))
            return $"v_{new FileInfo(exePath).Length}";
        return "v_unknown";
    }

    private static bool IsPosSetupComplete()
    {
        var markerPath = Path.Combine(AppContext.BaseDirectory, ".setup_complete");
        if (!File.Exists(markerPath)) return false;
        try
        {
            var saved = File.ReadAllText(markerPath).Trim();
            return saved == GetAppVersion();
        }
        catch { return false; }
    }

    private static void MarkPosSetupComplete()
    {
        try
        {
            var markerPath = Path.Combine(AppContext.BaseDirectory, ".setup_complete");
            File.WriteAllText(markerPath, GetAppVersion());
        }
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
        _cloudSyncTimer?.Stop();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
