namespace HandballManager.Engine2D;

/// <summary>
/// What the two sides wear on the 2D court. The colours come from each club's logo (see
/// tools/kit_colours.py); a clash is settled the way a referee settles it, by sending the away
/// side out in its second kit.
/// </summary>
public static partial class KitColours
{
    /// <summary>
    /// Hand corrections, keyed like the generated table ("Country/logo.png"), for the clubs whose
    /// badge is not in the colours they play in. These win over the logo.
    /// </summary>
    private static readonly Dictionary<string, (string Primary, string Secondary)> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Partizan's crest carries a red star and flame, which the reader takes for the club's
        // colour; Partizan play in black and white.
        ["Serbia/partizan.png"] = ("#111111", "#FFFFFF"),
        // Naisa's crest is a white figure outlined in yellow on transparency, so the reader lands
        // on a washed-out cream. Yellow with a dark second kit is what the crest says.
        ["Serbia/zrk_naisa.png"] = ("#F2E300", "#1A1A4E"),
        // Mladost's second colour came out pink from the red-and-white border's anti-aliasing.
        ["Serbia/mladost.png"] = ("#EC1F2A", "#FFFFFF"),
        // The same anti-aliasing turned Atzgersdorf's and Dornbirn's white into pink.
        ["Austria/wat_atzgersdorf.png"] = ("#E11E24", "#FFFFFF"),
        ["Austria/dornbirn.png"] = ("#932929", "#FFFFFF"),
        // Benfica's crest is mostly the gold eagle and wheel, so the reader lands on beige;
        // Benfica play in red and white.
        ["Portugal/benfica.png"] = ("#E3001B", "#FFFFFF"),
        // The crests read grey or brown; the OXE roster gives the clubs' colours as yellow and
        // blue (Kamatero), white, blue and green (Nea Ionia) and red and blue (Panellinios).
        ["Greece/gas_kamaterou.png"] = ("#FFD200", "#1F3C88"),
        ["Greece/ofn_ionias.png"] = ("#1D4E9E", "#FFFFFF"),
        ["Greece/panellinios.png"] = ("#C8102E", "#0F1552"),
        // Iceland: Fram's crest is gold on blue but the team plays in blue, and Haukar's badge
        // is mostly blue while the club is the red of Hafnarfjörður.
        ["Iceland/fram.png"] = ("#175DA2", "#FFFFFF"),
        ["Iceland/haukar.png"] = ("#D14434", "#FFFFFF"),
    };

    private const string DefaultPrimary = "#3355AA";
    private const string DefaultSecondary = "#FFFFFF";

    /// <summary>Keeper shirts: loud, and chosen to stand apart from both outfield kits.</summary>
    private static readonly string[] KeeperPalette = ["#C6FF00", "#FF8F00", "#00E5FF", "#E040FB", "#FFEA00", "#FF1744"];

    public sealed record Kit(string Shirt, string Number);

    public sealed record MatchKits(Kit Home, Kit Away, Kit HomeKeeper, Kit AwayKeeper);

    public static (string Primary, string Secondary) For(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath)) return (DefaultPrimary, DefaultSecondary);
        if (Overrides.TryGetValue(logoPath, out var o) || FromLogos.TryGetValue(logoPath, out o)) return o;

        // Some logo paths are stored without their country folder; match on the file alone.
        string file = Path.GetFileName(logoPath);
        foreach (var (key, value) in Overrides) if (Path.GetFileName(key).Equals(file, StringComparison.OrdinalIgnoreCase)) return value;
        foreach (var (key, value) in FromLogos) if (Path.GetFileName(key).Equals(file, StringComparison.OrdinalIgnoreCase)) return value;
        return (DefaultPrimary, DefaultSecondary);
    }

    /// <summary>The four kits for a match: home in first colours, away in whichever does not clash.</summary>
    public static MatchKits Resolve(string? homeLogo, string? awayLogo)
    {
        var (hp, _) = For(homeLogo);
        var (ap, asec) = For(awayLogo);

        string away = ap;
        if (Distance(hp, ap) < ClashDistance)
        {
            away = Distance(hp, asec) >= ClashDistance ? asec
                 : Distance(hp, "#FFFFFF") > Distance(hp, "#1A1A2E") ? "#FFFFFF" : "#1A1A2E";
        }

        var homeKeeper = KeeperPalette.OrderByDescending(k => Math.Min(Distance(k, hp), Distance(k, away))).First();
        var awayKeeper = KeeperPalette.Where(k => k != homeKeeper)
            .OrderByDescending(k => Math.Min(Math.Min(Distance(k, hp), Distance(k, away)), Distance(k, homeKeeper))).First();

        return new MatchKits(Dress(hp), Dress(away), Dress(homeKeeper), Dress(awayKeeper));
    }

    /// <summary>How far apart two colours must be to tell the sides apart at a glance, in weighted RGB.</summary>
    private const double ClashDistance = 60;

    private static Kit Dress(string shirt) => new(shirt, Luminance(shirt) > 0.55 ? "#111111" : "#FFFFFF");

    private static (int R, int G, int B) Rgb(string hex)
    {
        hex = hex.TrimStart('#');
        return (Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16));
    }

    /// <summary>A "redmean" colour distance: cheap, and much closer to what the eye sees than plain RGB.</summary>
    public static double Distance(string a, string b)
    {
        var (r1, g1, b1) = Rgb(a);
        var (r2, g2, b2) = Rgb(b);
        double rm = (r1 + r2) / 2.0;
        int dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
        return Math.Sqrt((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db) / 3.0;
    }

    public static double Luminance(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
    }
}
