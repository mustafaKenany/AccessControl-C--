using System.Windows;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    public LoginWindow(IAuthService authService, CurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
        InitializeComponent();
        UsernameTextBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await DoLoginAsync();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await DoLoginAsync();
    }

    private void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            PasswordBox.Focus();
    }

    private async Task DoLoginAsync()
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError(Lang.UsernameRequired);
            UsernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError(Lang.PasswordRequired);
            PasswordBox.Focus();
            return;
        }

        // Show loading state
        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;

        try
        {
            var result = await Task.Run(() => _authService.LoginAsync(username, password));

            if (result.Error == LoginError.None && result.User != null)
            {
                _currentUser.Username = result.User.Username;
                _currentUser.DisplayName = result.User.DisplayName;
                _currentUser.Role = result.User.Role;
                DialogResult = true;
            }
            else
            {
                var errorMessage = result.Error switch
                {
                    LoginError.UserNotFound => Lang.UserNotFound,
                    LoginError.WrongPassword => Lang.WrongPassword,
                    LoginError.AccountDisabled => Lang.AccountDisabled,
                    _ => Lang.LoginFailed
                };
                ShowError(errorMessage);

                if (result.Error == LoginError.WrongPassword)
                    PasswordBox.Focus();
                else
                    UsernameTextBox.Focus();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
