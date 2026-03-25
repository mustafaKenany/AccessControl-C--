using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using Microsoft.Win32;

namespace AccessControlPro.WPF.Views;

public partial class ReceiptDialog : Window
{
    private readonly List<CartItemDto> _items;
    private readonly decimal _total;
    private readonly PaymentMethod _method;
    private readonly string? _playerName;

    public ReceiptDialog(List<CartItemDto> items, decimal total, PaymentMethod method, string? playerName = null)
    {
        InitializeComponent();

        _items = items;
        _total = total;
        _method = method;
        _playerName = playerName;

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

    private void PrintClick(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PageWidth = 280,
            ColumnWidth = 280,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Consolas")
        };

        // Header
        doc.Blocks.Add(new Paragraph(new Run("GYM"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        doc.Blocks.Add(new Paragraph(new Run(DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss")))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (!string.IsNullOrEmpty(_playerName))
        {
            doc.Blocks.Add(new Paragraph(new Run(_playerName))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32)))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10
        });

        // Items
        foreach (var item in _items)
        {
            doc.Blocks.Add(new Paragraph(new Run($"{item.ProductName}"))
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            doc.Blocks.Add(new Paragraph(new Run($"  {item.Quantity} x {item.Price:N0} = {item.Total:N0}"))
            {
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32)))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10
        });

        // Total
        doc.Blocks.Add(new Paragraph(new Run($"TOTAL: {_total:N0}"))
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4)
        });

        var methodText = _method == PaymentMethod.Cash ? "CASH" : "CARD BALANCE";
        doc.Blocks.Add(new Paragraph(new Run($"Paid: {methodText}"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 10
        });

        doc.Blocks.Add(new Paragraph(new Run("Thank you!"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        printDialog.PrintDocument(paginator, "POS Receipt");
    }

    private void SaveTextClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true) return;

        var lines = new List<string>
        {
            "GYM",
            DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss"),
            new string('-', 32)
        };

        if (!string.IsNullOrEmpty(_playerName))
            lines.Add(_playerName);

        foreach (var item in _items)
        {
            lines.Add($"{item.ProductName}");
            lines.Add($"  {item.Quantity} x {item.Price:N0} = {item.Total:N0}");
        }

        lines.Add(new string('-', 32));
        lines.Add($"TOTAL: {_total:N0}");
        lines.Add($"Paid: {(_method == PaymentMethod.Cash ? "CASH" : "CARD BALANCE")}");
        lines.Add("");
        lines.Add("Thank you!");

        File.WriteAllLines(dialog.FileName, lines);
    }
}
