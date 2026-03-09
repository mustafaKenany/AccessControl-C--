using System.Windows;
using AccessControlPro.WPF.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DevicesViewModel _devicesViewModel;
    private readonly DoorsViewModel _doorsViewModel;
    private readonly EmployeesViewModel _employeesViewModel;
    private readonly LogsViewModel _logsViewModel;
    private readonly DeletedRecordsViewModel _deletedRecordsViewModel;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    public LanguageManager Lang => LanguageManager.Instance;

    public MainViewModel(DashboardViewModel dashboardViewModel, DevicesViewModel devicesViewModel, DoorsViewModel doorsViewModel, EmployeesViewModel employeesViewModel, LogsViewModel logsViewModel, DeletedRecordsViewModel deletedRecordsViewModel)
    {
        _dashboardViewModel = dashboardViewModel;
        _devicesViewModel = devicesViewModel;
        _doorsViewModel = doorsViewModel;
        _employeesViewModel = employeesViewModel;
        _logsViewModel = logsViewModel;
        _deletedRecordsViewModel = deletedRecordsViewModel;
        CurrentView = dashboardViewModel;
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Lang.SwitchLanguage();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;

        CurrentView = page switch
        {
            "Dashboard" => _dashboardViewModel,
            "Devices" => _devicesViewModel,
            "Doors" => _doorsViewModel,
            "Employees" => _employeesViewModel,
            "Logs" => _logsViewModel,
            "DeletedRecords" => _deletedRecordsViewModel,
            _ => CurrentView
        };
    }
}
