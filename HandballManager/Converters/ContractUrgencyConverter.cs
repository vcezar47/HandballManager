using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HandballManager.Converters;

/// <summary>
/// Returns a foreground or background brush based on contract time remaining.
/// Pass parameter="bg" for background (semi-transparent), omit for foreground.
/// </summary>
public class ContractUrgencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isBg = parameter is string p && p == "bg";

        if (value is not string text)
            return isBg ? new SolidColorBrush(Color.FromArgb(30, 34, 197, 94))
                        : new SolidColorBrush(Color.FromRgb(34, 197, 94));

        if (text == "Expired")
            return isBg ? new SolidColorBrush(Color.FromArgb(40, 239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(239, 68, 68));

        // Parse years out of e.g. "6m", "1y", "1y 3m", "2y 6m"
        int totalMonths = ParseMonths(text);

        if (totalMonths <= 6)
            return isBg ? new SolidColorBrush(Color.FromArgb(40, 239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(239, 68, 68));   // red

        if (totalMonths <= 12)
            return isBg ? new SolidColorBrush(Color.FromArgb(40, 245, 158, 11))
                        : new SolidColorBrush(Color.FromRgb(245, 158, 11));  // amber

        return isBg ? new SolidColorBrush(Color.FromArgb(30, 34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(34, 197, 94));        // green
    }

    private static int ParseMonths(string text)
    {
        int years = 0, months = 0;
        var parts = text.Split(' ');
        foreach (var part in parts)
        {
            if (part.EndsWith("y") && int.TryParse(part.TrimEnd('y'), out int y)) years = y;
            if (part.EndsWith("m") && int.TryParse(part.TrimEnd('m'), out int m)) months = m;
        }
        return years * 12 + months;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}