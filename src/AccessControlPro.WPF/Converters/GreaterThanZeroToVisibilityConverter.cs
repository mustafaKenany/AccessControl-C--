using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AccessControlPro.WPF.Converters;

/// <summary>
/// Shows element when a numeric value is greater than zero. Works with int, decimal, double.
/// </summary>
public class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is decimal d) return d > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is double db) return db > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
