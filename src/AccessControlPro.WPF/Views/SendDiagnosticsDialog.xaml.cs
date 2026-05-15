using System.Windows;
using System.Windows.Input;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class SendDiagnosticsDialog : Window
{
    public string Note { get; private set; } = "";

    private SendDiagnosticsDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.Instance.FlowDirection;

        var lang = LanguageManager.Instance;
        TitleText.Text = lang.DiagConfirmTitle;
        BodyText.Text = lang.DiagConfirmBody;
        NoteLabel.Text = lang.DiagNoteLabel;
        HintText.Text = lang.DiagNoteHint;
        CancelButtonText.Text = lang.No;
        SendButtonText.Text = lang.DiagSendNow;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        Note = NoteBox.Text?.Trim() ?? "";
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Show the dialog modally. Returns (confirmed, note) — note is empty if the user
    /// didn't type anything but still chose to send.
    /// </summary>
    public static (bool ok, string note) Show(Window? owner = null)
    {
        var dlg = new SendDiagnosticsDialog();
        if (owner != null) dlg.Owner = owner;
        var ok = dlg.ShowDialog() == true;
        return (ok, dlg.Note);
    }
}
