using System.Globalization;
using System.Windows.Data;

namespace AccessControlPro.WPF.Helpers;

public class ActionLocalizedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string action) return value;
        var lang = LanguageManager.Instance;
        return action switch
        {
            "Create" => lang.ActionCreate,
            "Update" => lang.ActionUpdate,
            "Delete" => lang.ActionDelete,
            "SoftDelete" => lang.ActionSoftDelete,
            "Freeze" => lang.ActionFreeze,
            "Unfreeze" => lang.ActionUnfreeze,
            "Renew" => lang.ActionRenew,
            "SyncCard" => lang.ActionSyncCard,
            _ => action
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EntityTypeLocalizedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string entityType) return value;
        var lang = LanguageManager.Instance;
        return entityType switch
        {
            "Player" => lang.EntityPlayer,
            "AccessCard" => lang.EntityAccessCard,
            "Device" => lang.EntityDevice,
            _ => entityType
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
