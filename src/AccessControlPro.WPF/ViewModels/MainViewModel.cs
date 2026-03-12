using System.Runtime.InteropServices;
using System.Windows;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Enums;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly CurrentUserService _currentUser;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    public LanguageManager Lang => LanguageManager.Instance;

    public string CurrentUserDisplayName => _currentUser.DisplayName ?? _currentUser.Username ?? "";

    // Permission-based visibility for sidebar navigation
    public bool CanViewDevices => _currentUser.HasPermission(AppPermission.DevicesView);
    public bool CanViewDoors => _currentUser.HasPermission(AppPermission.DoorsView);
    public bool CanViewPlayers => _currentUser.HasPermission(AppPermission.PlayersView);
    public bool CanViewEvents => _currentUser.HasPermission(AppPermission.EventsView);
    public bool CanViewFinance => _currentUser.HasPermission(AppPermission.FinanceView);
    public bool CanViewCashFlow => _currentUser.HasPermission(AppPermission.CashFlowView);
    public bool CanViewLogs => _currentUser.HasPermission(AppPermission.LogsView);
    public bool CanViewDeletedRecords => _currentUser.HasPermission(AppPermission.DeletedRecordsView);
    public bool CanViewMonitor => _currentUser.HasPermission(AppPermission.MonitorView);

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
        IServiceProvider serviceProvider,
        CurrentUserService currentUser)
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
        _serviceProvider = serviceProvider;
        _currentUser = currentUser;
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
            "Devices" => CanViewDevices,
            "Doors" => CanViewDoors,
            "Employees" => CanViewPlayers,
            "Events" => CanViewEvents,
            "Finance" => CanViewFinance,
            "CashFlow" => CanViewCashFlow,
            "Logs" => CanViewLogs,
            "DeletedRecords" => CanViewDeletedRecords,
            _ => true // Dashboard always allowed
        };

        if (!allowed) return;

        CurrentPage = page;

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
        }
    }

    [RelayCommand]
    private void OpenMonitor()
    {
        if (!CanViewMonitor) return;
        var monitorVm = _serviceProvider.GetRequiredService<MonitorViewModel>();
        var monitorWindow = new MonitorWindow(monitorVm);

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
