using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DevicesViewModel _devicesViewModel;
    private readonly DoorsViewModel _doorsViewModel;
    private readonly EmployeesViewModel _employeesViewModel;
    private readonly EventsViewModel _eventsViewModel;
    private readonly LogsViewModel _logsViewModel;
    private readonly DeletedRecordsViewModel _deletedRecordsViewModel;
    private readonly FinanceViewModel _financeViewModel;
    private readonly CashFlowViewModel _cashFlowViewModel;
    private readonly QrPassViewModel _qrPassViewModel;
    private readonly MonitorViewModel _monitorViewModel;
    private readonly CurrentUserService _currentUser;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    [ObservableProperty]
    private bool _showDeviceNotification;

    [ObservableProperty]
    private string _deviceNotificationMessage = "";

    [ObservableProperty]
    private string _deviceNotificationDetail = "";

    [ObservableProperty]
    private bool _isDeviceOperationRunning;

    [ObservableProperty]
    private string _deviceOperationProgress = "";

    public LanguageManager Lang => LanguageManager.Instance;

    public string CurrentUserDisplayName => _currentUser.DisplayName ?? _currentUser.Username ?? "";

    // Permission-based visibility for sidebar navigation
    public bool CanViewDashboard => _currentUser.HasPermission(AppPermission.DashboardView);
    public bool CanViewDevices => _currentUser.HasPermission(AppPermission.DevicesView);
    public bool CanViewDoors => _currentUser.HasPermission(AppPermission.DoorsView);
    public bool CanViewPlayers => _currentUser.HasPermission(AppPermission.PlayersView);
    public bool CanViewEvents => _currentUser.HasPermission(AppPermission.EventsView);
    public bool CanViewFinance => _currentUser.HasPermission(AppPermission.FinanceView);
    public bool CanViewCashFlow => _currentUser.HasPermission(AppPermission.CashFlowView);
    public bool CanViewLogs => _currentUser.HasPermission(AppPermission.LogsView);
    public bool CanViewDeleted => _currentUser.HasPermission(AppPermission.DeletedRecordsView);
    public bool CanViewDeletedRecords => CanViewDeleted; // Alias for XAML binding compatibility
    public bool CanViewMonitor => _currentUser.HasPermission(AppPermission.MonitorView);
    public bool CanViewDataMigration => _currentUser.HasPermission(AppPermission.DataMigration);
    public bool CanViewQrPass => _currentUser.HasPermission(AppPermission.QrPassManage);

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        DevicesViewModel devicesViewModel,
        DoorsViewModel doorsViewModel,
        EmployeesViewModel employeesViewModel,
        EventsViewModel eventsViewModel,
        LogsViewModel logsViewModel,
        DeletedRecordsViewModel deletedRecordsViewModel,
        FinanceViewModel financeViewModel,
        CashFlowViewModel cashFlowViewModel,
        QrPassViewModel qrPassViewModel,
        MonitorViewModel monitorViewModel,
        CurrentUserService currentUser,
        IServiceProvider serviceProvider)
    {
        _dashboardViewModel = dashboardViewModel;
        _devicesViewModel = devicesViewModel;
        _doorsViewModel = doorsViewModel;
        _employeesViewModel = employeesViewModel;
        _eventsViewModel = eventsViewModel;
        _logsViewModel = logsViewModel;
        _deletedRecordsViewModel = deletedRecordsViewModel;
        _financeViewModel = financeViewModel;
        _cashFlowViewModel = cashFlowViewModel;
        _qrPassViewModel = qrPassViewModel;
        _monitorViewModel = monitorViewModel;
        _currentUser = currentUser;
        _serviceProvider = serviceProvider;
        CurrentView = dashboardViewModel;
        _ = _dashboardViewModel.InitializeAsync();
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Lang.SwitchLanguage();
    }

    [RelayCommand]
    private async Task NavigateTo(string page)
    {
        // Enforce permission check before navigation
        var allowed = page switch
        {
            "Dashboard" => CanViewDashboard,
            "Devices" => CanViewDevices,
            "Doors" => CanViewDoors,
            "Employees" => CanViewPlayers,
            "Events" => CanViewEvents,
            "Finance" => CanViewFinance,
            "CashFlow" => CanViewCashFlow,
            "Logs" => CanViewLogs,
            "DeletedRecords" => CanViewDeleted,
            "QrPass" => CanViewQrPass,
            _ => true
        };

        if (!allowed) return;

        // Auto-stop monitoring when navigating to any page to avoid TCP conflicts during card ops
        if (_monitorViewModel.IsMonitoring)
            await _monitorViewModel.StopMonitoringCommand.ExecuteAsync(null);

        CurrentPage = page;
        ActivityLogger.LogNavigation(page);

        CurrentView = page switch
        {
            "Dashboard" => _dashboardViewModel,
            "Devices" => _devicesViewModel,
            "Doors" => _doorsViewModel,
            "Employees" => _employeesViewModel,
            "Events" => _eventsViewModel,
            "Logs" => _logsViewModel,
            "DeletedRecords" => _deletedRecordsViewModel,
            "Finance" => _financeViewModel,
            "CashFlow" => _cashFlowViewModel,
            "QrPass" => _qrPassViewModel,
            _ => CurrentView
        };

        // Initialize data-loading ViewModels on first navigation
        switch (page)
        {
            case "Dashboard":
                await _dashboardViewModel.InitializeAsync();
                break;
            case "Devices":
                await _devicesViewModel.InitializeAsync();
                break;
            case "Doors":
                await _doorsViewModel.InitializeAsync();
                break;
            case "Events":
                await _eventsViewModel.LoadDevicesAsync();
                break;
            case "Finance":
                await _financeViewModel.InitializeAsync();
                break;
            case "CashFlow":
                await _cashFlowViewModel.InitializeAsync();
                break;
            case "QrPass":
                await _qrPassViewModel.InitializeAsync();
                break;
        }
    }

    [RelayCommand]
    private void OpenMonitor()
    {
        if (!CanViewMonitor) return;
        var monitorWindow = new MonitorWindow(_monitorViewModel);

        // Try to place on secondary monitor using Win32 API
        var monitors = GetMonitorRects();
        if (monitors.Count > 1)
        {
            // Pick the first non-primary monitor
            var secondary = monitors.FirstOrDefault(m => !m.IsPrimary) ?? monitors[1];
            monitorWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            monitorWindow.Left = secondary.Left;
            monitorWindow.Top = secondary.Top;
            monitorWindow.Width = secondary.Width;
            monitorWindow.Height = secondary.Height;
            monitorWindow.WindowState = WindowState.Maximized;
        }

        monitorWindow.Show();
    }

    public async Task CheckPendingDeviceOperationsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var qrPool = scope.ServiceProvider.GetRequiredService<IQrPoolService>();

            var pendingCount = await qrPool.GetPendingUploadCountAsync();

            if (pendingCount > 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowDeviceNotification = true;
                    DeviceNotificationMessage = Lang.IsArabic
                        ? $"\u26a0\ufe0f {pendingCount} \u0631\u0645\u0632 QR \u0628\u062d\u0627\u062c\u0629 \u0644\u0644\u0631\u0641\u0639 \u0625\u0644\u0649 \u0627\u0644\u062c\u0647\u0627\u0632"
                        : $"\u26a0\ufe0f {pendingCount} QR codes need to be uploaded to device";
                    DeviceNotificationDetail = Lang.IsArabic
                        ? "\u0627\u0636\u063a\u0637 \u0647\u0646\u0627 \u0644\u0644\u0645\u0632\u0627\u0645\u0646\u0629"
                        : "Click here to sync";
                });
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowDeviceNotification = false;
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task SyncDeviceAsync()
    {
        IsDeviceOperationRunning = true;
        DeviceOperationProgress = Lang.IsArabic ? "\u062c\u0627\u0631\u064a \u0641\u062d\u0635 \u0627\u0644\u0623\u062c\u0647\u0632\u0629..." : "Checking devices...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var allDevices = (await deviceRepo.GetAllAsync()).ToList();

            if (allDevices.Count == 0)
            {
                DeviceOperationProgress = Lang.IsArabic ? "\u0644\u0627 \u062a\u0648\u062c\u062f \u0623\u062c\u0647\u0632\u0629 \u0645\u0633\u062c\u0644\u0629" : "No devices registered";
                await Task.Delay(2000);
                IsDeviceOperationRunning = false;
                return;
            }

            DeviceOperationProgress = Lang.IsArabic
                ? $"\u062c\u0627\u0631\u064a \u0641\u062d\u0635 {allDevices.Count} \u062c\u0647\u0627\u0632..."
                : $"Pinging {allDevices.Count} device(s)...";

            var onlineDevices = new List<(Device dev, DeviceInfo info)>();
            foreach (var d in allDevices)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(d.IP, 2000);
                    if (reply.Status == IPStatus.Success)
                    {
                        onlineDevices.Add((d, new DeviceInfo
                        {
                            IP = d.IP, MAC = d.MAC, SerialNumber = d.SerialNumber,
                            TCPPort = d.TCPPort, Password = d.Password,
                            Gateway = d.Gateway, SubnetMask = d.SubnetMask
                        }));
                    }
                }
                catch { }
            }

            if (onlineDevices.Count == 0)
            {
                DeviceOperationProgress = Lang.IsArabic
                    ? "\u274c \u062c\u0645\u064a\u0639 \u0627\u0644\u0623\u062c\u0647\u0632\u0629 \u063a\u064a\u0631 \u0645\u062a\u0635\u0644\u0629. \u062a\u0623\u0643\u062f \u0645\u0646 \u0627\u0644\u0627\u062a\u0635\u0627\u0644 \u0648\u062d\u0627\u0648\u0644 \u0645\u0631\u0629 \u0623\u062e\u0631\u0649"
                    : "\u274c All devices are offline. Check connection and try again.";
                await Task.Delay(3000);
                IsDeviceOperationRunning = false;
                return;
            }

            DeviceOperationProgress = Lang.IsArabic
                ? $"\u062c\u0627\u0631\u064a \u0631\u0641\u0639 \u0631\u0645\u0648\u0632 QR \u0625\u0644\u0649 {onlineDevices.Count} \u062c\u0647\u0627\u0632..."
                : $"Uploading QR codes to {onlineDevices.Count} device(s)...";

            var qrPool = scope.ServiceProvider.GetRequiredService<IQrPoolService>();
            var sdk = _serviceProvider.GetRequiredService<IAccessControlSdk>();
            var deviceInfos = onlineDevices.Select(d => d.info).ToList();

            var (uploaded, deleted, generated) = await Task.Run(() =>
                qrPool.SyncQrPoolToDeviceAsync(sdk, deviceInfos));

            DeviceOperationProgress = Lang.IsArabic
                ? $"\u2705 \u062a\u0645 \u0631\u0641\u0639 {uploaded} \u0631\u0645\u0632\u060c \u062d\u0630\u0641 {deleted}\u060c \u0625\u0646\u0634\u0627\u0621 {generated}"
                : $"\u2705 Uploaded {uploaded}, cleaned {deleted}, generated {generated}";

            ShowDeviceNotification = false;
            await Task.Delay(3000);
        }
        catch (Exception ex)
        {
            DeviceOperationProgress = Lang.IsArabic
                ? $"\u274c \u062e\u0637\u0623: {ex.Message}"
                : $"\u274c Error: {ex.Message}";
            await Task.Delay(3000);
        }
        finally
        {
            IsDeviceOperationRunning = false;
            DeviceOperationProgress = "";
        }
    }

    #region Multi-Monitor Win32

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private record MonitorRect(double Left, double Top, double Width, double Height, bool IsPrimary);

    private static List<MonitorRect> GetMonitorRects()
    {
        var results = new List<MonitorRect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var work = info.rcWork;
                bool isPrimary = (info.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY
                results.Add(new MonitorRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top, isPrimary));
            }
            return true;
        }, IntPtr.Zero);
        return results;
    }

    #endregion
}
