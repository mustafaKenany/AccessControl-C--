using System.Windows;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly IAuthService _authService;
    private readonly string _username;
    private readonly bool _forced;

    public ChangePasswordDialog(IAuthService authService, string username, bool forced = false)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.Instance.FlowDirection;

        _authService = authService;
        _username = username;
        _forced = forced;

        var lang = LanguageManager.Instance;
        TitleText.Text = lang.CpwChangePassword;
        CurrentPasswordLabel.Text = lang.CpwCurrentPassword;
        NewPasswordLabel.Text = lang.CpwNewPassword;
        ConfirmPasswordLabel.Text = lang.CpwConfirmPassword;
        CancelButtonText.Text = lang.Cancel;
        SaveButtonText.Text = lang.UsrSave;

        if (forced)
        {
            WarningText.Text = lang.CpwDefaultPasswordWarning;
            WarningText.Visibility = Visibility.Visible;
        }

        CurrentPasswordBox.Focus();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_forced && DialogResult != true)
        {
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPassword = CurrentPasswordBox.Password;
        var newPassword = NewPasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;
        var lang = LanguageManager.Instance;

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            ShowError(lang.CpwCurrentPassword + " is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            ShowError(lang.CpwMinLength);
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError(lang.CpwPasswordMismatch);
            return;
        }

        try
        {
            await _authService.ChangePasswordAsync(_username, currentPassword, newPassword);
            DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_forced) return;
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
