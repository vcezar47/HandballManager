using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HandballManager.Converters;

public class RatingToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double rating && value is not float && value is not int)
            return Brushes.Transparent;

        double val = System.Convert.ToDouble(value);

        // User requested color map
        string hex;
        if (val >= 10) hex = "#0033ff";      // perfect 10
        else if (val >= 9.0) hex = "#26ff3c"; // 9-9.9
        else if (val >= 8.0) hex = "#005c09"; // 8-8.9
        else if (val >= 7.0) hex = "#d4db00"; // 7-7.9
        else if (val >= 6.0) hex = "#db9600"; // 6-6.9
        else hex = "#db1a00";                // anything below 6

        return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
