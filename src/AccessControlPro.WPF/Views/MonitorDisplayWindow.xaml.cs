using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;
using FontAwesome.WPF;

namespace AccessControlPro.WPF.Views;

public partial class MonitorDisplayWindow : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    private readonly DispatcherTimer _resetTimer;

    public MonitorDisplayWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Auto-reset to idle after 8 seconds
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer.Stop();
            ShowIdle();
        };

        // ESC to close display window
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        };

        Loaded += (_, _) => PositionOnSecondaryScreen();
    }

    /// <summary>
    /// Positions the window on the secondary monitor (extended display).
    /// Falls back to primary if no secondary monitor is detected.
    /// </summary>
    private void PositionOnSecondaryScreen()
    {
        var monitors = GetMonitors();
        var target = monitors.FirstOrDefault(m => !m.IsPrimary);
        if (target.Width == 0)
            target = monitors.FirstOrDefault(m => m.IsPrimary);
        if (target.Width == 0)
            return;

        WindowState = WindowState.Normal;
        Left = target.Left;
        Top = target.Top;
        Width = target.Width;
        Height = target.Height;
        WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Sets the door name displayed under the status banner.
    /// </summary>
    public void SetDoorName(string doorName)
    {
        DoorNameText.Text = doorName;
    }

    /// <summary>
    /// Shows player info when a card is swiped.
    /// </summary>
    public void ShowCardEvent(Employee employee, AccessCard card, string cardNumber, string direction = "", bool isCardExpired = false)
    {
        Dispatcher.Invoke(() =>
        {
            _resetTimer.Stop();

            bool isExpired = isCardExpired
                || employee.EndDate.Date < DateTime.Today
                || (employee.MaxVisits > 0 && employee.UsedVisits >= employee.MaxVisits);
            bool isFrozen = employee.IsFrozen;

            if (isExpired)
                SetStatus($"{Lang.DispExpiredCard}\n{direction}", "#F7685B", FontAwesomeIcon.TimesCircle);
            else if (isFrozen)
                SetStatus($"{Lang.DispFrozenCard}\n{direction}", "#FFB946", FontAwesomeIcon.PauseCircle);
            else
                SetStatus($"{Lang.DispSuccessPass}\n{direction}", "#2ED47A", FontAwesomeIcon.CheckCircle);

            // Show both names if available, or best available
            string displayName = "";
            if (!string.IsNullOrWhiteSpace(employee.FullNameAr) && !string.IsNullOrWhiteSpace(employee.FullNameEn))
                displayName = $"{employee.FullNameAr}\n{employee.FullNameEn}";
            else if (!string.IsNullOrWhiteSpace(employee.FullNameAr))
                displayName = employee.FullNameAr;
            else if (!string.IsNullOrWhiteSpace(employee.FullNameEn))
                displayName = employee.FullNameEn;
            else
                displayName = "Unknown";
            PlayerNameText.Text = displayName;

            SubLabel.Text = Lang.Subscription;
            SubValue.Text = employee.SubscriptionType;

            CardLabel.Text = Lang.CardNo;
            CardValue.Text = cardNumber;

            StartLabel.Text = Lang.StartDate;
            StartValue.Text = employee.StartDate.ToString("yyyy-MM-dd");
            EndLabel.Text = Lang.EndDate;
            EndValue.Text = employee.EndDate.ToString("yyyy-MM-dd");

            // Always show fee and paid info
            FeePanel.Visibility = Visibility.Visible;
            FeeLabel.Text = Lang.Fee;
            FeeValue.Text = $"{employee.SubscriptionFee:N0} {Lang.IQD}";

            PaidPanel.Visibility = Visibility.Visible;
            PaidLabel.Text = Lang.Paid;
            PaidValue.Text = $"{employee.AmountPaid:N0} {Lang.IQD}";

            // Show remaining balance only if there is an outstanding amount
            decimal remaining = employee.SubscriptionFee - employee.AmountPaid;
            if (remaining > 0)
            {
                BalancePanel.Visibility = Visibility.Visible;
                BalanceLabel.Text = Lang.Remaining;
                BalanceValue.Text = $"{remaining:N0} {Lang.IQD}";
            }
            else
            {
                BalancePanel.Visibility = Visibility.Collapsed;
            }

            TimeLabel.Text = Lang.Time;
            TimeValue.Text = DateTime.Now.ToString("hh:mm tt");

            SetPlayerPhoto(employee.PhotoData);

            IdlePanel.Visibility = Visibility.Collapsed;
            EventPanel.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            EventPanel.BeginAnimation(OpacityProperty, fadeIn);

            _resetTimer.Start();
        });
    }

    /// <summary>
    /// Called when an unregistered card is swiped — show "Not Registered" with gray color.
    /// </summary>
    public void ShowUnregistered()
    {
        Dispatcher.Invoke(() =>
        {
            _resetTimer.Stop();

            SetStatus(Lang.DispNotRegistered, "#8E8E93", FontAwesomeIcon.QuestionCircle);

            PlayerNameText.Text = Lang.DispNotRegistered;
            SubLabel.Text = "";
            SubValue.Text = "";
            CardLabel.Text = "";
            CardValue.Text = "";
            StartLabel.Text = "";
            StartValue.Text = "";
            EndLabel.Text = "";
            EndValue.Text = "";
            FeePanel.Visibility = Visibility.Collapsed;
            PaidPanel.Visibility = Visibility.Collapsed;
            BalancePanel.Visibility = Visibility.Collapsed;
            TimeLabel.Text = Lang.Time;
            TimeValue.Text = DateTime.Now.ToString("hh:mm tt");

            PlayerPhoto.ImageSource = null;
            NoPhotoIcon.Visibility = Visibility.Visible;

            IdlePanel.Visibility = Visibility.Collapsed;
            EventPanel.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            EventPanel.BeginAnimation(OpacityProperty, fadeIn);

            _resetTimer.Start();
        });
    }

    private void ShowIdle()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (_, _) =>
        {
            EventPanel.Visibility = Visibility.Collapsed;
            IdlePanel.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            IdlePanel.BeginAnimation(OpacityProperty, fadeIn);
        };
        EventPanel.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SetStatus(string text, string colorHex, FontAwesomeIcon icon)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);

        StatusText.Text = text;
        StatusText.Foreground = Brushes.White;
        StatusIcon.Icon = icon;
        StatusIcon.Foreground = Brushes.White;

        StatusBanner.Background = new LinearGradientBrush(
            Color.FromArgb(0x60, color.R, color.G, color.B),
            Color.FromArgb(0x20, color.R, color.G, color.B),
            0);
    }

    private void SetPlayerPhoto(byte[]? photoData)
    {
        if (photoData is { Length: > 0 })
        {
            var image = new BitmapImage();
            using var ms = new MemoryStream(photoData);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            PlayerPhoto.ImageSource = image;
            NoPhotoIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlayerPhoto.ImageSource = null;
            NoPhotoIcon.Visibility = Visibility.Visible;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _resetTimer.Stop();
        base.OnClosed(e);
    }

    #region Monitor Detection via Win32 API

    private record struct MonitorRect(int Left, int Top, int Width, int Height, bool IsPrimary);

    private static List<MonitorRect> GetMonitors()
    {
        var monitors = new List<MonitorRect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var r = info.rcMonitor;
                monitors.Add(new MonitorRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                    (info.dwFlags & 1) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    #endregion
}
