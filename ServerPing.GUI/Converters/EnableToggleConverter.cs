using System.Globalization;
using System.Windows.Data;
using ServerPing.GUI.Services;

namespace ServerPing.GUI.Converters;

public class EnableToggleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true
            ? LocalizationService.Get("Main.Disable")
            : LocalizationService.Get("Main.Enable");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
