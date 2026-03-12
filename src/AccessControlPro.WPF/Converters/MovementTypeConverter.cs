using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.WPF.Converters;

public class MovementTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MovementType type)
        {
            var isArabic = Helpers.LanguageManager.Instance.IsArabic;
            return type == MovementType.In
                ? (isArabic ? "وارد" : "Stock In")
                : (isArabic ? "صادر" : "Stock Out");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class MovementTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MovementType type)
            return type == MovementType.In
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A)) // green
                : new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B)); // red
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class MovementTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MovementType type)
            return type == MovementType.In ? "ArrowDown" : "ArrowUp";
        return "Question";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
