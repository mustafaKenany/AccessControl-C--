using System.Windows;
using System.Windows.Controls;

namespace AccessControlPro.WPF.Views;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
    }

    // Open the per-device "⋯ More" overflow menu on LEFT-click (WPF context menus are right-click by
    // default). PlacementTarget is set so the menu items can bind back to the row's VM + device.
    private void DeviceMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.ContextMenu != null)
        {
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.IsOpen = true;
        }
    }
}
