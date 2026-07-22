using System.Globalization;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

/// <summary>
/// Team of the Week and Team of the Season.
///
/// TOTW is derived on demand from the season's stored match stats — nothing extra is
/// written, and every completed matchweek stays available. TOTS is captured once at
/// season end, because the match data it is built from is wiped for the new season.
/// </summary>
public class AwardsService(HandballDbContext db)
{
    /// <summary>The seven shirts of a handball line-up, in display order.</summary>
    public static readonly string[] Positions = ["GK", "LW", "LB", "CB", "RB", "RW", "Pivot"];

    private readonly HandballDbContext _db = db;

    /// <summary>
    /// The played windows of a competition, newest first — what the awards picker steps
    /// through. A league counts in matchweeks; cups and supercups have no matchweek of
    /// their own, so each of their matchdays is one window.
    /// </summary>
    public async Task<List<AwardPeriod>> GetAwardPeriodsAsync(string competitionName, CompetitionType type)
    {
        if (type == CompetitionType.League)
        {
            var weeks = await CompetitionRecords(competitionName, type)
                .Where(m => m.MatchweekNumber > 0)
                .Select(m => m.MatchweekNumber)
                .Distinct()
                .OrderByDescending(n => n)
                .ToListAsync();

            return weeks.Select(w => new AwardPeriod(w.ToString(), $"Matchweek {w}")).ToList();
        }

        var matchdays = await CompetitionRecords(competitionName, type)
            .Select(m => new { m.PlayedOn, m.CupRound })
            .ToListAsync();

        return matchdays
            .GroupBy(m => m.PlayedOn.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new AwardPeriod(
                g.Key.ToString("yyyy-MM-dd"),
                $"{RoundLabel(g.Select(m => m.CupRound))} · {g.Key:d MMM yyyy}"))
            .ToList();
    }

