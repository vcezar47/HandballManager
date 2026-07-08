using System;
using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class LevelToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentLevel && int.TryParse(parameter?.ToString(), out int dotLevel))
        {
            return currentLevel >= dotLevel ? 1.0 : 0.15;
        }
        return 0.15;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
