using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HandballManager.Converters;

public class AttributeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int attrValue)
        {
            Color color;
            if (attrValue >= 16)
                color = (Color)ColorConverter.ConvertFromString("#F1C40F"); // Elite (Gold)
            else if (attrValue >= 11)
                color = (Color)ColorConverter.ConvertFromString("#00B894"); // Good (Green)
            else if (attrValue >= 6)
                color = (Color)ColorConverter.ConvertFromString("#FF8C00"); // Average (Orange)
            else
                color = (Color)ColorConverter.ConvertFromString("#FF4B2B"); // Poor (Red)

            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
