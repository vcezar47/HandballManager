using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class EqualityToBoolConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        
        var val1 = values[0];
        var val2 = values[1];

        if (val1 == null && val2 == null) return true;
        if (val1 == null || val2 == null) return false;

        return val1.Equals(val2);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
