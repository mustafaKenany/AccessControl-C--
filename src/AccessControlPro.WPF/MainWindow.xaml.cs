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

    // Discreet SuperAdmin entry: click the version label → step-up password → local POS/online overrides.
    private void VersionClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!Helpers.SuperAdminGate.RequireSuperAdmin(
                "إعدادات المزوّد (تفعيل الكاشير / الاشتراك أونلاين).\nProvider settings (enable POS / online).",
                this))
            return;
        var dlg = new SuperAdminSettingsDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void MigrationClick(object sender, RoutedEventArgs e)
    {
        // SuperAdmin-reserved: bulk data import/export can move/overwrite data, so it needs the
        // provider's step-up password (online gyms are controlled centrally; this is the offline path).
        if (!Helpers.SuperAdminGate.RequireSuperAdmin(
                "ترحيل البيانات (استيراد/تصدير) محجوز للمزوّد.\nData migration (import/export) is reserved for the provider.",
                this))
            return;

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
        var (ok, note) = SendDiagnosticsDialog.Show(this);
        if (!ok) return;

        // Disable the clicked button while uploading so the user can't fire it twice
        if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;

        try
        {
            // Capture a fresh managed-heap histogram first, so this bundle carries a snapshot of what's
            // using memory right now (works at any memory level — great for the leak hunt on demand).
            await Task.Run(() =>
            {
                try
                {
                    RollingLogFile.Append(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "heap_log.txt"),
                        HeapHistogram.CaptureTopTypes(25));
                }
                catch { }
            });

            var diag = _serviceProvider.GetRequiredService<IDiagnosticsService>();
            var result = await Task.Run(() => diag.UploadAsync("manual", note));

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
