using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class BulkOperationDialog : Window
{
    // 0 = Freeze, 1 = Unfreeze, 2 = Extend, 3 = Upload All
    public int SelectedOperation => OperationCombo.SelectedIndex;

    // 0 = All Active, 1 = Expiring in 7 days, 2 = Expired, 3 = Frozen
    public int SelectedTarget => TargetCombo.SelectedIndex;

    public string FreezeReason => ReasonTextBox.Text.Trim();

    public int ExtendDays =>
        int.TryParse(DaysTextBox.Text.Trim(), out var d) && d > 0 ? d : 0;

    private readonly List<(CheckBox cb, int deviceId)> _deviceChecks = new();

    public List<int> SelectedDeviceIds =>
        _deviceChecks.Where(x => x.cb.IsChecked == true).Select(x => x.deviceId).ToList();

    public BulkOperationDialog(List<DeviceDto>? devices = null)
    {
        InitializeComponent();
        OperationCombo.SelectedIndex = 0;
        TargetCombo.SelectedIndex = 0;

        if (devices != null)
        {
            foreach (var device in devices)
            {
                var cb = new CheckBox
                {
                    Content = $"{device.Name} ({device.IP})",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                    FontSize = 13,
                    Margin = new Thickness(0, 3, 0, 3),
                    IsChecked = true
                };
                _deviceChecks.Add((cb, device.Id));
                DeviceCheckList.Children.Add(cb);
            }
        }
    }

    private void OperationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FreezeReasonPanel == null || ExtendDaysPanel == null || DeviceSelectPanel == null) return;

        FreezeReasonPanel.Visibility = OperationCombo.SelectedIndex == 0
            ? Visibility.Visible : Visibility.Collapsed;
        ExtendDaysPanel.Visibility = OperationCombo.SelectedIndex == 2
            ? Visibility.Visible : Visibility.Collapsed;
        DeviceSelectPanel.Visibility = OperationCombo.SelectedIndex == 3
            ? Visibility.Visible : Visibility.Collapsed;

        // Hide target combo and label for Upload All (not relevant)
        var showTarget = OperationCombo.SelectedIndex != 3;
        TargetCombo.Visibility = showTarget ? Visibility.Visible : Visibility.Collapsed;
        TargetLabel.Visibility = showTarget ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ExecuteClick(object sender, RoutedEventArgs e)
    {
        if (OperationCombo.SelectedIndex < 0)
            return;

        // For non-upload operations, require target selection
        if (OperationCombo.SelectedIndex != 3 && TargetCombo.SelectedIndex < 0)
            return;

        if (OperationCombo.SelectedIndex == 2 && ExtendDays <= 0)
        {
            CustomMessageBox.Show(
                LanguageManager.Instance.BulkExtendDaysRequired,
                LanguageManager.Instance.BulkOperations,
                MsgType.Warning, this);
            return;
        }

        if (OperationCombo.SelectedIndex == 3 && SelectedDeviceIds.Count == 0)
        {
            CustomMessageBox.Show(
                LanguageManager.Instance.BulkSelectAtLeastOneDevice,
                LanguageManager.Instance.BulkOperations,
                MsgType.Warning, this);
            return;
        }

        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
