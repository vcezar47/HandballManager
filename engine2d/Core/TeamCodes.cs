using System.Globalization;

namespace HandballManager.Engine2D;

/// <summary>
/// Three-letter codes for a scoreboard: "BUC", "ESB", "GYŐ".
/// </summary>
/// <remarks>
/// The game stores no short names, and a broadcast board has room for three letters, so they are
/// made from the club's name: the first word of three letters or more left once the club-type
/// words (HC, CSM, Håndbold, ŽRK and the rest) are taken out, which is nearly always the town.
/// Clubs known by something else, or carrying a sponsor's name in front of their town, are listed
/// by hand. Two clubs from the same town would still collide, so <see cref="ForMatch"/> keeps the
/// two codes of a match apart.
/// </remarks>
public static class TeamCodes
{
    /// <summary>Clubs whose code is not the first letters of their first word.</summary>
    /// <remarks>
    /// Mostly clubs with a sponsor's name in front of their town — Spain's and Poland's above all
    /// — where the first word is the sponsor's, and two clubs sharing a sponsor (Energa and Enea,
    /// two Madeiras) would otherwise be told apart by guesswork.
    /// </remarks>
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Borussia Dortmund"] = "BVB",
        ["FTC-Rail Cargo Hungaria"] = "FTC",
        ["Ferencváros"] = "FTC",
        ["MOL Esztergom"] = "ESZ",
        ["Rapid București"] = "RAP",
        ["Dinamo București"] = "DIN",
        ["SCM Universitatea Craiova"] = "CRA",
        ["Brest Bretagne Handball"] = "BRE",
        ["Paris 92"] = "P92",
        ["ESBF Besançon"] = "BES",
        ["JDA Bourgogne Dijon Handball"] = "DIJ",
        ["OGC Nice Côte d'Azur Handball"] = "NIC",
        ["Thüringer HC"] = "THC",
        ["FRISCH AUF Göppingen"] = "GÖP",
        ["ŽRK Z'dežele Celje"] = "CEL",
        ["Energa Start Elbląg"] = "ELB",
        ["Enea MKS Gniezno"] = "GNI",
        ["TAURON Ruch Szczypiorno Kalisz"] = "KAL",
        ["KPR Gminy Kobierzyce"] = "KOB",
        ["Piłka Ręczna Koszalin"] = "KOS",
        ["PGE MKS El-Volt Lublin"] = "LUB",
        ["KRASOŃ MKS Piotrcovia"] = "PIO",
        ["KGHM MKS Zagłębie Lubin"] = "ZAG",
        ["Caja Rural Aula Valladolid"] = "VAL",
        ["Super Amara Bera Bera"] = "BER",
        ["Replasa Beti-Onak"] = "BET",
        ["CICAR Lanzarote Ciudad de Arrecife"] = "LAN",
        ["AtticGo Elche"] = "ELC",
        ["KH-7 BM Granollers"] = "GRA",
        ["Mecalia Atlético Guardés"] = "GUA",
        ["Grafometal Sporting La Rioja"] = "RIO",
        ["Costa del Sol Málaga"] = "MÁL",
        ["Conservas Orbe Zendal Porriño"] = "POR",
        ["DHK Baník Most"] = "MOS",
        ["DHK Zora Olomouc"] = "OLO",
        ["TJ Sokol Písek"] = "PÍS",
        ["DHC Sokol Poruba"] = "POR",
        ["MŠK Iuventa Michalovce"] = "MIC",
        ["HK Slovan Duslo Šaľa"] = "ŠAĽ",
        ["DHB Rotweiss Thun"] = "THU",
        ["GKL Krems-Langenlois"] = "KRE",
        ["Fortes Venlo"] = "VEN",
        ["E&O"] = "E&O",
        ["OF Nea Ionia"] = "ION",
        ["CS Madeira"] = "CSM",
        ["Madeira SAD"] = "MAD",
        ["KA/Þór"] = "KA",
    };

    /// <summary>Words that say what kind of club it is, not which one.</summary>
    private static readonly HashSet<string> ClubWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "HC", "HK", "HB", "HBC", "HF", "IK", "IF", "KH", "KHF", "KC", "KS", "KPR", "MKS", "SPR", "UKS", "AZS",
        "CSM", "CS", "SCM", "ACS", "CSU", "SC", "SG", "SV", "TV", "TuS", "VfL", "BSV", "HSG", "TSV", "TSG", "DJK",
        "DHC", "DHK", "UHC", "MŠK", "SSV", "SE", "NKSE", "KKA", "Házená",
        "RK", "ŽRK", "ZRK", "ŽRKM", "GRK", "ŽORK", "HAC", "HAK", "AC", "ASC", "GS", "SK", "SKP", "TJ", "HBM",
        "Handball", "Håndbold", "Handbold", "Håndball", "Handboll", "Handbal", "Handbol", "Hándbol", "Balonmano",
        "Club", "Clube", "Team", "Damen", "Frauen", "Women", "Ladies", "Femenino", "Féminin", "ženske", "žene",
        "Kvinder", "Dam", "Damer", "Sport", "Sports", "Sporting", "Spor", "Kulübü", "Klub", "Kluby", "Asociația",
        "Association", "Union", "Unión", "Real", "AD", "CD", "CB", "BM", "SD", "GD", "UD", "VC", "AS", "ES", "US",
    };

    private static readonly char[] Separators = [' ', '-', '/', '.'];

    /// <summary>A club's code on its own.</summary>
    public static string For(string? name) => Candidates(name).FirstOrDefault() ?? "???";

    /// <summary>
    /// The two codes for a match, kept apart: if both clubs' first choice is the same, the away
    /// side falls back to its next candidate.
    /// </summary>
    public static (string Home, string Away) ForMatch(string? home, string? away)
    {
        string h = For(home);
        string a = Candidates(away).FirstOrDefault(c => c != h) ?? For(away);
        return (h, a);
    }

    private static IEnumerable<string> Candidates(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) yield break;
        if (Known.TryGetValue(name.Trim(), out var known)) yield return known;

        // Apostrophes join a word rather than split it; dots split initials, and a lone letter
        // left over ("Ž.U.R.D.") says nothing.
        var words = name.Replace("'", "").Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Count(char.IsLetter) >= 2)
            .ToList();

        // Words long enough to give three letters first: "EH Aalborg" is AAL, not EH.
        var meaningful = words.Where(w => !ClubWords.Contains(w) && w.Count(char.IsLetter) >= 3).ToList();
        if (meaningful.Count == 0) meaningful = words.Where(w => !ClubWords.Contains(w)).ToList();
        if (meaningful.Count == 0) meaningful = words;

        foreach (var word in meaningful)
            yield return Code(word);

        // Two words' initials and a third letter, as a last resort: "Nykøbing Falster" → "NFA".
        if (meaningful.Count >= 2)
            yield return Code(meaningful[0][..1] + meaningful[1]);
    }

    private static string Code(string word)
    {
        var letters = word.Where(char.IsLetterOrDigit).Take(3).ToArray();
        return new string(letters).ToUpper(CultureInfo.InvariantCulture);
    }
}
