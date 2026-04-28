using System;
using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class TeamLogoPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string logoPath && !string.IsNullOrEmpty(logoPath))
        {
            return $"/Assets/teamlogo/{logoPath}";
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
