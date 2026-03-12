using System.Windows;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;

namespace AccessControlPro.WPF.Views;

public partial class ActivationWindow : Window
{
    private readonly ILicenseService _licenseService;

    public bool IsActivated { get; private set; }

    public ActivationWindow(ILicenseService licenseService, LicenseStatus status, DeveloperInfo? devInfo = null)
    {
        _licenseService = licenseService;
        InitializeComponent();

        MachineIdBox.Text = status.MachineId;

        // Show appropriate status message
        StatusText.Text = status.Message switch
        {
            "NOT_ACTIVATED" => "Software is not activated. Enter your activation code.",
            "EXPIRED" => $"License expired on {status.ExpiryDate:yyyy-MM-dd}. Contact provider for renewal.",
            "HARDWARE_CHANGED" => "Hardware change detected. Contact provider with your new Machine ID.",
            "CLOCK_TAMPER" => "System clock manipulation detected. Contact provider.",
            "INVALID_KEY" => "License key is invalid. Contact provider.",
            "CORRUPTED" => "License file is corrupted. Re-enter your activation code.",
            "INVALID_FILE" => "License file is invalid. Re-enter your activation code.",
            _ => "License validation failed. Contact provider."
        };

        // Color based on severity
        StatusText.Foreground = status.Message switch
        {
            "NOT_ACTIVATED" => new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4F, 0xC3, 0xF7)),
            _ => new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B))
        };

        // Show developer contact info
        devInfo ??= ILicenseService.LoadDeveloperInfo();
        SetupDeveloperInfo(devInfo);
    }

    private void SetupDeveloperInfo(DeveloperInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.CompanyName))
            DevCompanyText.Text = info.CompanyName;
        else
            DevCompanyText.Visibility = Visibility.Collapsed;

        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.Phone))
            contactParts.Add($"Phone: {info.Phone}");
        if (!string.IsNullOrWhiteSpace(info.WhatsApp))
            contactParts.Add($"WhatsApp: {info.WhatsApp}");
        if (!string.IsNullOrWhiteSpace(info.Email))
            contactParts.Add($"Email: {info.Email}");

        if (contactParts.Count > 0)
            DevContactText.Text = string.Join("  |  ", contactParts);
        else
            DevContactText.Text = "Contact your software provider for activation.";
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MachineIdBox.Text);
        StatusText.Text = "Machine ID copied to clipboard!";
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
    }

    private void LicenseKeyBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ShowError("Please enter an activation code.");
            return;
        }

        try
        {
            if (_licenseService.ActivateLicense(key))
            {
                IsActivated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Invalid or expired activation code. Please check and try again.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Activation failed: {ex.Message}");
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        IsActivated = false;
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
