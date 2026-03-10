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

    public async Task<List<LeagueEntry>> GetStandingsAsync()
    {
        var entries = await _db.LeagueEntries
            .Include(e => e.Team)
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

    public const int MaxMatchweeks = 22;

    public static DateTime GetMatchweekDate(int matchweek)
    {
        var dates = MatchweekDates;
        if (matchweek < 1 || matchweek > dates.Count)
            throw new ArgumentOutOfRangeException(nameof(matchweek), matchweek, $"Matchweek must be between 1 and {dates.Count}.");

        return dates[matchweek - 1];
    }

    public static DateTime SeasonStartDate => MatchweekDates[0];
    public static DateTime SeasonEndDate => MatchweekDates[^1];

    // First in-game day for the management "season" (pre-season + transfer window),
    // which intentionally starts before the first league matchweek.
    public static DateTime GameSeasonStartDate => new DateTime(CurrentSeasonYear, 6, 15);
    
    public static int CurrentSeasonYear { get; private set; } = 2025;

    private static List<DateTime>? _matchweekDatesCache;
    public static List<DateTime> MatchweekDates 
    {
        get
        {
            if (_matchweekDatesCache == null)
            {
                _matchweekDatesCache = GenerateSeasonMatchweekDates(CurrentSeasonYear);
            }
            return _matchweekDatesCache;
        }
    }

    private static List<DateTime> GenerateSeasonMatchweekDates(int seasonYear)
    {
        // Season runs Sep -> late May, with a winter break (no league matches).
        var seasonStart = FirstSaturdayOnOrAfter(new DateTime(seasonYear, 9, 1));
        var seasonEnd = LastSaturdayOnOrBefore(new DateTime(seasonYear + 1, 5, 31));

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
        if (candidates.Count <= MaxMatchweeks)
            return candidates.Take(MaxMatchweeks).ToList();

        // Evenly spread matchweeks across the available Saturdays so the final rounds land in late May.
        var indices = new int[MaxMatchweeks];
        double step = (candidates.Count - 1) / (double)(MaxMatchweeks - 1);
        for (int i = 0; i < MaxMatchweeks; i++)
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

        var selected = new List<DateTime>(MaxMatchweeks);
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
        _matchweekDatesCache = null; // Invalidate cache so it regenerates for the new year
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

    public async Task<List<MatchRecord>> GetResultsForMatchweekAsync(int matchweek)
    {
        return await _db.MatchRecords
            .Where(m => m.MatchweekNumber == matchweek)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    public async Task<(int homeTeamId, int awayTeamId, int matchweek)> GetNextFixtureAsync(int playerTeamId)
    {
        int played = await _db.MatchRecords
            .Where(m => m.HomeTeamId == playerTeamId || m.AwayTeamId == playerTeamId)
            .CountAsync();

        int nextMatchweek = played + 1;

        if (nextMatchweek > MaxMatchweeks)
            return (-1, -1, -1);

        var teams = await _db.Teams.Select(t => t.Id).ToListAsync();
        var pairings = GetPairingsForMatchweek(teams, nextMatchweek);
        var playerPairing = pairings.First(p => p.HomeId == playerTeamId || p.AwayId == playerTeamId);

        return (playerPairing.HomeId, playerPairing.AwayId, nextMatchweek);
    }

    public static List<(int HomeId, int AwayId)> GetPairingsForMatchweek(List<int> teamIds, int matchweek)
    {
        int n = teamIds.Count;
        if (n % 2 != 0) return [];

        var list = new List<int>(teamIds);
        int rounds = (n - 1) * 2;
        int currentRound = (matchweek - 1) % rounds;

        bool secondHalf = currentRound >= (n - 1);
        int rotation = currentRound % (n - 1);

        var rotating = list.GetRange(1, n - 1);
        for (int i = 0; i < rotation; i++)
        {
            var first = rotating[0];
            rotating.RemoveAt(0);
            rotating.Add(first);
        }

        var pivoted = new List<int> { list[0] };
        pivoted.AddRange(rotating);

        var pairings = new List<(int, int)>();
        for (int i = 0; i < n / 2; i++)
        {
            int home = pivoted[i];
            int away = pivoted[n - 1 - i];

            if (secondHalf)
                pairings.Add((away, home));
            else
                pairings.Add((home, away));
        }

        return pairings;
    }
}