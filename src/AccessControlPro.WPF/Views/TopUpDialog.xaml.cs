using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class TopUpDialog : Window
{
    public decimal Amount { get; private set; }

    public TopUpDialog()
    {
        InitializeComponent();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(AmountBox.Text, out var amount) && amount > 0)
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
