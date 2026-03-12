using System.Collections.Generic;
using System.Windows;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class ReceiptDialog : Window
{
    public ReceiptDialog(List<CartItemDto> items, decimal total, PaymentMethod method, string? playerName = null)
    {
        InitializeComponent();

        var lang = LanguageManager.Instance;

        DateText.Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        TotalText.Text = total.ToString("N0");
        PaymentText.Text = method == PaymentMethod.Cash ? lang.PosPayCash : lang.PosPayCard;
        ItemsList.ItemsSource = items;

        if (!string.IsNullOrEmpty(playerName))
        {
            PlayerText.Text = playerName;
            PlayerText.Visibility = Visibility.Visible;
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
