using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace HandballManager.Converters;

public class AttributeHighlightConverter : IMultiValueConverter
{
    private static readonly Dictionary<string, HashSet<string>> PrimaryAttributes = new()
    {
        { "GK", new HashSet<string> { "Reflexes", "OneOnOnes", "Positioning", "Agility", "Concentration", "Anticipation" } },
        { "LW", new HashSet<string> { "Finishing", "Acceleration", "Agility", "Flair", "Technique", "Balance" } },
        { "RW", new HashSet<string> { "Finishing", "Acceleration", "Agility", "Flair", "Technique", "Balance" } },
        { "LB", new HashSet<string> { "Finishing", "JumpingReach", "Strength", "Passing", "Decisions", "Marking" } },
        { "RB", new HashSet<string> { "Finishing", "JumpingReach", "Strength", "Passing", "Decisions", "Marking" } },
        { "CB", new HashSet<string> { "Passing", "Vision", "Decisions", "Teamwork", "Agility", "Technique" } },
        { "Pivot", new HashSet<string> { "Strength", "Balance", "Finishing", "Positioning", "Marking", "Tackling" } }
    };

    private static readonly Dictionary<string, HashSet<string>> SecondaryAttributes = new()
    {
        { "GK", new HashSet<string> { "Throwing", "Communication", "Handling", "AerialReach", "Passing", "Balance" } },
        { "LW", new HashSet<string> { "Pace", "Receiving", "Marking", "Stamina", "Anticipation", "Dribbling" } },
        { "RW", new HashSet<string> { "Pace", "Receiving", "Marking", "Stamina", "Anticipation", "Dribbling" } },
        { "LB", new HashSet<string> { "Vision", "Technique", "LongThrows", "Stamina", "Aggression", "Composure" } },
        { "RB", new HashSet<string> { "Vision", "Technique", "LongThrows", "Stamina", "Aggression", "Composure" } },
        { "CB", new HashSet<string> { "Finishing", "Dribbling", "Composure", "Acceleration", "Anticipation", "Leadership" } },
        { "Pivot", new HashSet<string> { "Aggression", "Determination", "Agility", "Teamwork", "Stamina", "Concentration" } }
    };

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Brushes.Transparent;

        string position = values[0] as string ?? string.Empty;
        string attributeName = values[1] as string ?? string.Empty;

        // Clean up attribute name to match HashSet keys
        string cleanName = attributeName.Replace(" ", "").ToLowerInvariant();
        
        // Map common XAML labels to property names used in HashSets
        string mappedName = cleanName switch
        {
            "firsttouch" => "Receiving",
            "7mtaking" => "SevenMeterTaking",
            "oneonones" => "OneOnOnes",
            "offtheball" => "OffTheBall",
            "jumpingreach" => "JumpingReach",
            "naturalfitness" => "NaturalFitness",
            _ => attributeName.Replace(" ", "") // Default to spaceless original
        };

        if (PrimaryAttributes.ContainsKey(position) && PrimaryAttributes[position].Contains(mappedName))
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3436"));
        }

        if (SecondaryAttributes.ContainsKey(position) && SecondaryAttributes[position].Contains(mappedName))
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E272E"));
        }

        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
