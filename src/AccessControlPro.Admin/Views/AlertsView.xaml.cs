using System.Windows.Controls;
using System.Windows.Input;
using AccessControlPro.Admin.ViewModels;

namespace AccessControlPro.Admin.Views;

public partial class AlertsView : UserControl
{
    public AlertsView() => InitializeComponent();

    private void ExpiringTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.ShowTabCommand.Execute("expiring");
    }

    private void ExpiredTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.ShowTabCommand.Execute("expired");
    }

    private void FrozenTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.ShowTabCommand.Execute("frozen");
    }

    private void LowStockTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.ShowTabCommand.Execute("lowstock");
    }

    private void ExpiringProductsTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.ShowTabCommand.Execute("expiringproducts");
    }
}
