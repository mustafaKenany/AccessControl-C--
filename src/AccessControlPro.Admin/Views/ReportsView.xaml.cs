using System.Windows.Controls;
using System.Windows.Input;
using AccessControlPro.Admin.ViewModels;

namespace AccessControlPro.Admin.Views;

public partial class ReportsView : UserControl
{
    public ReportsView() => InitializeComponent();

    private void PlayersTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("players");
    }

    private void FinanceTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("finance");
    }

    private void InventoryTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("inventory");
    }
}
