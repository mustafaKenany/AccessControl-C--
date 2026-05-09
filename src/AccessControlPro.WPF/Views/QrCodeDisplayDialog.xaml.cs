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

    /// <summary>
    /// Converts the pass code into the format the access control device expects in the QR.
    /// The device is configured with "8H10D" QR card-number format: it reads 8 hex characters
    /// from the QR and interprets them as a decimal card number. So our numeric pool code
    /// (e.g. 50001050) needs to be embedded in the QR as its 8-char uppercase hex
    /// representation ("02FAF49A"). The device then converts back: 0x02FAF49A = 50001050,
    /// which matches the card we already uploaded — door opens.
    /// Falls back to raw text for non-numeric pass codes (legacy QR0504... format).
    /// </summary>
    private static string ToDeviceQrPayload(string passCode)
    {
        if (long.TryParse(passCode, out var numeric) && numeric >= 0 && numeric <= 0xFFFFFFFFL)
            return numeric.ToString("X8"); // 8-char uppercase hex, e.g. 50001050 -> "02FAF49A"
        return passCode;
    }

    private void GenerateQrCode()
    {
        var qrPayload = ToDeviceQrPayload(_pass.PassCode);

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);
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
