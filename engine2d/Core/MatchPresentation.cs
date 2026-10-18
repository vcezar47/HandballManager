using HandballManager.Data;
using HandballManager.Helpers;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Engine2D;

/// <summary>The court's paint: the floor, the goal areas, the lines, and what surrounds it.</summary>
public sealed record CourtTheme(string Surround, string Floor, string GoalArea, string Lines, string NineMetre, string Accent)
{
    /// <summary>The everyday look: the app's own palette.</summary>
    public static CourtTheme App { get; } = new("#1A1A2E", "#22304F", "#2B3B63", "#EAEAEA", "#EAEAEA", "#E94560");

    /// <summary>Champions League nights: deep navy and gold, as the app's CL skin.</summary>
    public static CourtTheme ChampionsLeague { get; } = new("#0A1330", "#101C42", "#17275A", "#E3C567", "#E3C567", "#FFD700");

    /// <summary>European League: navy with orange, as the app's EL skin.</summary>
    public static CourtTheme EuropeanLeague { get; } = new("#0B1730", "#12234A", "#C85A12", "#FFFFFF", "#FF7A1A", "#FF7A1A");

    /// <summary>European Cup: midnight navy with electric blue, as the app's EC skin.</summary>
    public static CourtTheme EuropeanCup { get; } = new("#081228", "#0F2042", "#1A56B8", "#FFFFFF", "#1E78FF", "#1E78FF");

    public static CourtTheme For(string competition) => competition switch
    {
        LeagueService.ChampionsLeagueCompetition => ChampionsLeague,
        LeagueService.EuropeanLeagueCompetition => EuropeanLeague,
        LeagueService.EuropeanCupCompetition => EuropeanCup,
        _ => App,
    };
}

/// <summary>How a watched match looks: whose competition it is, its crest in the centre circle, the court and the kits.</summary>
public sealed record MatchPresentation(string Competition, string Logo, CourtTheme Theme, KitColours.MatchKits Kits)
{
    /// <summary>League, cup, supercup or European: decides the scoreboard's tag and trim.</summary>
    public CompetitionKind Kind { get; init; } = CompetitionKind.League;

    /// <summary>A cup or supercup fixture's stored round ("SemiFinal"), or null.</summary>
    public string? Round { get; init; }

    /// <summary>The scoreboard for this match, in the competition's colours.</summary>
    public ScoreboardStyle Board => ScoreboardStyle.For(Competition, Kind, Round);

    /// <summary>
    /// The crest the scoreboard carries. The EHF boards show the emblem alone, without the
    /// wordmark the centre-circle crest has; everything else uses the competition's crest.
    /// </summary>
    public string BoardCrest => Competition switch
    {
        LeagueService.ChampionsLeagueCompetition => "ehf_champions_league_crest.png",
        LeagueService.EuropeanLeagueCompetition => "ehf_european_league_crest.png",
        LeagueService.EuropeanCupCompetition => "ehf_european_cup_crest.png",
        _ => Logo,
    };

    /// <summary>
    /// Finds today's fixture between the two clubs and dresses the match for it. A European
    /// tie gets that competition's court; everything else the app's own, with the league's,
    /// cup's or supercup's crest in the middle.
    /// </summary>
    public static async Task<MatchPresentation> ResolveAsync(HandballDbContext db, Team home, Team away, DateTime date)
    {
        var kits = KitColours.Resolve(home.LogoPath, away.LogoPath);
        var day = date.Date;

        var cup = await db.CupFixtures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.HomeTeamId == home.Id && f.AwayTeamId == away.Id && !f.IsPlayed && f.ScheduledDate.Date == day);
        if (cup != null)
        {
            bool european = LeagueService.IsEuropeanCompetition(cup.CompetitionName);
            string logo = european ? CompetitionAssets.LeagueLogo(cup.CompetitionName) : CompetitionAssets.CupLogo(cup.CompetitionName);
            return new MatchPresentation(cup.CompetitionName, logo, CourtTheme.For(cup.CompetitionName), kits)
            {
                Kind = european ? CompetitionKind.European : CompetitionKind.Cup,
                Round = cup.Round,
            };
        }

        var supercup = await db.SupercupFixtures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.HomeTeamId == home.Id && f.AwayTeamId == away.Id && !f.IsPlayed && f.ScheduledDate.Date == day);
        if (supercup != null)
        {
            string logo = CompetitionAssets.SupercupLogo(supercup.CompetitionName) ?? CompetitionAssets.LeagueLogo(supercup.CompetitionName);
            return new MatchPresentation(supercup.CompetitionName, logo, CourtTheme.App, kits)
            {
                Kind = CompetitionKind.Supercup,
                Round = supercup.Round,
            };
        }

        string league = home.CompetitionName;
        return new MatchPresentation(league, CompetitionAssets.LeagueLogo(league), CourtTheme.App, kits);
    }
}
