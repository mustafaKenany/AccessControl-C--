using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.DTOs;
using QRCoder;

namespace AccessControlPro.WPF.Views;

public partial class QrCodeDisplayDialog : Window
{
    private readonly QrPassDto _pass;

    public QrCodeDisplayDialog(QrPassDto pass)
    {
        InitializeComponent();
        _pass = pass;
        GenerateQrCode();
        DisplayPassInfo();
    }

    private void GenerateQrCode()
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(_pass.PassCode, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = new MemoryStream(qrBytes);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        QrImage.Source = bitmap;
    }

    private void DisplayPassInfo()
    {
        PassCodeText.Text = _pass.PassCode;
        PlayerInfoText.Text = $"{_pass.PlayerName} | {_pass.Phone}";
        ValidityText.Text = $"{_pass.ValidFrom:yyyy-MM-dd} → {_pass.ValidTo:yyyy-MM-dd} | Max: {_pass.MaxUses} uses";
    }

    private void PrintClick(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        // Build print visual
        var printPanel = new StackPanel
        {
            Background = Brushes.White,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Title
        printPanel.Children.Add(new TextBlock
        {
            Text = "QR Daily Pass",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 10),
            Foreground = Brushes.Black
        });

        // QR Image
        var qrImg = new Image
        {
            Source = QrImage.Source,
            Width = 180,
            Height = 180,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        printPanel.Children.Add(qrImg);

        // Pass Code
        printPanel.Children.Add(new TextBlock
        {
            Text = _pass.PassCode,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.Black
        });

        // Player name
        printPanel.Children.Add(new TextBlock
        {
            Text = _pass.PlayerName,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = Brushes.Black
        });

        // Validity
        printPanel.Children.Add(new TextBlock
        {
            Text = $"Valid: {_pass.ValidFrom:yyyy-MM-dd} to {_pass.ValidTo:yyyy-MM-dd}",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = Brushes.DarkGray
        });

        // Max uses
        printPanel.Children.Add(new TextBlock
        {
            Text = $"Max Uses: {_pass.MaxUses}",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 10),
            Foreground = Brushes.DarkGray
        });

        printPanel.Measure(new Size(300, double.PositiveInfinity));
        printPanel.Arrange(new Rect(printPanel.DesiredSize));

        printDialog.PrintVisual(printPanel, "QR Pass - " + _pass.PassCode);

        CustomMessageBox.Show("QR Pass printed successfully", "Print", MsgType.Success, this);
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
