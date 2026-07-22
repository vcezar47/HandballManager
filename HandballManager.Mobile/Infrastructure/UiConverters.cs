using System.Globalization;
using HandballManager.Models;

namespace HandballManager.Mobile;

/// <summary>Loads packaged MauiAsset images by their asset-relative path, with caching.</summary>
public static class PackagedAssets
{
    private static readonly Dictionary<string, ImageSource> Cache = new();

    // Android packages MauiAssets under assets/Assets/...; the unpackaged Windows head
    // deploys them at their Link path under Resources\Raw (verified in the build outputs).
    private static readonly string Root = DeviceInfo.Platform == DevicePlatform.WinUI
        ? "Resources/Raw/"
        : "";

    /// <param name="assetPath">Path below the asset root, e.g. "Assets/teamlogo/Romania/csm.png".</param>
    public static ImageSource Get(string assetPath)
    {
        if (Cache.TryGetValue(assetPath, out var cached)) return cached;

        string fullPath = Root + assetPath;
        var source = ImageSource.FromStream(async ct =>
        {
            try { return await FileSystem.OpenAppPackageFileAsync(fullPath); }
            catch { return Stream.Null; }
        });
        Cache[assetPath] = source;
        return source;
    }
}

/// <summary>
/// Flattens a Core asset path ("Romania/csmbucuresti.png", "/Assets/leaguelogo/nbi.png") to the
/// lowercased filename MauiImage uses as its resource id, and resolves it to an ImageSource.
/// </summary>
public static class MauiImages
{
    private static readonly Dictionary<string, ImageSource> Cache = new();

    public static ImageSource Get(string path)
    {
        string name = System.IO.Path.GetFileName(path.Replace('\\', '/')).ToLowerInvariant();
        if (Cache.TryGetValue(name, out var cached)) return cached;
        var source = ImageSource.FromFile(name);
        Cache[name] = source;
        return source;
    }
}

/// <summary>
/// Resolves a Core LogoPath (e.g. "Romania/csmbucuresti.png") to the team-logo MauiImage.
/// Returns null for empty paths.
/// </summary>
public class TeamLogoSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string logoPath || string.IsNullOrWhiteSpace(logoPath)) return null;
        return MauiImages.Get(logoPath);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves desktop-style asset paths the Core view models expose
/// ("/Assets/leaguelogo/ligaflorilor.png", "pack://application:,,,/Assets/trophies/x.png").
/// League logos and flags are MauiImages; trophies remain raw stream assets.
/// </summary>
public class AssetImageSourceConverter : IValueConverter
{
    private const string PackPrefix = "pack://application:,,,/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        path = path.Replace('\\', '/');
        if (path.StartsWith(PackPrefix, StringComparison.OrdinalIgnoreCase))
            path = path.Substring(PackPrefix.Length);

        // Trophies and flags stay raw stream assets (see csproj notes); league logos are MauiImages.
        if (path.Contains("/trophies/", StringComparison.OrdinalIgnoreCase))
            return PackagedAssets.Get("Assets/trophies/" + System.IO.Path.GetFileName(path));
        if (path.Contains("/flags/", StringComparison.OrdinalIgnoreCase))
            return PackagedAssets.Get("Assets/flags/" + System.IO.Path.GetFileName(path));

        return MauiImages.Get(path);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves Team.StadiumImage ("Romania/lascarpana.jpg" — country-prefixed at seed time,
/// unlike the ViewModel-level "/Assets/..." paths AssetImageSourceConverter handles) to the
/// packaged stadium photo, falling back to the placeholder when a team has none set.
/// </summary>
public class StadiumImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string path = (value as string)?.Replace('\\', '/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(path)
            ? PackagedAssets.Get("Assets/stadiums/placeholderstadium.png")
            : PackagedAssets.Get("Assets/stadiums/" + path);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Colors a match rating (0–10 scale) exactly like the desktop RatingToColorConverter.</summary>
public class RatingToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not (double or float or int)) return Colors.Transparent;
        double val = System.Convert.ToDouble(value);

        string hex = val switch
        {
            >= 10 => "#0033ff",
            >= 9.0 => "#26ff3c",
            >= 8.0 => "#005c09",
            >= 7.0 => "#d4db00",
            >= 6.0 => "#db9600",
            _ => "#db1a00"
        };
        return Color.FromArgb(hex);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when values[0] (an int PlayerId) equals values[1] (a nullable int, e.g. RecentlySubstitutedPlayerId).</summary>
public class PlayerIdMatchesConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not int id) return false;
        return values[1] is int otherId && otherId == id;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a 0–100 percentage to the 0–1 range a ProgressBar expects.</summary>
public class PercentToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value switch
        {
            double d => d,
            int i => i,
            _ => 0.0
        };
        return Math.Clamp(pct / 100.0, 0.0, 1.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the bound value is null (e.g. "cup draw not yet available" empty states).</summary>
public class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True when the bound value is set — used to show single knockout fixtures.</summary>
public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Green for incoming money, red for outgoing.</summary>
public class AmountColorConverter : IValueConverter
{
    private static readonly Color Positive = Color.FromArgb("#6EE7A0");
    private static readonly Color Negative = Color.FromArgb("#E94560");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal amount && amount < 0 ? Negative : Positive;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Highlights the currently selected country tab in WorldLeaguesPage with the app's red accent.
/// Values: [0] = this tab's competition name, [1] = the page's SelectedCompetition.
/// Parameter selects which visual to return: "stroke" | "background" | "text".
/// </summary>
public class CompetitionHighlightConverter : IMultiValueConverter
{
    private static readonly Color SelectedStroke = Color.FromArgb("#E94560");
    private static readonly Color NormalStroke = Color.FromArgb("#3A5578");
    private static readonly Color SelectedBackground = Color.FromArgb("#2A1620");
    private static readonly Color SelectedText = Color.FromArgb("#E94560");
    private static readonly Color NormalText = Color.FromArgb("#EAEAEA");

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool selected = values.Length >= 2 && values[0] is string a && values[1] is string b
            && string.Equals(a, b, StringComparison.Ordinal);

        return (parameter as string) switch
        {
            "background" => selected ? SelectedBackground : Colors.Transparent,
            "text" => selected ? SelectedText : NormalText,
            _ => selected ? SelectedStroke : NormalStroke
        };
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dims a control to signal it's inactive (e.g. match-day-gated Home actions) without overriding its explicit colors.</summary>
public class BoolToDisabledOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.4;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Green while the transfer window has time left, amber inside the last week, red on the closing day.</summary>
public class WindowUrgencyColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            TransferWindowUrgency.LastDay => Color.FromArgb("#E94560"),
            TransferWindowUrgency.ClosingSoon => Color.FromArgb("#F0A500"),
            TransferWindowUrgency.Open => Color.FromArgb("#3DDC97"),
            _ => Color.FromArgb("#8888AA")
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a NewsItem.NewsType to its icon (mirrors the desktop NewsView).</summary>
public class NewsTypeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string icon = value as string switch
        {
            "Transfer" => "transfer.png",
            "RetirementAnnouncement" => "retirement.png",
            _ => "clipboard.png"
        };
        return MauiImages.Get(icon);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
