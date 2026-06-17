using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Lightweight, non-blocking corner notification (bottom-right). Unlike CustomMessageBox
/// (which is modal/ShowDialog), this never steals focus or blocks the cashier — it slides
/// in, auto-dismisses after a few seconds, and can be clicked to close. Used by the device
/// watchdog so an offline gate is surfaced without interrupting work.
/// </summary>
public static class ToastNotification
{
    /// <summary>Show a toast. Safe to call from any thread. isError=red, else teal/green.</summary>
    public static void Show(string title, string message, bool isError, int seconds = 12)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null) return;
        app.Dispatcher.Invoke(() => ShowOnUiThread(title, message, isError, seconds));
    }

    private static void ShowOnUiThread(string title, string message, bool isError, int seconds)
    {
        bool ar = LanguageManager.Instance.IsArabic;

        var accent = isError ? Color.FromRgb(0xC0, 0x39, 0x2B) : Color.FromRgb(0x1E, 0x7E, 0x4F);

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        var msgBlock = new TextBlock
        {
            Text = message,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xEC, 0xEC)),
            TextWrapping = TextWrapping.Wrap
        };
        var hint = new TextBlock
        {
            Text = ar ? "اضغط للإغلاق" : "Click to dismiss",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xCF, 0xCF)),
            Margin = new Thickness(0, 8, 0, 0)
        };

        var stack = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        stack.Children.Add(titleBlock);
        stack.Children.Add(msgBlock);
        stack.Children.Add(hint);

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x32)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(0, 0, 0, 0),
            CornerRadius = new CornerRadius(10),
            Child = stack
        };
        // Accent stripe on the leading edge
        var grid = new Grid { FlowDirection = ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var stripe = new Border
        {
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(10, 0, 0, 10)
        };
        Grid.SetColumn(stripe, 0);
        Grid.SetColumn(card, 1);
        grid.Children.Add(stripe);
        grid.Children.Add(card);

        var window = new Window
        {
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,   // don't steal focus from the cashier
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Content = grid,
            FlowDirection = ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        var wa = SystemParameters.WorkArea;
        window.Left = wa.Right - window.Width - 16;
        window.Top = wa.Bottom - 160; // refined once we know the real height
        window.Loaded += (_, _) => { window.Top = wa.Bottom - window.ActualHeight - 16; };
        window.MouseLeftButtonDown += (_, _) => { try { window.Close(); } catch { } };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) => { timer.Stop(); try { window.Close(); } catch { } };
        timer.Start();

        window.Show();
    }
}
