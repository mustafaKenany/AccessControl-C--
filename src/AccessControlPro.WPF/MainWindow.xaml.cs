using System.Windows;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.ViewModels;
using AccessControlPro.WPF.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF;

public partial class MainWindow : Window
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _currentUser;
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainViewModel viewModel, IAuthService authService, CurrentUserService currentUser, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _currentUser = currentUser;
        _serviceProvider = serviceProvider;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void MainWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MigrationClick(object sender, RoutedEventArgs e)
    {
        using var scope = _serviceProvider.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        var dialog = new MigrationDialog(migrationService);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void ChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ChangePasswordDialog(_authService, _currentUser.Username!);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            CustomMessageBox.Show(
                LanguageManager.Instance.CpwPasswordChanged,
                LanguageManager.Instance.CpwChangePassword,
                MsgType.Success, this);
        }
    }

    private async void SendDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var lang = LanguageManager.Instance;
        var ok = CustomMessageBox.Confirm(lang.DiagConfirmBody, lang.DiagConfirmTitle, MsgType.Info, this);
        if (!ok) return;

        // Disable the clicked button while uploading so the user can't fire it twice
        if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;

        try
        {
            var diag = _serviceProvider.GetRequiredService<IDiagnosticsService>();
            var result = await Task.Run(() => diag.UploadAsync("manual"));

            if (result.Success)
            {
                CustomMessageBox.Show(lang.DiagUploadSuccess, lang.DiagConfirmTitle, MsgType.Success, this);
            }
            else
            {
                CustomMessageBox.Show($"{lang.DiagUploadFailed} {result.Message}", lang.DiagConfirmTitle, MsgType.Error, this);
            }
        }
        finally
        {
            if (sender is System.Windows.Controls.Button b) b.IsEnabled = true;
        }
    }
}
