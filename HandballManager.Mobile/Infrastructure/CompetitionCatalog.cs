using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.Mobile;

/// <summary>
/// Name and crest for each of a country's competitions.
///
/// Every competition screen — standings/bracket, player stats, awards, history — is
/// addressed by the same pair: the league key its clubs belong to, plus which of that
/// country's three competitions is meant. This is the one place that turns that pair
/// into what the header should read.
/// </summary>
public static class CompetitionCatalog
{
    /// <summary>Only Romania and Denmark run a supercup.</summary>
    public static bool HasSupercup(string league)
        => league is "Liga Florilor" or LeagueService.KvindeligaenCompetition;

    public static (string Name, string Logo) Describe(string league, CompetitionType type) => type switch
    {
        CompetitionType.Cup => (CupName(league), CupLogo(league)),
        CompetitionType.Supercup => (SupercupName(league), SupercupLogo(league)),
        _ => (league, LeagueLogo(league))
    };

    public static string LeagueLogo(string league) => league switch
    {
        "NB I" => "nbi.png",
        "Ligue Butagaz Énergie" => "lfhdivision1.png",
        "Kvindeligaen" => "kvindeligaen.png",
        _ => "ligaflorilor.png"
    };

    public static string CupName(string league) => league switch
    {
        "NB I" => "Magyar Kupa",
        "Ligue Butagaz Énergie" => "Coupe de France",
        "Kvindeligaen" => "Landspokalturnering",
        _ => "Cupa României"
    };

    public static string CupLogo(string league) => league switch
    {
        "NB I" => "magyarkupa.png",
        "Ligue Butagaz Énergie" => "coupedefrance.png",
        "Kvindeligaen" => "santandercup.png",
        _ => "cuparomaniei.png"
    };

    public static string SupercupName(string league)
        => league == "Kvindeligaen" ? "Bambuni Supercup" : "Supercupa României";

    public static string SupercupLogo(string league)
        => league == "Kvindeligaen" ? "bambunisupercup.png" : "supercuparomaniei.png";
}
