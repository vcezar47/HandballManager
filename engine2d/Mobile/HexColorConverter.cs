using System.Globalization;

namespace HandballManager.Mobile.Match2D;

/// <summary>"#RRGGBB" from the view model to a colour — kits, event colours, energy bars.</summary>
public sealed class HexColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string hex && hex.StartsWith('#') ? Color.FromArgb(hex) : Colors.White;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
