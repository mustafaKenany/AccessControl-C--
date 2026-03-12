using System.Windows;
using System.Windows.Controls;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class BulkOperationDialog : Window
{
    // 0 = Freeze, 1 = Unfreeze, 2 = Extend
    public int SelectedOperation => OperationCombo.SelectedIndex;

    // 0 = All Active, 1 = Expiring in 7 days, 2 = Expired, 3 = Frozen
    public int SelectedTarget => TargetCombo.SelectedIndex;

    public string FreezeReason => ReasonTextBox.Text.Trim();

    public int ExtendDays =>
        int.TryParse(DaysTextBox.Text.Trim(), out var d) && d > 0 ? d : 0;

    public BulkOperationDialog()
    {
        InitializeComponent();
        OperationCombo.SelectedIndex = 0;
        TargetCombo.SelectedIndex = 0;
    }

    private void OperationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FreezeReasonPanel == null || ExtendDaysPanel == null) return;

        FreezeReasonPanel.Visibility = OperationCombo.SelectedIndex == 0
            ? Visibility.Visible : Visibility.Collapsed;
        ExtendDaysPanel.Visibility = OperationCombo.SelectedIndex == 2
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ExecuteClick(object sender, RoutedEventArgs e)
    {
        if (OperationCombo.SelectedIndex < 0 || TargetCombo.SelectedIndex < 0)
            return;

        if (OperationCombo.SelectedIndex == 2 && ExtendDays <= 0)
        {
            CustomMessageBox.Show(
                LanguageManager.Instance.BulkExtendDaysRequired,
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
