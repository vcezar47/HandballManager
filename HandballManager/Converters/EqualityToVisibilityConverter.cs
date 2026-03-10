using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HandballManager.Converters;

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isEqual = value?.ToString() == parameter?.ToString();
        return isEqual ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
