using System;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using HandballManager.Models;

namespace HandballManager.Converters;

/// <summary>
/// Converts a ProgressionPhase enum value to the pack URI of the corresponding arrow image.
/// Returns null for Neutral (so no image is displayed).
/// </summary>
public class ProgressionPhaseToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProgressionPhase phase)
        {
            return phase switch
            {
                ProgressionPhase.FastProgression   => "/Assets/visuals/prog90.png",
                ProgressionPhase.SlightProgression => "/Assets/visuals/prog45.png",
                ProgressionPhase.SlightRegression  => "/Assets/visuals/reg45.png",
                ProgressionPhase.FastRegression    => "/Assets/visuals/reg90.png",
                _ => null
            };
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a ProgressionPhase to Visibility.
/// Visible for any non-Neutral phase, Collapsed for Neutral.
/// </summary>
public class ProgressionPhaseToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProgressionPhase phase && phase != ProgressionPhase.Neutral)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts SeasonAttributeChangesJson + ConverterParameter (attribute name) 
/// to the correct arrow icon path.
/// Usage: Source="{Binding Player.SeasonAttributeChangesJson, 
///        Converter={StaticResource AttrTrendIconConverter}, ConverterParameter=Finishing}"
/// </summary>
public class AttributeTrendToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string json || parameter is not string attrName)
            return null;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (dict == null || !dict.TryGetValue(attrName, out int change) || change == 0)
                return null;

            return change switch
            {
                >= 2  => "/Assets/visuals/prog90.png",
                >= 1  => "/Assets/visuals/prog45.png",
                <= -2 => "/Assets/visuals/reg90.png",
                <= -1 => "/Assets/visuals/reg45.png",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts SeasonAttributeChangesJson + ConverterParameter (attribute name)
/// to Visibility (Visible if there's a non-zero change, Collapsed otherwise).
/// </summary>
public class AttributeTrendToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string json || parameter is not string attrName)
            return Visibility.Collapsed;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (dict != null && dict.TryGetValue(attrName, out int change) && change != 0)
                return Visibility.Visible;
        }
        catch { }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
