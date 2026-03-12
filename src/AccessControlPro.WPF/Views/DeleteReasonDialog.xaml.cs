using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class DeleteReasonDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    public string Reason => ReasonTextBox.Text.Trim();

    public DeleteReasonDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonTextBox.Text))
        {
            CustomMessageBox.Show(Lang.DeleteReasonRequired, Lang.ValidationTitle, MsgType.Warning, this);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
