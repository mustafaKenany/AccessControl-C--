using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class AdminMainViewModel : ObservableObject
{
    private readonly UsersViewModel _usersViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly CategoriesViewModel _categoriesViewModel;
    private readonly ProductsViewModel _productsViewModel;
    private readonly SuppliersViewModel _suppliersViewModel;
    private readonly PurchaseOrdersViewModel _purchaseOrdersViewModel;
    private readonly AdminDashboardViewModel _dashboardViewModel;
    private readonly AuditLogViewModel _auditLogViewModel;
    private readonly AlertsViewModel _alertsViewModel;
    private readonly BackupViewModel _backupViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly TimeGroupViewModel _timeGroupViewModel;
    private readonly CurrentUserService _currentUser;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    public LanguageManager Lang => LanguageManager.Instance;

    public string CurrentUserDisplayName => _currentUser.DisplayName ?? _currentUser.Username ?? "";

    public AdminMainViewModel(
        UsersViewModel usersViewModel,
        SettingsViewModel settingsViewModel,
        CategoriesViewModel categoriesViewModel,
        ProductsViewModel productsViewModel,
        SuppliersViewModel suppliersViewModel,
        PurchaseOrdersViewModel purchaseOrdersViewModel,
        AdminDashboardViewModel dashboardViewModel,
        AuditLogViewModel auditLogViewModel,
        AlertsViewModel alertsViewModel,
        BackupViewModel backupViewModel,
        ReportsViewModel reportsViewModel,
        TimeGroupViewModel timeGroupViewModel,
        CurrentUserService currentUser)
    {
        _usersViewModel = usersViewModel;
        _settingsViewModel = settingsViewModel;
        _categoriesViewModel = categoriesViewModel;
        _productsViewModel = productsViewModel;
        _suppliersViewModel = suppliersViewModel;
        _purchaseOrdersViewModel = purchaseOrdersViewModel;
        _dashboardViewModel = dashboardViewModel;
        _auditLogViewModel = auditLogViewModel;
        _alertsViewModel = alertsViewModel;
        _backupViewModel = backupViewModel;
        _reportsViewModel = reportsViewModel;
        _timeGroupViewModel = timeGroupViewModel;
        _currentUser = currentUser;
        CurrentView = dashboardViewModel;
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Lang.SwitchLanguage();
    }

    [RelayCommand]
    private async Task NavigateTo(string page)
    {
        CurrentPage = page;

        CurrentView = page switch
        {
            "Dashboard" => _dashboardViewModel,
            "Users" => _usersViewModel,
            "Settings" => _settingsViewModel,
            "Categories" => _categoriesViewModel,
            "Products" => _productsViewModel,
            "Suppliers" => _suppliersViewModel,
            "PurchaseOrders" => _purchaseOrdersViewModel,
            "AuditLog" => _auditLogViewModel,
            "Alerts" => _alertsViewModel,
            "Backup" => _backupViewModel,
            "Reports" => _reportsViewModel,
            "TimeGroups" => _timeGroupViewModel,
            _ => CurrentView
        };

        if (page == "Dashboard")
            await _dashboardViewModel.LoadAsync();
        else if (page == "Users")
            await _usersViewModel.LoadUsersAsync();
        else if (page == "Settings")
            await _settingsViewModel.LoadSettingsAsync();
        else if (page == "Categories")
            await _categoriesViewModel.LoadItemsAsync();
        else if (page == "Products")
            await _productsViewModel.LoadAsync();
        else if (page == "Suppliers")
            await _suppliersViewModel.LoadAsync();
        else if (page == "PurchaseOrders")
            await _purchaseOrdersViewModel.LoadAsync();
        else if (page == "AuditLog")
            await _auditLogViewModel.LoadAsync();
        else if (page == "Alerts")
            await _alertsViewModel.LoadAsync();
        else if (page == "Reports")
            await _reportsViewModel.LoadAsync();
        else if (page == "TimeGroups")
            await _timeGroupViewModel.LoadAsync();
    }
}