    /// <summary>
    /// Best performer per position across one window of a competition. Returns an empty
    /// list when that window has not been played.
    /// </summary>
    public async Task<List<AwardPlayer>> GetTeamOfThePeriodAsync(string competitionName, CompetitionType type, string periodKey)
    {
        var stats = _db.MatchPlayerStats
            .Where(s => s.MatchRecord != null
                        && s.Player != null
                        && s.Player.Team != null
                        && s.Player.Team.CompetitionName == competitionName);

        if (type == CompetitionType.League)
        {
            if (!int.TryParse(periodKey, out int matchweek)) return [];
            stats = stats.Where(s => !s.MatchRecord!.IsCupMatch && s.MatchRecord.MatchweekNumber == matchweek);
        }
        else
        {
            if (!DateTime.TryParse(periodKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return [];
            day = day.Date;
            stats = type == CompetitionType.Supercup
                ? stats.Where(s => s.MatchRecord!.IsCupMatch
                                   && s.MatchRecord.CupRound != null
                                   && s.MatchRecord.CupRound.StartsWith("Supercup")
                                   && s.MatchRecord.PlayedOn.Date == day)
                : stats.Where(s => s.MatchRecord!.IsCupMatch
                                   && (s.MatchRecord.CupRound == null || !s.MatchRecord.CupRound.StartsWith("Supercup"))
                                   && s.MatchRecord.PlayedOn.Date == day);
        }

        var rows = await stats
            .Select(s => new AwardPlayer(
                s.PlayerId,
                s.PlayerName,
                s.Player!.Team!.Name,
                s.Player.Team.LogoPath,
                s.Player.Position,
                s.Rating,
                1,
                s.Goals,
                s.Assists,
                s.Saves))
            .ToListAsync();

        return PickBestPerPosition(rows);
    }

    /// <summary>
    /// Played records belonging to one competition of one country. Mirrors the split
    /// <see cref="LeagueService.GetLeaderboardsAsync"/> uses, so the two agree on which
    /// match counts where.
    /// </summary>
    private IQueryable<MatchRecord> CompetitionRecords(string competitionName, CompetitionType type)
    {
        var records = _db.MatchRecords
            .Where(m => !m.IsUnplayedPlaceholder
                        && _db.Teams.Any(t => t.Id == m.HomeTeamId && t.CompetitionName == competitionName));

        return type switch
        {
            CompetitionType.League => records.Where(m => !m.IsCupMatch),
            CompetitionType.Supercup => records.Where(m => m.IsCupMatch
                                                           && m.CupRound != null
                                                           && m.CupRound.StartsWith("Supercup")),
            _ => records.Where(m => m.IsCupMatch
                                    && (m.CupRound == null || !m.CupRound.StartsWith("Supercup")))
        };
    }

    /// <summary>
    /// Names a matchday from the rounds played on it — the four cup groups all play the
    /// same day, so they collapse to one label rather than "Group A / Group B / …".
    /// </summary>
    private static string RoundLabel(IEnumerable<string?> rounds)
    {
        var named = rounds
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.StartsWith("Supercup ") ? r["Supercup ".Length..] : r)
            .Distinct()
            .ToList();

        if (named.Count == 0) return "Matchday";
        if (named.All(r => r.StartsWith("Group"))) return "Group Phase";
        return string.Join(" / ", named);
    }

    /// <summary>
    /// Builds and stores the Team of the Season for every league. Must run before
    /// <c>ProcessEndOfSeasonAsync</c> wipes the match records it reads.
    /// </summary>
    public async Task CaptureTeamsOfTheSeasonAsync(string season)
    {
        var competitions = await _db.Teams
            .Select(t => t.CompetitionName)
            .Distinct()
            .ToListAsync();

        foreach (var competition in competitions)
        {
            // Re-running the same season should not duplicate the selection.
            var existing = await _db.TeamOfTheSeasonEntries
                .Where(e => e.CompetitionName == competition && e.Season == season)
                .ToListAsync();
            if (existing.Count > 0) _db.TeamOfTheSeasonEntries.RemoveRange(existing);

            var best = await BuildTeamOfTheSeasonAsync(competition);

            foreach (var p in best)
            {
                _db.TeamOfTheSeasonEntries.Add(new TeamOfTheSeasonEntry
                {
                    CompetitionName = competition,
                    Season = season,
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    TeamName = p.TeamName,
                    Position = p.Position,
                    AverageRating = p.Rating,
                    MatchesPlayed = p.MatchesPlayed,
                    Goals = p.Goals,
                    Assists = p.Assists,
                    Saves = p.Saves
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<List<AwardPlayer>> BuildTeamOfTheSeasonAsync(string competitionName)
    {
        var stats = await _db.MatchPlayerStats
            .Where(s => s.MatchRecord != null
                        && !s.MatchRecord.IsCupMatch
                        && s.Player != null
                        && s.Player.Team != null
                        && s.Player.Team.CompetitionName == competitionName)
            .Select(s => new
            {
                s.PlayerId,
                s.PlayerName,
                TeamName = s.Player!.Team!.Name,
                TeamLogo = s.Player.Team.LogoPath,
                s.Player.Position,
                s.Rating,
                s.Goals,
                s.Assists,
                s.Saves
            })
            .ToListAsync();

        var aggregated = stats
            .GroupBy(s => s.PlayerId)
            .Select(g => new AwardPlayer(
                g.Key,
                g.First().PlayerName,
                g.First().TeamName,
                g.First().TeamLogo,
                g.First().Position,
                g.Average(s => s.Rating),
                g.Count(),
                g.Sum(s => s.Goals),
                g.Sum(s => s.Assists),
                g.Sum(s => s.Saves)))
            .ToList();

        // A blistering two-game cameo should not beat a season of consistency.
        int threshold = aggregated.Count == 0 ? 0 : Math.Max(3, aggregated.Max(p => p.MatchesPlayed) / 2);
        var eligible = aggregated.Where(p => p.MatchesPlayed >= threshold).ToList();

        // If nobody clears the bar (very short season), fall back to the full pool.
        return PickBestPerPosition(eligible.Count > 0 ? eligible : aggregated);
    }

    /// <summary>
    /// Highest rated player in each of the seven positions. Positions with no
    /// candidate are simply omitted rather than filled with someone out of position.
    /// </summary>
    private static List<AwardPlayer> PickBestPerPosition(List<AwardPlayer> candidates)
    {
        var result = new List<AwardPlayer>(Positions.Length);

        foreach (var position in Positions)
        {
            var best = candidates
                .Where(p => p.Position == position)
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.Goals + p.Assists + p.Saves)
                .FirstOrDefault();

            if (best != null) result.Add(best);
        }

        return result;
    }

    /// <summary>
    /// The archived XI for a season. The crest is looked up by club name rather than
    /// stored on the entry, so old saves get logos without a schema change.
    /// </summary>
    public async Task<List<AwardPlayer>> GetTeamOfTheSeasonAsync(string competitionName, string season)
        => await _db.TeamOfTheSeasonEntries
            .Where(e => e.CompetitionName == competitionName && e.Season == season)
            .Select(e => new AwardPlayer(e.PlayerId, e.PlayerName, e.TeamName,
                _db.Teams.Where(t => t.Name == e.TeamName).Select(t => t.LogoPath).FirstOrDefault() ?? "",
                e.Position, e.AverageRating, e.MatchesPlayed, e.Goals, e.Assists, e.Saves))
            .ToListAsync();

    /// <summary>Seasons with a stored Team of the Season, newest first.</summary>
    public async Task<List<string>> GetArchivedSeasonsAsync(string competitionName)
        => await _db.TeamOfTheSeasonEntries
            .Where(e => e.CompetitionName == competitionName)
            .Select(e => e.Season)
            .Distinct()
            .OrderByDescending(s => s)
            .ToListAsync();
}

/// <summary>
/// One selectable window of a competition. <paramref name="Key"/> is a matchweek number
/// for leagues and an ISO date for cups; only <see cref="AwardsService"/> reads it.
/// </summary>
public record AwardPeriod(string Key, string Label);

/// <summary>A single selection in a team of the week/season.</summary>
public record AwardPlayer(
    int PlayerId,
    string PlayerName,
    string TeamName,
    string TeamLogo,
    string Position,
    double Rating,
    int MatchesPlayed,
    int Goals,
    int Assists,
    int Saves)
{
    public string RatingText => Rating.ToString("F2");

    /// <summary>"12 goals · 4 assists" — keepers show saves instead.</summary>
    public string ContributionText
    {
        get
        {
            var parts = new List<string>(3);
            if (Saves > 0) parts.Add($"{Saves} saves");
            if (Goals > 0) parts.Add($"{Goals} goals");
            if (Assists > 0) parts.Add($"{Assists} assists");
            return parts.Count == 0 ? "—" : string.Join("  ·  ", parts);
        }
    }
}
