using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class ViewCardsDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    public ViewCardsDialog(EmployeeDto employee)
    {
        // Register converters before InitializeComponent
        Resources.Add("BoolToActiveBg", new BoolToBrushConverter("#2ED47A", "#F7685B"));
        Resources.Add("BoolToActiveText", new BoolToStringConverter("Active", "Inactive"));
        Resources.Add("BoolToSyncBg", new BoolToBrushConverter("#44A1A0", "#3A3A50"));
        Resources.Add("BoolToSyncText", new BoolToStringConverter("Synced", "Not Synced"));

        InitializeComponent();

        PlayerNameText.Text = employee.FullNameEn;
        PlayerCardNoText.Text = employee.CardNo;
        PlayerSubText.Text = $"{employee.SubscriptionType} | {employee.Phone}";

        if (employee.PhotoData != null && employee.PhotoData.Length > 0)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(employee.PhotoData);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PlayerPhoto.Source = bitmap;
            }
            catch { /* ignore bad photo */ }
        }

        CardsPanel.ItemsSource = employee.Cards;
    }
}

public class BoolToBrushConverter : IValueConverter
{
    private readonly SolidColorBrush _trueBrush;
    private readonly SolidColorBrush _falseBrush;

    public BoolToBrushConverter(string trueHex, string falseHex)
    {
        _trueBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(trueHex));
        _falseBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(falseHex));
    }

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is true ? _trueBrush : _falseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStringConverter : IValueConverter
{
    private readonly string _trueText;
    private readonly string _falseText;

    public BoolToStringConverter(string trueText, string falseText)
    {
        _trueText = trueText;
        _falseText = falseText;
    }

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is true ? _trueText : _falseText;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
