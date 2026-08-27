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
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Show the guided tour once, on the very first launch. After that it only runs from the
        // "?" help button. Delayed a beat so the sidebar has finished laying out (target bounds).
        if (Helpers.TourState.HasSeen()) return;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(1200) };
        t.Tick += (_, _) => { t.Stop(); StartTour(); };
        t.Start();
    }

    private void HelpTourClick(object sender, RoutedEventArgs e)
    {
        CloseSettingsPopup();
        StartTour();
    }

    private void StartTour()
    {
        Tour.Start(new System.Collections.Generic.List<Controls.CoachStep>
        {
            new()
            {
                Target = TodayNavBtn,
                TitleAr = "شاشة اليوم", TitleEn = "Today screen",
                BodyAr = "أهم شاشة: كم عضو دخل اليوم، كم اشتراك، كم فلوس، ومن ينتهي اشتراكه قريباً.",
                BodyEn = "Your main screen: entries today, subscriptions, revenue, and who's about to expire.",
            },
            new()
            {
                Target = MonitorNavBtn,
                TitleAr = "المراقبة المباشرة", TitleEn = "Live monitor",
                BodyAr = "افتحها عند البوابة لترى كل عضو يدخل مع صورته وحالة اشتراكه لحظياً.",
                BodyEn = "Open it at the gate to see each member entering with their photo and status live.",
            },
            new()
            {
                Target = PlayersNavBtn,
                TitleAr = "اللاعبين", TitleEn = "Members",
                BodyAr = "من هنا تضيف عضو جديد (جرّب زر «إضافة سريعة») وتعرض وتجدّد المشتركين.",
                BodyEn = "Add a new member here (try the “Quick Add” button), and view or renew members.",
            },
            new()
            {
                Target = DevicesNavBtn,
                TitleAr = "الأجهزة وحالة النظام", TitleEn = "Devices & status",
                BodyAr = "شاشة الأجهزة. تظهر نقطة حمراء نابضة هنا إذا كانت البوابة غير متصلة أو يحتاج النظام انتباهك.",
                BodyEn = "The Devices screen. A pulsing red dot appears here if the gate is offline or the system needs attention.",
            },
        });
    }

    private void MainWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only allow dragging when NOT maximized. The window is locked to full-screen (see
        // OnStateChanged), so this is effectively a no-op — but it stops a title-bar drag from
        // restoring the window to a small, icon-overlapping size.
        if (e.ChangedButton == MouseButton.Left && WindowState != WindowState.Maximized)
            DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    // The window is LOCKED to full-screen: any attempt to restore it to a normal (small) size snaps
    // it straight back to Maximized. Operators can only Minimize or Close now (the Maximize button was
    // removed). Deliberate — at small window sizes the layout icons overlapped and confused users.
    protected override void OnStateChanged(System.EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Normal)
            WindowState = WindowState.Maximized;
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

    // ── Settings popup (rarely-used tools grouped under one button) ──
    private void SettingsClick(object sender, RoutedEventArgs e)
        => SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

    private void CloseSettingsPopup() => SettingsPopup.IsOpen = false;

    private void LanguageClick(object sender, RoutedEventArgs e)
    {
        CloseSettingsPopup();
        Helpers.LanguageManager.Instance.SwitchLanguage();
    }

    private void MigrationClick(object sender, RoutedEventArgs e)
    {
        CloseSettingsPopup();
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
        CloseSettingsPopup();
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
        CloseSettingsPopup();
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
