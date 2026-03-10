using System;
using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class TransferBudgetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal budget)
        {
            return Math.Round(budget * 0.70m, 0);
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class WageBudgetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal budget)
        {
            return Math.Round((budget * 0.30m) / 52m, 0);
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
