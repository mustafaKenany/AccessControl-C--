using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AccessControlPro.WPF.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Open" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),      // Green
            "Closed" => new SolidColorBrush(Color.FromRgb(144, 164, 174)),   // Grey
            "Alarm" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),      // Red
            "Fault" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),      // Orange
            _ => new SolidColorBrush(Color.FromRgb(144, 164, 174))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
