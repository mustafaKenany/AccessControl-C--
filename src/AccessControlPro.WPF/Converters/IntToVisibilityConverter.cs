using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AccessControlPro.WPF.Converters;

/// <summary>
/// Converts an int value to Visibility. Visible when the value equals TargetValue, Collapsed otherwise.
/// Usage in XAML: set TargetValue property to the tab index you want visible.
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public int TargetValue { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue == TargetValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible ? TargetValue : Binding.DoNothing;
    }
}
