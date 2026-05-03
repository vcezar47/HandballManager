using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class LeagueService
{
    private readonly HandballDbContext _db;

    public LeagueService(HandballDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeagueEntry>> GetStandingsAsync(string competitionName = "Liga Florilor")
    {
        var entries = await _db.LeagueEntries
            .Include(e => e.Team)
            .Where(e => e.CompetitionName == competitionName)
            .ToListAsync();

        var sorted = entries
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .ToList();

        // Assign rank based on sorted position
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].Rank = i + 1;

        return sorted;
    }

    public async Task<List<MatchRecord>> GetRecentResultsAsync(int count = 10)
    {
        return await _db.MatchRecords
            .OrderByDescending(m => m.PlayedOn)
            .Take(count)
            .ToListAsync();
    }

    public static int GetMaxMatchweeks(string competitionName)
    {
        if (competitionName == "NB I") return 26;
        if (competitionName == "Ligue Butagaz Énergie") return 26;
        return 22;
    }

    public static DateTime GetMatchweekDate(int matchweek, string competitionName)
    {
        var dates = GetMatchweekDates(competitionName);
        if (matchweek < 1 || matchweek > dates.Count)
            return dates.Last(); // Fallback for edge cases

        return dates[matchweek - 1];
    }

    public static int GetMatchweekForDate(DateTime date, string competitionName)
    {
        var dates = GetMatchweekDates(competitionName);
        var index = dates.FindIndex(d => d.Date == date.Date);
        return index >= 0 ? index + 1 : -1;
    }

    public static DateTime SeasonStartDate(string competitionName) => GetMatchweekDates(competitionName)[0];
    public static DateTime SeasonEndDate(string competitionName) => GetMatchweekDates(competitionName)[^1];

    // First in-game day for the management "season" (pre-season + transfer window),
    // which intentionally starts before the first league matchweek.
    public static DateTime GameSeasonStartDate => new DateTime(CurrentSeasonYear, 6, 15);
    
    public static int CurrentSeasonYear { get; private set; } = 2025;

    private static readonly Dictionary<string, List<DateTime>> _matchweekDatesCache = new();
    
    public static List<DateTime> GetMatchweekDates(string competitionName)
    {
        if (!_matchweekDatesCache.TryGetValue(competitionName, out var dates))
        {
            dates = GenerateSeasonMatchweekDates(CurrentSeasonYear, competitionName);
            _matchweekDatesCache[competitionName] = dates;
        }
        return dates;
    }

    private static List<DateTime> GenerateSeasonMatchweekDates(int seasonYear, string competitionName)
    {
        int maxMatchweeks = GetMaxMatchweeks(competitionName);
        // Season runs Sep -> late May, with a winter break (no league matches).
        var seasonStart = FirstSaturdayOnOrAfter(new DateTime(seasonYear, 9, 1));
        var seasonEnd = LastSaturdayOnOrBefore(new DateTime(seasonYear + 1, 5, 25));

        var breakStart = new DateTime(seasonYear, 12, 15);
        var breakEnd = new DateTime(seasonYear + 1, 1, 15);

        var candidates = new List<DateTime>();
        for (var d = seasonStart.Date; d <= seasonEnd.Date; d = d.AddDays(7))
        {
            if (d >= breakStart.Date && d <= breakEnd.Date)
                continue;
            candidates.Add(d);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("No candidate match dates could be generated for the season.");

        // If we ever have fewer candidates than matchweeks (shouldn't happen), fall back to earliest dates.
        if (candidates.Count <= maxMatchweeks)
            return candidates.Take(maxMatchweeks).ToList();

        // Evenly spread matchweeks across the available Saturdays so the final rounds land in late May.
        var indices = new int[maxMatchweeks];
        double step = (candidates.Count - 1) / (double)(maxMatchweeks - 1);
        for (int i = 0; i < maxMatchweeks; i++)
            indices[i] = (int)Math.Round(i * step);

        // Enforce strictly increasing indices.
        for (int i = 1; i < indices.Length; i++)
        {
            if (indices[i] <= indices[i - 1])
                indices[i] = indices[i - 1] + 1;
        }

        // If we overflow at the end, shift the whole sequence back.
        int overflow = indices[^1] - (candidates.Count - 1);
        if (overflow > 0)
        {
            for (int i = 0; i < indices.Length; i++)
                indices[i] -= overflow;
        }

        var selected = new List<DateTime>(maxMatchweeks);
        foreach (var idx in indices)
        {
            int clamped = Math.Clamp(idx, 0, candidates.Count - 1);
            selected.Add(candidates[clamped]);
        }

        return selected;
    }

    /// <summary>
    /// Returns true if the given date falls within the summer transfer window
    /// (1st of July to 30th of August, inclusive) for that calendar year.
    /// </summary>
    public static bool IsWithinSummerTransferWindow(DateTime date)
    {
        var year = date.Year;
        var start = new DateTime(year, 7, 1);
        var end = new DateTime(year, 8, 30);
        return date.Date >= start && date.Date <= end;
    }

    /// <summary>
    /// Returns true if the given date falls within the winter transfer window
    /// (1st of January to 31st of January, inclusive) for that calendar year.
    /// </summary>
    public static bool IsWithinWinterTransferWindow(DateTime date)
    {
        var year = date.Year;
        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, 1, 31);
        return date.Date >= start && date.Date <= end;
    }

    /// <summary>
    /// Returns true if the given date is in either transfer window.
    /// </summary>
    public static bool IsWithinAnyTransferWindow(DateTime date)
        => IsWithinSummerTransferWindow(date) || IsWithinWinterTransferWindow(date);
    
    public static void AdvanceToNextSeason()
    {
        CurrentSeasonYear++;
        _matchweekDatesCache.Clear(); // Invalidate cache so it regenerates for the new year
    }

    private static DateTime FirstSaturdayOnOrAfter(DateTime date)
    {
        int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        return date.Date.AddDays(daysUntilSaturday);
    }

    private static DateTime LastSaturdayOnOrBefore(DateTime date)
    {
        int daysSinceSaturday = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.Date.AddDays(-daysSinceSaturday);
    }

    public async Task<List<MatchRecord>> GetResultsForMatchweekAsync(int matchweek, string competitionName = "Liga Florilor")
    {
        return await _db.MatchRecords
            .Where(m => m.MatchweekNumber == matchweek && !m.IsCupMatch)
            .Where(m => _db.Teams.Any(t => t.Id == m.HomeTeamId && t.CompetitionName == competitionName))
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    public async Task<List<LeagueFixture>> GetFixturesForRoundAsync(int round, string competitionName = "Liga Florilor")
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        return await _db.LeagueFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.Round == round && f.CompetitionName == competitionName)
            .ToListAsync();
    }

    public async Task GenerateSeasonFixturesAsync()
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        
        var allTeams = await _db.Teams.ToListAsync();
        var competitions = allTeams.Select(t => t.CompetitionName).Distinct().ToList();

        foreach (var compName in competitions)
        {
            // Check if fixtures already exist for THIS competition and THIS season
            if (await _db.LeagueFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == compName))
                continue;

            var teams = allTeams.Where(t => t.CompetitionName == compName).ToList();
            if (teams.Count % 2 != 0) continue; 

            // Shuffle teams for randomization every season
            var rand = new Random();
            var shuffledIds = teams.Select(t => t.Id).OrderBy(_ => rand.Next()).ToList();
            
            int n = shuffledIds.Count;
            int rounds = n - 1;

            for (int round = 0; round < rounds; round++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    int homeIdx = (round + i) % (n - 1);
                    int awayIdx = (round + n - 1 - i) % (n - 1);

                    if (i == 0) awayIdx = n - 1;

                    int home = shuffledIds[homeIdx];
                    int away = shuffledIds[awayIdx];

                    // Alternate home/away for the pivot team (idx 0 in this logic)
                    // and everyone else to ensure balance
                    if (round % 2 == 1)
                    {
                        int temp = home;
                        home = away;
                        away = temp;
                    }

                    // Week (round + 1)
                    _db.LeagueFixtures.Add(new LeagueFixture { Season = season, Round = round + 1, HomeTeamId = home, AwayTeamId = away, CompetitionName = compName });
                    
                    // Construct the return match for the second half of the season
                    // Week (round + rounds + 1)
                    _db.LeagueFixtures.Add(new LeagueFixture { Season = season, Round = round + rounds + 1, HomeTeamId = away, AwayTeamId = home, CompetitionName = compName });
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(int homeTeamId, int awayTeamId, int matchweek)> GetNextFixtureAsync(int playerTeamId)
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        var next = await _db.LeagueFixtures
            .Where(f => f.Season == season && !f.IsPlayed && (f.HomeTeamId == playerTeamId || f.AwayTeamId == playerTeamId))
            .OrderBy(f => f.Round)
            .FirstOrDefaultAsync();

        if (next == null) return (-1, -1, -1);

        return (next.HomeTeamId, next.AwayTeamId, next.Round);
    }

}