using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class TypeToBoolConverter : IValueConverter, IMultiValueConverter
{
    // Single-value conversion (legacy/standard)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return IsMatch(value, parameter);
    }

    // Multi-value conversion (fixes the XAML binding issue)
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        
        var value = values[0];
        var target = values[1];

        return IsMatch(value, target);
    }

    private bool IsMatch(object value, object parameter)
    {
        if (value == null || parameter == null)
            return false;

        var valueType = value.GetType();

        // Handle single Type parameter
        if (parameter is Type type)
            return valueType == type;

        // Handle comma-separated string of type names
        if (parameter is string typeList)
        {
            var types = typeList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var tName in types)
            {
                // Try to find the type by name in the ViewModels namespace
                var fullTypeName = $"HandballManager.ViewModels.{tName}";
                var t = Type.GetType(fullTypeName);
                if (t != null && valueType == t) return true;
                
                // Fallback for types without namespace check
                if (valueType.Name == tName) return true;
            }
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
