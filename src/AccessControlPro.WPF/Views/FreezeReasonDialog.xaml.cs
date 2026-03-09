using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class FreezeReasonDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    public string Reason => ReasonTextBox.Text.Trim();

    public FreezeReasonDialog()
    {
        InitializeComponent();
        ReasonTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonTextBox.Text))
        {
            CustomMessageBox.Show(Lang.EnterFreezeReason, Lang.FreezePlayer, MsgType.Warning, this);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
