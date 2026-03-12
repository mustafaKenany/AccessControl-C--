using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using FontAwesome.WPF;

namespace AccessControlPro.WPF.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    public LoginWindow(
        IAuthService authService,
        CurrentUserService currentUser,
        IAppSettingsService? settingsService = null,
        string appName = "Access Control Pro",
        FontAwesomeIcon appIcon = FontAwesomeIcon.Shield,
        Color gradientStart = default,
        Color gradientEnd = default)
    {
        _authService = authService;
        _currentUser = currentUser;
        InitializeComponent();

        // Set app-specific identity
        AppNameText.Text = appName;
        AppIcon.Icon = appIcon;

        if (gradientStart != default && gradientEnd != default)
        {
            AppIconBorder.Background = new LinearGradientBrush(gradientStart, gradientEnd, 0);
        }

        UsernameTextBox.Focus();

        if (settingsService != null)
            _ = LoadSettingsAsync(settingsService);
    }

    private async Task LoadSettingsAsync(IAppSettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.GetSettingsAsync();

            if (!string.IsNullOrWhiteSpace(settings.CompanyName))
                CompanyNameText.Text = settings.CompanyName;

            if (!string.IsNullOrWhiteSpace(settings.GymName))
                GymNameText.Text = settings.GymName;

            // Build footer text from phone/address
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.Phone))
                parts.Add(settings.Phone);
            if (!string.IsNullOrWhiteSpace(settings.Address))
                parts.Add(settings.Address);
            if (parts.Count > 0)
                FooterText.Text = string.Join(" | ", parts);

            if (!string.IsNullOrWhiteSpace(settings.LogoPath) && System.IO.File.Exists(settings.LogoPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(settings.LogoPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                GymLogoImage.Source = bitmap;
                GymLogoPlaceholder.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // Settings load failure shouldn't block login
        }
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
                _currentUser.SetPermissions(result.User.Permissions);

                // Force password change if using default password "123456"
                if (_authService.IsDefaultPassword(result.User.PasswordHash))
                {
                    var changeDialog = new ChangePasswordDialog(_authService, result.User.Username, forced: true);
                    changeDialog.Owner = this;
                    if (changeDialog.ShowDialog() == true)
                    {
                        CustomMessageBox.Show(
                            Lang.CpwPasswordChanged,
                            Lang.CpwChangePassword,
                            MsgType.Success, this);
                    }
                }

                DialogResult = true;
            }
            else
            {
                var errorMessage = result.Error switch
                {
                    LoginError.UserNotFound => Lang.UserNotFound,
                    LoginError.WrongPassword => Lang.WrongPassword,
                    LoginError.AccountDisabled => Lang.AccountDisabled,
                    LoginError.AccountLockedOut => "Account locked. Try again in 5 minutes.",
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
