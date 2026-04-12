using System.Globalization;
using System.Windows.Data;

namespace HandballManager.Converters;

public class NationalityToFlagConverter : IValueConverter
{
    private static readonly Dictionary<string, string> FlagMap = new()
    {
        { "ANG", "angola.png" },
        { "BLR", "belarus.png" },
        { "BIH", "bosnia.png" },
        { "BRA", "brazil.png" },
        { "CRO", "croatia.png" },
        { "DEN", "denmark.png" },
        { "FRA", "france.png" },
        { "HUN", "hungary.png" },
        { "JPN", "japan.png" },
        { "MNE", "montenegro.png" },
        { "NED", "netherlands.png" },
        { "NOR", "norway.png" },
        { "POL", "poland.png" },
        { "POR", "portugal.png" },
        { "PRT", "portugal.png" },
        { "ROU", "romania.png" },
        { "RUS", "russia.png" },
        { "SRB", "serbia.png" },
        { "SLO", "slovenia.png" },
        { "ESP", "spain.png" },
        { "SWE", "sweden.png" },
        { "TUR", "turkey.png" },
        { "UKR", "ukraine.png" },
        { "GER", "germany.png" },
        { "SUI", "switzerland.png" },
        { "MKD", "macedonia.png" },
        { "BUL", "bulgaria.png" },
        { "TUN", "tunisia.png" }
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string nationality || string.IsNullOrWhiteSpace(nationality))
            return null;

        string code = nationality.Trim().ToUpper();
        if (FlagMap.TryGetValue(code, out var filename))
        {
            return $"/Assets/flags/{filename}";
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
