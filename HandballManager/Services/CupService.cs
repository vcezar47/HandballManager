using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class CupService
{
    private readonly HandballDbContext _db;
    private readonly Random _rng = new();

    public CupService(HandballDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Draws groups and schedules all cup fixtures for a season.
    /// Call once at the start of each season.
    /// </summary>
    public async Task GenerateCupAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        // Don't regenerate if already exists for this season
        if (await _db.CupGroups.AnyAsync(g => g.Season == season))
            return;

        var teamIds = await _db.Teams.Select(t => t.Id).ToListAsync();

        // Shuffle teams randomly
        var shuffled = teamIds.OrderBy(_ => _rng.Next()).ToList();

        // Create 4 groups of 3
        string[] groupNames = ["A", "B", "C", "D"];
        var groups = new List<CupGroup>();

        for (int g = 0; g < 4; g++)
        {
            var group = new CupGroup
            {
                GroupName = groupNames[g],
                Season = season
            };

            var groupTeams = shuffled.Skip(g * 3).Take(3).ToList();
            foreach (var tid in groupTeams)
            {
                group.Entries.Add(new CupGroupEntry { TeamId = tid });
            }

            groups.Add(group);
            _db.CupGroups.Add(group);
        }

        await _db.SaveChangesAsync();

        // Schedule group fixtures (3 per group = round-robin among 3 teams)
        var groupDates = GetGroupStageDates();
        foreach (var group in groups)
        {
            var teamIdsInGroup = group.Entries.Select(e => e.TeamId).ToList();
            // 3 teams → 3 matches: 0v1, 0v2, 1v2
            var pairings = new List<(int Home, int Away)>
            {
                (teamIdsInGroup[0], teamIdsInGroup[1]),
                (teamIdsInGroup[0], teamIdsInGroup[2]),
                (teamIdsInGroup[1], teamIdsInGroup[2])
            };

            // Shuffle pairings so matchday assignment is random
            pairings = pairings.OrderBy(_ => _rng.Next()).ToList();

            for (int i = 0; i < 3; i++)
            {
                // Randomly flip home/away
                var (home, away) = _rng.NextDouble() < 0.5
                    ? pairings[i]
                    : (pairings[i].Away, pairings[i].Home);

                _db.CupFixtures.Add(new CupFixture
                {
                    CupGroupId = group.Id,
                    Season = season,
                    HomeTeamId = home,
                    AwayTeamId = away,
                    ScheduledDate = groupDates[i],
                    Round = "Group"
                });
            }
        }

        // Schedule quarter-final date (actual matchups determined after group stage)
        var qfDate = GetQuarterFinalDate();
        // We'll create QF fixture shells once groups are decided.
        // For now just store the date reference on the service.

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Finds 3 Wednesdays before the winter break that have ≥3-day gaps from league fixtures.
    /// </summary>
    public static List<DateTime> GetGroupStageDates()
    {
        var leagueDates = LeagueService.MatchweekDates;
        var currentYear = LeagueService.CurrentSeasonYear;

        var octoberWednesdays = GetWednesdaysInMonth(currentYear, 10, leagueDates);
        var novemberWednesdays = GetWednesdaysInMonth(currentYear, 11, leagueDates);
        var januaryWednesdays = GetWednesdaysInMonth(currentYear + 1, 1, leagueDates).Where(d => d.Day > 15).ToList();

        var selected = new List<DateTime>();
        if (octoberWednesdays.Any()) selected.Add(octoberWednesdays[octoberWednesdays.Count / 2]);
        if (novemberWednesdays.Any()) selected.Add(novemberWednesdays[novemberWednesdays.Count / 2]);
        if (januaryWednesdays.Any()) selected.Add(januaryWednesdays[januaryWednesdays.Count / 2]);

        // Fallback if any month didn't have a valid Wednesday (rare)
        while (selected.Count < 3)
        {
            var fallback = octoberWednesdays.Concat(novemberWednesdays).Concat(januaryWednesdays)
                .OrderBy(d => d).FirstOrDefault(d => !selected.Contains(d));
            if (fallback == default) break;
            selected.Add(fallback);
        }

        return selected.OrderBy(d => d).ToList();
    }

    private static List<DateTime> GetWednesdaysInMonth(int year, int month, List<DateTime> leagueDates)
    {
        var result = new List<DateTime>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (int day = 1; day <= daysInMonth; day++)
        {
            var d = new DateTime(year, month, day);
            if (d.DayOfWeek == DayOfWeek.Wednesday)
            {
                bool valid = leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 3);
                if (valid) result.Add(d);
            }
        }
        return result;
    }

    /// <summary>
    /// Finds a Wednesday in March for the quarter-finals.
    /// </summary>
    public static DateTime GetQuarterFinalDate()
    {
        var leagueDates = LeagueService.MatchweekDates;
        var marchStart = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 1);
        var marchEnd = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 31);

        for (var d = marchStart; d <= marchEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday)
            {
                bool valid = leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 3);
                if (valid)
                    return d;
            }
        }

        // Fallback: mid-March Wednesday
        var fallback = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 15);
        while (fallback.DayOfWeek != DayOfWeek.Wednesday) fallback = fallback.AddDays(1);
        return fallback;
    }

    /// <summary>
    /// Finds two consecutive days for the Final Four (mid-May, Wed/Thu).
    /// </summary>
    public static (DateTime Day1, DateTime Day2) GetFinalFourDates()
    {
        var leagueDates = LeagueService.MatchweekDates;
        // Search mid-May
        var searchStart = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 5);
        var searchEnd = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 25);

        for (var d = searchStart; d <= searchEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday)
            {
                var day1 = d;
                var day2 = d.AddDays(1); // Thursday
                
                // Ensure at least 2 days gap from any league match (Wed vs following Sat)
                bool valid = leagueDates.All(ld =>
                    Math.Abs((day1 - ld).TotalDays) >= 2 &&
                    Math.Abs((day2 - ld).TotalDays) >= 2);
                    
                if (valid)
                    return (day1, day2);
            }
        }

        // Fallback: 2nd Wednesday of May
        var fb = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 14);
        while (fb.DayOfWeek != DayOfWeek.Wednesday) fb = fb.AddDays(-1);
        return (fb, fb.AddDays(1));
    }

    /// <summary>
    /// Returns all scheduled cup dates (group + knockout) for the current season.
    /// </summary>
    public async Task<List<DateTime>> GetAllCupDatesAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Where(f => f.Season == season)
            .Select(f => f.ScheduledDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all cup fixtures for a specific date.
    /// </summary>
    public async Task<List<CupFixture>> GetFixturesForDateAsync(DateTime date)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.CupGroup)
            .Where(f => f.Season == season && f.ScheduledDate.Date == date.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the cup fixture for a specific team on a specific date (if any).
    /// </summary>
    public async Task<CupFixture?> GetFixtureForTeamOnDateAsync(int teamId, DateTime date)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.CupGroup)
            .FirstOrDefaultAsync(f =>
                f.Season == season &&
                f.ScheduledDate.Date == date.Date &&
                !f.IsPlayed &&
                (f.HomeTeamId == teamId || f.AwayTeamId == teamId));
    }

    /// <summary>
    /// Gets the next unplayed cup fixture date.
    /// </summary>
    public async Task<DateTime?> GetNextCupDateAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Where(f => f.Season == season && !f.IsPlayed)
            .OrderBy(f => f.ScheduledDate)
            .Select(f => (DateTime?)f.ScheduledDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets the next unplayed cup fixture date specifically for a given team.
    /// </summary>
    public async Task<DateTime?> GetNextCupDateForTeamAsync(int teamId)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Where(f => f.Season == season && !f.IsPlayed && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .OrderBy(f => f.ScheduledDate)
            .Select(f => (DateTime?)f.ScheduledDate)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// After group stage is complete, creates quarter-final fixtures.
    /// Seeding: A1 vs B2, B1 vs A2, C1 vs D2, D1 vs C2.
    /// </summary>
    public async Task TryGenerateQuarterFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        // Check if QF fixtures already exist
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "QuarterFinal"))
            return;

        // Check if all group matches are played
        var groupFixtures = await _db.CupFixtures
            .Where(f => f.Season == season && f.Round == "Group")
            .ToListAsync();

        if (groupFixtures.Count == 0 || groupFixtures.Any(f => !f.IsPlayed))
            return;

        // Get group standings
        var groups = await _db.CupGroups
            .Include(g => g.Entries).ThenInclude(e => e.Team)
            .Where(g => g.Season == season)
            .ToListAsync();

        var standings = new Dictionary<string, List<CupGroupEntry>>();
        foreach (var g in groups)
        {
            standings[g.GroupName] = g.Entries
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.GoalsFor)
                .ToList();

            for (int i = 0; i < standings[g.GroupName].Count; i++)
                standings[g.GroupName][i].Rank = i + 1;
        }

        var qfDate = GetQuarterFinalDate();

        // A1 vs B2, B1 vs A2, C1 vs D2, D1 vs C2
        var qfPairings = new[]
        {
            (standings["A"][0].TeamId, standings["B"][1].TeamId),
            (standings["B"][0].TeamId, standings["A"][1].TeamId),
            (standings["C"][0].TeamId, standings["D"][1].TeamId),
            (standings["D"][0].TeamId, standings["C"][1].TeamId)
        };

        foreach (var (home, away) in qfPairings)
        {
            _db.CupFixtures.Add(new CupFixture
            {
                Season = season,
                HomeTeamId = home,
                AwayTeamId = away,
                ScheduledDate = qfDate,
                Round = "QuarterFinal"
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// After quarter-finals are complete, creates Final Four fixtures.
    /// </summary>
    public async Task TryGenerateFinalFourAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        // Check if SF fixtures already exist
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "SemiFinal"))
            return;

        var qfFixtures = await _db.CupFixtures
            .Where(f => f.Season == season && f.Round == "QuarterFinal")
            .OrderBy(f => f.Id)
            .ToListAsync();

        if (qfFixtures.Count != 4 || qfFixtures.Any(f => !f.IsPlayed))
            return;

        var (day1, day2) = GetFinalFourDates();
        var venueTeam = (await _db.Teams.Where(t => t.StadiumCapacity > 1500).ToListAsync())
            .OrderBy(_ => _rng.Next()).FirstOrDefault() ?? (await _db.Teams.FindAsync(qfFixtures[0].HomeTeamId))!;
        string venue = $"{venueTeam.StadiumName}, {venueTeam.City}";

        // Winners of QF1 vs QF2 (semi 1), Winners of QF3 vs QF4 (semi 2)
        int Winner(CupFixture f) => f.HomeGoals > f.AwayGoals ? f.HomeTeamId : f.AwayTeamId;

        var sf1Home = Winner(qfFixtures[0]);
        var sf1Away = Winner(qfFixtures[1]);
        var sf2Home = Winner(qfFixtures[2]);
        var sf2Away = Winner(qfFixtures[3]);

        _db.CupFixtures.Add(new CupFixture
        {
            Season = season, HomeTeamId = sf1Home, AwayTeamId = sf1Away,
            ScheduledDate = day1, Round = "SemiFinal", VenueName = venue
        });
        _db.CupFixtures.Add(new CupFixture
        {
            Season = season, HomeTeamId = sf2Home, AwayTeamId = sf2Away,
            ScheduledDate = day1, Round = "SemiFinal", VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// After semi-finals are complete, creates the final and 3rd-place match.
    /// </summary>
    public async Task TryGenerateFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "Final"))
            return;

        var sfFixtures = await _db.CupFixtures
            .Where(f => f.Season == season && f.Round == "SemiFinal")
            .OrderBy(f => f.Id)
            .ToListAsync();

        if (sfFixtures.Count != 2 || sfFixtures.Any(f => !f.IsPlayed))
            return;

        var (_, day2) = GetFinalFourDates();
        // Use same venue from the semifinals
        string venue = sfFixtures[0].VenueName ?? "Sala Polivalentă, Bucharest";

        int Winner(CupFixture f) => f.HomeGoals > f.AwayGoals ? f.HomeTeamId : f.AwayTeamId;
        int Loser(CupFixture f) => f.HomeGoals > f.AwayGoals ? f.AwayTeamId : f.HomeTeamId;

        // 3rd place match
        _db.CupFixtures.Add(new CupFixture
        {
            Season = season,
            HomeTeamId = Loser(sfFixtures[0]),
            AwayTeamId = Loser(sfFixtures[1]),
            ScheduledDate = day2,
            Round = "ThirdPlace",
            VenueName = venue
        });

        // Final
        _db.CupFixtures.Add(new CupFixture
        {
            Season = season,
            HomeTeamId = Winner(sfFixtures[0]),
            AwayTeamId = Winner(sfFixtures[1]),
            ScheduledDate = day2,
            Round = "Final",
            VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all groups with standings for the current season.
    /// </summary>
    public async Task<List<CupGroup>> GetAllGroupsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var groups = await _db.CupGroups
            .Include(g => g.Entries).ThenInclude(e => e.Team)
            .Include(g => g.Fixtures)
            .Where(g => g.Season == season)
            .ToListAsync();

        foreach (var g in groups)
        {
            var sorted = g.Entries
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.GoalsFor)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Rank = i + 1;

            g.Entries = sorted;
        }

        return groups.OrderBy(g => g.GroupName).ToList();
    }

    /// <summary>
    /// Gets the cup group that the player's team belongs to.
    /// </summary>
    public async Task<CupGroup?> GetPlayerTeamGroupAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (playerTeam == null) return null;

        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var group = await _db.CupGroups
            .Include(g => g.Entries).ThenInclude(e => e.Team)
            .Include(g => g.Fixtures)
            .Where(g => g.Season == season && g.Entries.Any(e => e.TeamId == playerTeam.Id))
            .FirstOrDefaultAsync();

        if (group != null)
        {
            var sorted = group.Entries
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.GoalsFor)
                .ToList();
            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Rank = i + 1;
            group.Entries = sorted;
        }

        return group;
    }

    /// <summary>
    /// Gets all knockout fixtures for the current season.
    /// </summary>
    public async Task<List<CupFixture>> GetKnockoutFixturesAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.Round != "Group")
            .OrderBy(f => f.ScheduledDate)
            .ThenBy(f => f.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Clears all cup data for the current season (end-of-season cleanup).
    /// </summary>
    public async Task ClearSeasonDataAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        var fixtures = await _db.CupFixtures.Where(f => f.Season == season).ToListAsync();
        var entries = await _db.CupGroupEntries
            .Where(e => e.CupGroup != null && e.CupGroup.Season == season).ToListAsync();
        var groups = await _db.CupGroups.Where(g => g.Season == season).ToListAsync();

        _db.CupFixtures.RemoveRange(fixtures);
        _db.CupGroupEntries.RemoveRange(entries);
        _db.CupGroups.RemoveRange(groups);

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Updates CupGroupEntry stats after a group-stage match.
    /// </summary>
    public async Task UpdateGroupEntryAsync(int cupGroupId, int teamId, int goalsFor, int goalsAgainst)
    {
        var entry = await _db.CupGroupEntries
            .FirstOrDefaultAsync(e => e.CupGroupId == cupGroupId && e.TeamId == teamId);
        if (entry == null) return;

        entry.Played++;
        entry.GoalsFor += goalsFor;
        entry.GoalsAgainst += goalsAgainst;

        if (goalsFor > goalsAgainst) entry.Won++;
        else if (goalsFor == goalsAgainst) entry.Drawn++;
        else entry.Lost++;
    }
}
