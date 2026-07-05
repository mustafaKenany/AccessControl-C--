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

    private void SalesTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("sales");
    }

    private void PurchasesTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("purchases");
    }

    private void MovementsTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("movements");
    }

    private void SupplierTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("supplier");
    }

    private void DailyTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReportsViewModel vm) vm.SelectReportCommand.Execute("daily");
    }
}
