using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class EditReasonDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    public string Reason => ReasonTextBox.Text.Trim();

    public EditReasonDialog()
    {
        InitializeComponent();
        ReasonTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonTextBox.Text))
        {
            CustomMessageBox.Show(Lang.EditReasonRequired, Lang.EditReason, MsgType.Warning, this);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
