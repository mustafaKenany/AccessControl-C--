using System.Globalization;
using System.Windows.Data;

namespace AccessControlPro.WPF.Converters;

public class StockToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int stock && stock > 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
