using System.Windows;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class DiscountDialog : Window
{
    private readonly decimal _itemTotal;
    private bool _updatingFromPercent;
    private bool _updatingFromAmount;

    public decimal DiscountValue { get; private set; }

    public DiscountDialog(decimal itemTotal)
    {
        InitializeComponent();
        _itemTotal = itemTotal;
        PreviewText.Text = $"Item total: {_itemTotal:N0}";
        PercentBox.Focus();
    }

    private void PercentBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingFromAmount) return;
        _updatingFromPercent = true;

        if (decimal.TryParse(PercentBox.Text, out var pct) && pct >= 0 && pct <= 100)
        {
            var amount = Math.Round(_itemTotal * pct / 100, 0);
            AmountBox.Text = amount.ToString("N0");
            PreviewText.Text = $"{pct}% = -{amount:N0}";
        }

        _updatingFromPercent = false;
    }

    private void AmountBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingFromPercent) return;
        _updatingFromAmount = true;

        if (decimal.TryParse(AmountBox.Text, out var amount) && amount >= 0)
        {
            var pct = _itemTotal > 0 ? Math.Round(amount / _itemTotal * 100, 1) : 0;
            PercentBox.Text = pct.ToString();
            PreviewText.Text = $"{pct}% = -{amount:N0}";
        }

        _updatingFromAmount = false;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(AmountBox.Text, out var amount) && amount >= 0 && amount <= _itemTotal)
        {
            DiscountValue = amount;
            DialogResult = true;
        }
        else if (string.IsNullOrWhiteSpace(AmountBox.Text))
        {
            DiscountValue = 0;
            DialogResult = true;
        }
        else
        {
            CustomMessageBox.Show("Invalid discount amount.",
                LanguageManager.Instance.ValidationTitle, MsgType.Warning, this);
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
