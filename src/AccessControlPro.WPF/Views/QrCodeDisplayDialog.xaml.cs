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
    /// QR payload is the pass code verbatim (plain decimal, e.g. "50001050").
    ///
    /// Earlier versions hex-encoded this on the theory that the device's "8H10D" config meant
    /// "read 8 hex characters and convert to a 10-digit decimal card number." That theory was
    /// wrong. Empirical testing on 2026-05-09 with a real Hikvision/Dnake controller showed:
    ///   - QR text "02FAF59F"  →  device reports card "2"      (reads digits, stops at first letter)
    ///   - QR text "00989681"  →  device reports card "989681"  (strips leading zeros, all digits OK)
    ///   - QR text "10000001"  →  device reports card "10000001" + door opens (uploaded as 10000001)
    /// So the device just reads the numeric digits from the QR text and uses them as the card
    /// number to look up — no hex conversion ever happens. Hex-encoding only mangled the value.
    /// </summary>
    private void GenerateQrCode()
    {
        // Encode the reader-adjusted value: when QrReaderDivisor > 1 the gate stores PassCode and the
        // QR carries PassCode × divisor, so the reader's division lands back on the stored code.
        var qrPayload = Helpers.QrReaderConfig.QrValueFor(_pass.PassCode);

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
        // Print straight to the default printer (no picker); warns if none is connected.
        var printDialog = Helpers.ThermalReceipt.DefaultPrinterOrWarn();
        if (printDialog == null) return;

        // Build print visual
        var printPanel = new StackPanel
        {
            Background = Brushes.White,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Gym header (name + phone, from cached AppSettings)
        printPanel.Children.Add(new TextBlock
        {
            Text = Helpers.GymProfile.DisplayName,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = Brushes.Black
        });
        if (!string.IsNullOrWhiteSpace(Helpers.GymProfile.Phone))
        {
            printPanel.Children.Add(new TextBlock
            {
                Text = Helpers.GymProfile.Phone,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Gray
            });
        }

        // Title
        printPanel.Children.Add(new TextBlock
        {
            Text = "QR Daily Pass",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 10),
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
