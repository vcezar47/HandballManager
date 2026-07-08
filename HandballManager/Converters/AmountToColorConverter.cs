using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HandballManager.Converters;

public class AmountToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal amount)
        {
            if (amount > 0)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ECDC4")); // Greenish
            if (amount < 0)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E94560")); // Reddish
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
