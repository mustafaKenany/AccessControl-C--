using System.Windows;
using AccessControlPro.WPF.ViewModels;

namespace AccessControlPro.WPF.Views;

public partial class MonitorWindow : Window
{
    public MonitorWindow(MonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ExitClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
        {
            if (vm.IsMonitoring)
                vm.StopMonitoringCommand.Execute(null);
            vm.CloseDisplay();
        }
        base.OnClosed(e);
    }
}
