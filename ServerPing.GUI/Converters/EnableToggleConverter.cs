using System.Globalization;
using System.Windows.Data;

namespace ServerPing.GUI.Converters;

public class EnableToggleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "禁用" : "启用";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
