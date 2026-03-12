using System.Windows.Controls;
using AccessControlPro.WPF.ViewModels;

namespace AccessControlPro.WPF.Views;

public partial class EventsView : UserControl
{
    private bool _devicesLoaded;

    public EventsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_devicesLoaded) return;
        _devicesLoaded = true;
        try
        {
            if (DataContext is EventsViewModel vm)
                await vm.LoadDevicesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EventsView] Load error: {ex.Message}");
        }
    }
}
