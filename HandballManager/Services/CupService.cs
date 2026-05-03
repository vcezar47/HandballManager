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

    // ─── Generation ────────────────────────────────────────────────────

    public async Task GenerateCupAsync()
    {
        await GenerateRomanianCupAsync();
        await GenerateHungarianCupAsync();
        await GenerateFrenchCupAsync();
    }

    private async Task GenerateRomanianCupAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupGroups.AnyAsync(g => g.Season == season && g.CompetitionName == "Liga Florilor"))
            return;

        var teamIds = await _db.Teams.Where(t => t.CompetitionName == "Liga Florilor").Select(t => t.Id).ToListAsync();
        var shuffled = teamIds.OrderBy(_ => _rng.Next()).ToList();
        string[] groupNames = ["A", "B", "C", "D"];
        var groups = new List<CupGroup>();

        for (int g = 0; g < 4; g++)
        {
            var group = new CupGroup { GroupName = groupNames[g], Season = season, CompetitionName = "Liga Florilor" };
            foreach (var tid in shuffled.Skip(g * 3).Take(3))
                group.Entries.Add(new CupGroupEntry { TeamId = tid });
            groups.Add(group);
            _db.CupGroups.Add(group);
        }
        await _db.SaveChangesAsync();

        var groupDates = GetGroupStageDates("Liga Florilor");
        foreach (var group in groups)
        {
            var ids = group.Entries.Select(e => e.TeamId).ToList();
            var pairings = new List<(int H, int A)> { (ids[0], ids[1]), (ids[0], ids[2]), (ids[1], ids[2]) };
            pairings = pairings.OrderBy(_ => _rng.Next()).ToList();
            for (int i = 0; i < 3; i++)
            {
                var (h, a) = _rng.NextDouble() < 0.5 ? pairings[i] : (pairings[i].A, pairings[i].H);
                _db.CupFixtures.Add(new CupFixture
                {
                    CupGroupId = group.Id, Season = season, CompetitionName = "Liga Florilor",
                    HomeTeamId = h, AwayTeamId = a, ScheduledDate = groupDates[i], Round = "Group"
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task GenerateHungarianCupAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupGroups.AnyAsync(g => g.Season == season && g.CompetitionName == "NB I"))
            return;

        var teamIds = await _db.Teams.Where(t => t.CompetitionName == "NB I").Select(t => t.Id).ToListAsync();
        if (teamIds.Count < 14) return;
        var shuffled = teamIds.OrderBy(_ => _rng.Next()).ToList();

        string[] groupNames = ["A", "B"];
        var groups = new List<CupGroup>();
        for (int g = 0; g < 2; g++)
        {
            var group = new CupGroup { GroupName = groupNames[g], Season = season, CompetitionName = "NB I" };
            foreach (var tid in shuffled.Skip(g * 7).Take(7))
                group.Entries.Add(new CupGroupEntry { TeamId = tid });
            groups.Add(group);
            _db.CupGroups.Add(group);
        }
        await _db.SaveChangesAsync();

        // 7 teams → round-robin = 7 matchdays, 3 matches + 1 bye per day
        var dates = GetGroupStageDates("NB I");
        foreach (var group in groups)
        {
            var ids = group.Entries.Select(e => e.TeamId).ToList();
            int n = ids.Count; // 7
            // Generate round-robin schedule for odd number of teams (add phantom)
            var schedule = GenerateRoundRobinSchedule(ids);
            for (int day = 0; day < schedule.Count && day < dates.Count; day++)
            {
                foreach (var (h, a) in schedule[day])
                {
                    _db.CupFixtures.Add(new CupFixture
                    {
                        CupGroupId = group.Id, Season = season, CompetitionName = "NB I",
                        HomeTeamId = h, AwayTeamId = a, ScheduledDate = dates[day], Round = "Group"
                    });
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task GenerateFrenchCupAsync()
    {
        const string comp = "Ligue Butagaz Énergie";
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupGroups.AnyAsync(g => g.Season == season && g.CompetitionName == comp))
            return;

        var teamIds = await _db.Teams.Where(t => t.CompetitionName == comp).Select(t => t.Id).ToListAsync();
        if (teamIds.Count < 14) return;
        var shuffled = teamIds.OrderBy(_ => _rng.Next()).ToList();

        string[] groupNames = ["A", "B"];
        var groups = new List<CupGroup>();
        for (int g = 0; g < 2; g++)
        {
            var group = new CupGroup { GroupName = groupNames[g], Season = season, CompetitionName = comp };
            foreach (var tid in shuffled.Skip(g * 7).Take(7))
                group.Entries.Add(new CupGroupEntry { TeamId = tid });
            groups.Add(group);
            _db.CupGroups.Add(group);
        }
        await _db.SaveChangesAsync();

        // 7 teams → round-robin = 7 matchdays, 3 matches + 1 bye per day
        var dates = GetGroupStageDates(comp);
        foreach (var group in groups)
        {
            var ids = group.Entries.Select(e => e.TeamId).ToList();
            var schedule = GenerateRoundRobinSchedule(ids);
            for (int day = 0; day < schedule.Count && day < dates.Count; day++)
            {
                foreach (var (h, a) in schedule[day])
                {
                    _db.CupFixtures.Add(new CupFixture
                    {
                        CupGroupId = group.Id, Season = season, CompetitionName = comp,
                        HomeTeamId = h, AwayTeamId = a, ScheduledDate = dates[day], Round = "Group"
                    });
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Round-robin for odd-count teams (one bye per round).</summary>
    private List<List<(int Home, int Away)>> GenerateRoundRobinSchedule(List<int> teamIds)
    {
        var teams = new List<int>(teamIds);
        int n = teams.Count;
        // For odd number, add a "bye" sentinel
        if (n % 2 != 0) { teams.Add(-1); n++; }

        var rounds = new List<List<(int, int)>>();
        var rotatingTeams = teams.Skip(1).ToList();

        for (int round = 0; round < n - 1; round++)
        {
            var matches = new List<(int, int)>();
            var current = new List<int> { teams[0] };
            current.AddRange(rotatingTeams);

            for (int i = 0; i < n / 2; i++)
            {
                int home = current[i];
                int away = current[n - 1 - i];
                if (home == -1 || away == -1) continue; // bye
                if (_rng.NextDouble() < 0.5) (home, away) = (away, home);
                matches.Add((home, away));
            }
            rounds.Add(matches);
            // Rotate
            var last = rotatingTeams[^1];
            rotatingTeams.RemoveAt(rotatingTeams.Count - 1);
            rotatingTeams.Insert(0, last);
        }
        return rounds;
    }

    // ─── Scheduling helpers ────────────────────────────────────────────

    public static List<DateTime> GetGroupStageDates(string comp)
    {
        if (comp == "NB I") return GetHungarianGroupStageDates();
        if (comp == "Ligue Butagaz Énergie") return GetFrenchGroupStageDates();
        return GetRomanianGroupStageDates();
    }

    private static List<DateTime> GetRomanianGroupStageDates()
    {
        var leagueDates = LeagueService.GetMatchweekDates("Liga Florilor");
        var y = LeagueService.CurrentSeasonYear;
        var octW = GetWednesdaysInMonth(y, 10, leagueDates);
        var novW = GetWednesdaysInMonth(y, 11, leagueDates);
        var janW = GetWednesdaysInMonth(y + 1, 1, leagueDates).Where(d => d.Day > 15).ToList();
        var sel = new List<DateTime>();
        if (octW.Any()) sel.Add(octW[octW.Count / 2]);
        if (novW.Any()) sel.Add(novW[novW.Count / 2]);
        if (janW.Any()) sel.Add(janW[janW.Count / 2]);
        while (sel.Count < 3)
        {
            var fb = octW.Concat(novW).Concat(janW).OrderBy(d => d).FirstOrDefault(d => !sel.Contains(d));
            if (fb == default) break;
            sel.Add(fb);
        }
        return sel.OrderBy(d => d).ToList();
    }

    private static List<DateTime> GetHungarianGroupStageDates()
    {
        var leagueDates = LeagueService.GetMatchweekDates("NB I");
        var y = LeagueService.CurrentSeasonYear;
        // Collect valid Wednesdays from Oct through Mar
        var allWed = new List<DateTime>();
        int[] months = [10, 11, 12, 1, 2, 3];
        foreach (var m in months)
        {
            int yr = m >= 10 ? y : y + 1;
            // Skip winter break period (Dec 15 – Jan 15)
            allWed.AddRange(GetWednesdaysInMonth(yr, m, leagueDates)
                .Where(d => !(d >= new DateTime(y, 12, 15) && d <= new DateTime(y + 1, 1, 15))));
        }
        if (allWed.Count <= 7) return allWed.Take(7).ToList();
        // Spread 7 dates evenly
        var result = new List<DateTime>();
        double step = (allWed.Count - 1) / 6.0;
        for (int i = 0; i < 7; i++)
            result.Add(allWed[(int)Math.Round(i * step)]);
        return result;
    }

    private static List<DateTime> GetFrenchGroupStageDates()
    {
        var leagueDates = LeagueService.GetMatchweekDates("Ligue Butagaz Énergie");
        var y = LeagueService.CurrentSeasonYear;

        // 7 dates, strictly within Oct -> Feb (inclusive), avoiding Dec 15 – Jan 15 break,
        // and avoiding being within 3 days of league matchdays (via GetWednesdaysInMonth filter).
        var allWed = new List<DateTime>();
        int[] months = [10, 11, 12, 1, 2];
        foreach (var m in months)
        {
            int yr = m >= 10 ? y : y + 1;
            allWed.AddRange(GetWednesdaysInMonth(yr, m, leagueDates)
                .Where(d => !(d >= new DateTime(y, 12, 15) && d <= new DateTime(y + 1, 1, 15))));
        }

        // If not enough, fall back to any valid Wednesdays in that window.
        if (allWed.Count <= 7) return allWed.Take(7).OrderBy(d => d).ToList();

        // Spread 7 dates evenly across the available window.
        var result = new List<DateTime>();
        double step = (allWed.Count - 1) / 6.0;
        for (int i = 0; i < 7; i++)
            result.Add(allWed[(int)Math.Round(i * step)]);
        return result.Distinct().OrderBy(d => d).Take(7).ToList();
    }

    private static List<DateTime> GetWednesdaysInMonth(int year, int month, List<DateTime> leagueDates)
    {
        var result = new List<DateTime>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (int day = 1; day <= daysInMonth; day++)
        {
            var d = new DateTime(year, month, day);
            if (d.DayOfWeek == DayOfWeek.Wednesday && leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 3))
                result.Add(d);
        }
        return result;
    }

    public static DateTime GetFrenchSemiFinalDate()
    {
        var leagueDates = LeagueService.GetMatchweekDates("Ligue Butagaz Énergie");
        var start = new DateTime(LeagueService.CurrentSeasonYear + 1, 4, 1);
        var end = new DateTime(LeagueService.CurrentSeasonYear + 1, 4, 15);
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday && leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 3))
                return d;
        }
        var fb = new DateTime(LeagueService.CurrentSeasonYear + 1, 4, 8);
        while (fb.DayOfWeek != DayOfWeek.Wednesday) fb = fb.AddDays(1);
        return fb;
    }

    public static DateTime GetFrenchFinalDate()
    {
        var leagueDates = LeagueService.GetMatchweekDates("Ligue Butagaz Énergie");
        var start = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 15);
        var end = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 31);
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday && leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 2))
                return d;
        }
        var fb = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 21);
        while (fb.DayOfWeek != DayOfWeek.Wednesday) fb = fb.AddDays(-1);
        return fb;
    }

    public static DateTime GetQuarterFinalDate()
    {
        var leagueDates = LeagueService.GetMatchweekDates("Liga Florilor");
        var marchStart = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 1);
        var marchEnd = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 31);
        for (var d = marchStart; d <= marchEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday && leagueDates.All(ld => Math.Abs((d - ld).TotalDays) >= 3))
                return d;
        }
        var fb = new DateTime(LeagueService.CurrentSeasonYear + 1, 3, 15);
        while (fb.DayOfWeek != DayOfWeek.Wednesday) fb = fb.AddDays(1);
        return fb;
    }

    public static (DateTime Day1, DateTime Day2) GetFinalFourDates(string comp)
    {
        var leagueDates = LeagueService.GetMatchweekDates(comp);
        var searchStart = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 5);
        var searchEnd = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 25);
        for (var d = searchStart; d <= searchEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Wednesday)
            {
                var day1 = d; var day2 = d.AddDays(1);
                if (leagueDates.All(ld => Math.Abs((day1 - ld).TotalDays) >= 2 && Math.Abs((day2 - ld).TotalDays) >= 2))
                    return (day1, day2);
            }
        }
        var fb = new DateTime(LeagueService.CurrentSeasonYear + 1, 5, 14);
        while (fb.DayOfWeek != DayOfWeek.Wednesday) fb = fb.AddDays(-1);
        return (fb, fb.AddDays(1));
    }

    // ─── Queries ───────────────────────────────────────────────────────

    public async Task<List<DateTime>> GetAllCupDatesAsync(string? comp = null)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var q = _db.CupFixtures.Where(f => f.Season == season);
        if (comp != null) q = q.Where(f => f.CompetitionName == comp);
        return await q.Select(f => f.ScheduledDate).Distinct().OrderBy(d => d).ToListAsync();
    }

    public async Task<List<CupFixture>> GetFixturesForDateAsync(DateTime date, string? comp = null)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var q = _db.CupFixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.CupGroup)
            .Where(f => f.Season == season && f.ScheduledDate.Date == date.Date);
        if (comp != null) q = q.Where(f => f.CompetitionName == comp);
        return await q.ToListAsync();
    }

    public async Task<CupFixture?> GetFixtureForTeamOnDateAsync(int teamId, DateTime date)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.CupGroup)
            .FirstOrDefaultAsync(f => f.Season == season && f.ScheduledDate.Date == date.Date && !f.IsPlayed
                && (f.HomeTeamId == teamId || f.AwayTeamId == teamId));
    }

    public async Task<DateTime?> GetNextCupDateAsync(string? comp = null)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var q = _db.CupFixtures.Where(f => f.Season == season && !f.IsPlayed);
        if (comp != null) q = q.Where(f => f.CompetitionName == comp);
        return await q.OrderBy(f => f.ScheduledDate).Select(f => (DateTime?)f.ScheduledDate).FirstOrDefaultAsync();
    }

    public async Task<DateTime?> GetNextCupDateForTeamAsync(int teamId)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures
            .Where(f => f.Season == season && !f.IsPlayed && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .OrderBy(f => f.ScheduledDate).Select(f => (DateTime?)f.ScheduledDate).FirstOrDefaultAsync();
    }

    // ─── Bracket advancement ───────────────────────────────────────────

    public async Task TryGenerateQuarterFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "QuarterFinal" && f.CompetitionName == "Liga Florilor"))
            return;
        var groupFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "Group" && f.CompetitionName == "Liga Florilor").ToListAsync();
        if (groupFixtures.Count == 0 || groupFixtures.Any(f => !f.IsPlayed)) return;

        var groups = await _db.CupGroups.Include(g => g.Entries).ThenInclude(e => e.Team)
            .Where(g => g.Season == season && g.CompetitionName == "Liga Florilor").ToListAsync();
        var standings = new Dictionary<string, List<CupGroupEntry>>();
        foreach (var g in groups)
        {
            standings[g.GroupName] = g.Entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
            for (int i = 0; i < standings[g.GroupName].Count; i++) standings[g.GroupName][i].Rank = i + 1;
        }
        var qfDate = GetQuarterFinalDate();
        var qfPairings = new[] {
            (standings["A"][0].TeamId, standings["B"][1].TeamId),
            (standings["B"][0].TeamId, standings["A"][1].TeamId),
            (standings["C"][0].TeamId, standings["D"][1].TeamId),
            (standings["D"][0].TeamId, standings["C"][1].TeamId)
        };
        foreach (var (home, away) in qfPairings)
            _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = "Liga Florilor", HomeTeamId = home, AwayTeamId = away, ScheduledDate = qfDate, Round = "QuarterFinal" });
        await _db.SaveChangesAsync();
    }

    public async Task TryGenerateFinalFourAsync()
    {
        // Romanian: QF → SF
        await TryGenerateRomanianSemiFinalsAsync();
        // Hungarian: Group → SF (no QF)
        await TryGenerateHungarianSemiFinalsAsync();
    }

    public async Task TryGenerateFrenchSemiFinalsAsync()
    {
        const string comp = "Ligue Butagaz Énergie";
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "SemiFinal" && f.CompetitionName == comp))
            return;

        var groupFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "Group" && f.CompetitionName == comp).ToListAsync();
        if (groupFixtures.Count == 0 || groupFixtures.Any(f => !f.IsPlayed)) return;

        var groups = await _db.CupGroups.Include(g => g.Entries).ThenInclude(e => e.Team)
            .Where(g => g.Season == season && g.CompetitionName == comp).ToListAsync();
        if (groups.Count != 2) return;

        var standings = new Dictionary<string, List<CupGroupEntry>>();
        foreach (var g in groups)
        {
            standings[g.GroupName] = g.Entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
            for (int i = 0; i < standings[g.GroupName].Count; i++) standings[g.GroupName][i].Rank = i + 1;
        }

        if (!standings.ContainsKey("A") || !standings.ContainsKey("B")) return;
        if (standings["A"].Count < 4 || standings["B"].Count < 4) return;

        var sfDate = GetFrenchSemiFinalDate();

        // Home semifinals: A1 vs B4 and B2 vs A3 (uses all 4 qualifiers per group)
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = comp, HomeTeamId = standings["A"][0].TeamId, AwayTeamId = standings["B"][3].TeamId, ScheduledDate = sfDate, Round = "SemiFinal" });
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = comp, HomeTeamId = standings["B"][1].TeamId, AwayTeamId = standings["A"][2].TeamId, ScheduledDate = sfDate, Round = "SemiFinal" });

        await _db.SaveChangesAsync();
    }

    private async Task TryGenerateRomanianSemiFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "SemiFinal" && f.CompetitionName == "Liga Florilor"))
            return;
        var qfFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "QuarterFinal" && f.CompetitionName == "Liga Florilor").OrderBy(f => f.Id).ToListAsync();
        if (qfFixtures.Count != 4 || qfFixtures.Any(f => !f.IsPlayed)) return;

        var (day1, _) = GetFinalFourDates("Liga Florilor");
        var venueTeam = (await _db.Teams.Where(t => t.CompetitionName == "Liga Florilor" && t.StadiumCapacity > 1500).ToListAsync())
            .OrderBy(_ => _rng.Next()).FirstOrDefault();
        string venue = venueTeam != null ? $"{venueTeam.StadiumName}, {venueTeam.City}" : "Sala Polivalentă, Bucharest";

        int Winner(CupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.HomeTeamId : f.AwayTeamId;
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = "Liga Florilor", HomeTeamId = Winner(qfFixtures[0]), AwayTeamId = Winner(qfFixtures[1]), ScheduledDate = day1, Round = "SemiFinal", VenueName = venue });
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = "Liga Florilor", HomeTeamId = Winner(qfFixtures[2]), AwayTeamId = Winner(qfFixtures[3]), ScheduledDate = day1, Round = "SemiFinal", VenueName = venue });
        await _db.SaveChangesAsync();
    }

    private async Task TryGenerateHungarianSemiFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "SemiFinal" && f.CompetitionName == "NB I"))
            return;
        var groupFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "Group" && f.CompetitionName == "NB I").ToListAsync();
        if (groupFixtures.Count == 0 || groupFixtures.Any(f => !f.IsPlayed)) return;

        var groups = await _db.CupGroups.Include(g => g.Entries).ThenInclude(e => e.Team)
            .Where(g => g.Season == season && g.CompetitionName == "NB I").ToListAsync();
        var standings = new Dictionary<string, List<CupGroupEntry>>();
        foreach (var g in groups)
        {
            standings[g.GroupName] = g.Entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
            for (int i = 0; i < standings[g.GroupName].Count; i++) standings[g.GroupName][i].Rank = i + 1;
        }

        var (day1, _) = GetFinalFourDates("NB I");
        var venueTeam = (await _db.Teams.Where(t => t.CompetitionName == "NB I" && t.StadiumCapacity > 1500).ToListAsync())
            .OrderBy(_ => _rng.Next()).FirstOrDefault();
        string venue = venueTeam != null ? $"{venueTeam.StadiumName}, {venueTeam.City}" : "Audi Aréna, Győr";

        // A1 vs B2, B1 vs A2
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = "NB I", HomeTeamId = standings["A"][0].TeamId, AwayTeamId = standings["B"][1].TeamId, ScheduledDate = day1, Round = "SemiFinal", VenueName = venue });
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = "NB I", HomeTeamId = standings["B"][0].TeamId, AwayTeamId = standings["A"][1].TeamId, ScheduledDate = day1, Round = "SemiFinal", VenueName = venue });
        await _db.SaveChangesAsync();
    }

    public async Task TryGenerateFinalsAsync()
    {
        await TryGenerateFinalsForCompAsync("Liga Florilor");
        await TryGenerateFinalsForCompAsync("NB I");
        await TryGenerateFrenchFinalAsync();
    }

    private async Task TryGenerateFinalsForCompAsync(string comp)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "Final" && f.CompetitionName == comp))
            return;
        var sfFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "SemiFinal" && f.CompetitionName == comp).OrderBy(f => f.Id).ToListAsync();
        if (sfFixtures.Count != 2 || sfFixtures.Any(f => !f.IsPlayed)) return;

        var (_, day2) = GetFinalFourDates(comp);
        string venue = sfFixtures[0].VenueName ?? (comp == "NB I" ? "Audi Aréna, Győr" : "Sala Polivalentă, Bucharest");
        int Winner(CupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.HomeTeamId : f.AwayTeamId;
        int Loser(CupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.AwayTeamId : f.HomeTeamId;

        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = comp, HomeTeamId = Loser(sfFixtures[0]), AwayTeamId = Loser(sfFixtures[1]), ScheduledDate = day2, Round = "ThirdPlace", VenueName = venue });
        _db.CupFixtures.Add(new CupFixture { Season = season, CompetitionName = comp, HomeTeamId = Winner(sfFixtures[0]), AwayTeamId = Winner(sfFixtures[1]), ScheduledDate = day2, Round = "Final", VenueName = venue });
        await _db.SaveChangesAsync();
    }

    private async Task TryGenerateFrenchFinalAsync()
    {
        const string comp = "Ligue Butagaz Énergie";
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.CupFixtures.AnyAsync(f => f.Season == season && f.Round == "Final" && f.CompetitionName == comp))
            return;

        var sfFixtures = await _db.CupFixtures.Where(f => f.Season == season && f.Round == "SemiFinal" && f.CompetitionName == comp).OrderBy(f => f.Id).ToListAsync();
        if (sfFixtures.Count != 2 || sfFixtures.Any(f => !f.IsPlayed)) return;

        int Winner(CupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.HomeTeamId : f.AwayTeamId;

        var finalDate = GetFrenchFinalDate();

        var venueTeam = (await _db.Teams.Where(t => t.CompetitionName == comp && t.StadiumCapacity >= 2000).ToListAsync())
            .OrderBy(_ => _rng.Next()).FirstOrDefault();
        string venue = venueTeam != null ? $"{venueTeam.StadiumName}, {venueTeam.City}" : "Accor Arena, Paris";

        _db.CupFixtures.Add(new CupFixture
        {
            Season = season,
            CompetitionName = comp,
            HomeTeamId = Winner(sfFixtures[0]),
            AwayTeamId = Winner(sfFixtures[1]),
            ScheduledDate = finalDate,
            Round = "Final",
            VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    // ─── Group/standings queries ───────────────────────────────────────

    public async Task<List<CupGroup>> GetAllGroupsAsync(string comp = "Liga Florilor")
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var groups = await _db.CupGroups.Include(g => g.Entries).ThenInclude(e => e.Team).Include(g => g.Fixtures)
            .Where(g => g.Season == season && g.CompetitionName == comp).ToListAsync();
        foreach (var g in groups)
        {
            var sorted = g.Entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
            for (int i = 0; i < sorted.Count; i++) sorted[i].Rank = i + 1;
            g.Entries = sorted;
        }
        return groups.OrderBy(g => g.GroupName).ToList();
    }

    public async Task<CupGroup?> GetPlayerTeamGroupAsync(string? comp = null)
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (playerTeam == null) return null;
        string targetComp = comp ?? playerTeam.CompetitionName;
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var group = await _db.CupGroups.Include(g => g.Entries).ThenInclude(e => e.Team).Include(g => g.Fixtures)
            .Where(g => g.Season == season && g.CompetitionName == targetComp && g.Entries.Any(e => e.TeamId == playerTeam.Id))
            .FirstOrDefaultAsync();
        if (group != null)
        {
            var sorted = group.Entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
            for (int i = 0; i < sorted.Count; i++) sorted[i].Rank = i + 1;
            group.Entries = sorted;
        }
        return group;
    }

    public async Task<List<CupFixture>> GetKnockoutFixturesAsync(string comp = "Liga Florilor")
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.CupFixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.Round != "Group" && f.CompetitionName == comp)
            .OrderBy(f => f.ScheduledDate).ThenBy(f => f.Id).ToListAsync();
    }

    // ─── Cleanup ───────────────────────────────────────────────────────

    public async Task ClearSeasonDataAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var fixtures = await _db.CupFixtures.Where(f => f.Season == season).ToListAsync();
        var entries = await _db.CupGroupEntries.Where(e => e.CupGroup != null && e.CupGroup.Season == season).ToListAsync();
        var groups = await _db.CupGroups.Where(g => g.Season == season).ToListAsync();
        _db.CupFixtures.RemoveRange(fixtures);
        _db.CupGroupEntries.RemoveRange(entries);
        _db.CupGroups.RemoveRange(groups);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateGroupEntryAsync(int cupGroupId, int teamId, int goalsFor, int goalsAgainst)
    {
        var entry = await _db.CupGroupEntries.FirstOrDefaultAsync(e => e.CupGroupId == cupGroupId && e.TeamId == teamId);
        if (entry == null) return;
        entry.Played++;
        entry.GoalsFor += goalsFor;
        entry.GoalsAgainst += goalsAgainst;
        if (goalsFor > goalsAgainst) entry.Won++;
        else if (goalsFor == goalsAgainst) entry.Drawn++;
        else entry.Lost++;
    }
}
