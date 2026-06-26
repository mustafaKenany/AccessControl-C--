using System.Windows;
using AccessControlPro.Application.Services;

namespace AccessControlPro.WPF.Views;

/// <summary>
/// SuperAdmin local overrides for an OFFLINE machine (POS + online flags). The caller must already
/// have passed the SuperAdmin step-up password before opening this. Online gyms are governed by the
/// cloud portal instead, which overwrites these on the next poll.
/// </summary>
public partial class SuperAdminSettingsDialog : Window
{
    public SuperAdminSettingsDialog()
    {
        InitializeComponent();
        PosCheck.IsChecked = FeatureFlags.IsPosEnabled();
        OnlineCheck.IsChecked = FeatureFlags.IsOnlineEnabled();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        FeatureFlags.SetLocal(posEnabled: PosCheck.IsChecked == true, onlineEnabled: OnlineCheck.IsChecked == true);
        StatusText.Text = "تم الحفظ / Saved";
        StatusText.Visibility = Visibility.Visible;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
