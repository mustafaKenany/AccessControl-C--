using System.Windows;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Admin.ViewModels;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;

namespace AccessControlPro.Admin;

public partial class AdminMainWindow : Window
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _currentUser;

    public AdminMainWindow(AdminMainViewModel viewModel, IAuthService authService, CurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void MainWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e)
        => Close();

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
}
