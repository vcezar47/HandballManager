using System;
using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class StadiumImagePathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string imagePath && !string.IsNullOrEmpty(imagePath))
        {
            // First check the Romania subfolder as all our current stadiums are there
            return $"/Assets/stadiums/{imagePath}";
        }
        
        // Return placeholder if nothing else works
        return "/Assets/stadiums/placeholderstadium.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
