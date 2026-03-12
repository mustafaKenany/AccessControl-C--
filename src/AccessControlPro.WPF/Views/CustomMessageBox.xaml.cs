using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AccessControlPro.WPF.Helpers;
using FontAwesome.WPF;

namespace AccessControlPro.WPF.Views;

public enum MsgType
{
    Error,
    Warning,
    Success,
    Info
}

public partial class CustomMessageBox : Window
{
    private bool _isConfirmMode;
    private bool _confirmResult;

    private CustomMessageBox(string message, string title, MsgType type, bool confirmMode = false)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.Instance.FlowDirection;
        TitleText.Text = title;
        MessageText.Text = message;
        _isConfirmMode = confirmMode;

        if (confirmMode)
        {
            OkButton.Visibility = Visibility.Collapsed;
            ConfirmPanel.Visibility = Visibility.Visible;
            YesText.Text = LanguageManager.Instance.Yes;
            NoText.Text = LanguageManager.Instance.No;
        }
        else
        {
            OkText.Text = LanguageManager.Instance.OK;
        }

        ApplyType(type);
    }

    private void ApplyType(MsgType type)
    {
        switch (type)
        {
            case MsgType.Error:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
                IconElement.Icon = FontAwesomeIcon.TimesCircle;
                IconElement.Foreground = new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
                break;
            case MsgType.Warning:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x46));
                IconElement.Icon = FontAwesomeIcon.ExclamationTriangle;
                IconElement.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x46));
                break;
            case MsgType.Success:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A));
                IconElement.Icon = FontAwesomeIcon.CheckCircle;
                IconElement.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A));
                break;
            case MsgType.Info:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0x78, 0xCD, 0xD7));
                IconElement.Icon = FontAwesomeIcon.InfoCircle;
                IconElement.Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0xCD, 0xD7));
                break;
        }
    }

    public static void Show(string message, string title, MsgType type, Window? owner = null)
    {
        var box = new CustomMessageBox(message, title, type);
        if (owner != null)
            box.Owner = owner;
        box.ShowDialog();
    }

    public static bool Confirm(string message, string title, MsgType type = MsgType.Warning, Window? owner = null)
    {
        var box = new CustomMessageBox(message, title, type, confirmMode: true);
        if (owner != null)
            box.Owner = owner;
        box.ShowDialog();
        return box._confirmResult;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
