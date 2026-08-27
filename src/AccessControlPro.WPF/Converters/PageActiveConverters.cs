using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AccessControlPro.WPF.Converters;

/// <summary>
/// True/Visible when the current page (values[0]) equals this item's page tag (values[1]).
/// Powers the "active" underline on the top-bar nav items without a style-per-item.
/// </summary>
public class PageActiveToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is { Length: >= 2 } && values[0]?.ToString() == values[1]?.ToString()
            && !string.IsNullOrEmpty(values[1]?.ToString()))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Accent brush when the current page matches this item's tag, muted otherwise — for the
/// top-bar nav item text/icon color.</summary>
public class PageActiveToBrushConverter : IMultiValueConverter
{
    private static readonly Brush Active = Freeze("#FFFFFF");
    private static readonly Brush Inactive = Freeze("#9FB4BD");
    private static Brush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is { Length: >= 2 } && values[0]?.ToString() == values[1]?.ToString()
            && !string.IsNullOrEmpty(values[1]?.ToString()))
            return Active;
        return Inactive;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
