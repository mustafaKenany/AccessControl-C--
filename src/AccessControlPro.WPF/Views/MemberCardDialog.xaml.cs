using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;
using QRCoder;

namespace AccessControlPro.WPF.Views;

/// <summary>
/// Printable member ID card: gym name header, photo, name, card number, a QR of
/// the access card number, plus subscription/expiry/phone. Prints the card visual
/// only (buttons excluded) via PrintVisual.
/// </summary>
public partial class MemberCardDialog : Window
{
    private readonly EmployeeDto _member;

    public MemberCardDialog(EmployeeDto member)
    {
        _member = member;
        InitializeComponent();

        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        GymNameText.Text = GymProfile.DisplayName;
        NameText.Text = ar
            ? (string.IsNullOrWhiteSpace(member.FullNameAr) ? member.FullNameEn : member.FullNameAr)
            : (string.IsNullOrWhiteSpace(member.FullNameEn) ? member.FullNameAr : member.FullNameEn);

        SubLabel.Text = (ar ? "الاشتراك: " : "Plan: ");
        SubText.Text = member.SubscriptionType;
        ExpiryLabel.Text = (ar ? "ينتهي: " : "Expires: ");
        ExpiryText.Text = member.EndDate.ToString("yyyy-MM-dd");
        PhoneLabel.Text = (ar ? "الهاتف: " : "Phone: ");
        PhoneText.Text = member.Phone;
        CardNoText.Text = member.CardNo;

        LoadPhoto();
        GenerateQr(member.CardNo);
    }

    private void LoadPhoto()
    {
        try
        {
            if (_member.PhotoData is { Length: > 0 })
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(_member.PhotoData);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                PhotoImage.Source = bmp;
                PhotoPlaceholder.Visibility = Visibility.Collapsed;
            }
        }
        catch { /* keep the placeholder icon */ }
    }

    private void GenerateQr(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(10);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(qrBytes);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            QrImage.Source = bmp;
        }
        catch { /* QR is best-effort */ }
    }

    private void PrintClick(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;
        printDialog.PrintVisual(CardRoot, "Member Card - " + _member.CardNo);
    }

    private void CloseClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
