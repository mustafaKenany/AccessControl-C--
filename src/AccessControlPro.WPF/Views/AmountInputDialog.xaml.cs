using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class AmountInputDialog : Window
{
    public decimal Amount { get; private set; }

    public AmountInputDialog(string title, string prompt)
    {
        InitializeComponent();
        TitleText.Text = title;
        PromptText.Text = prompt;
        AmountBox.Focus();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(AmountBox.Text, out var amount) && amount >= 0)
        {
            Amount = amount;
            DialogResult = true;
        }
        else
        {
            CustomMessageBox.Show(LanguageManager.Instance.FeeRequired,
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
