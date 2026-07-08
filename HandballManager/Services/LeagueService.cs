using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class LeagueService
{
    public const string KvindeligaenCompetition = "Kvindeligaen";
    public const int KvindeligaenRegularSeasonRounds = 26;

    private readonly HandballDbContext _db;
    private readonly Random _kvPlayoffRng = new();

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
        if (competitionName == KvindeligaenCompetition)
            return GetMatchweekDates(KvindeligaenCompetition).Count;
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
        if (competitionName == "Kvindeligaen")
        {
            // Regular rounds 1–26 end by early April
            var regular = GenerateSeasonMatchweekDatesCore(seasonYear, KvindeligaenRegularSeasonRounds, new DateTime(seasonYear + 1, 4, 7));
            
            var playoff = new List<DateTime>();
            // Start playoff slots 4 days after the last regular match (typically a Wednesday)
            var d = regular[^1].AddDays(4); 
            // 6 champ group games + 3 SF legs + 3 Final legs = 12. Use 20 for safety cushion.
            const int playoffSlots = 20; 
            for (int i = 0; i < playoffSlots; i++)
            {
                playoff.Add(d);
                if (i < 5)
                {
                    // First 6 group stage slots (indices 0-5): 2 per week
                    d = d.AddDays(d.DayOfWeek == DayOfWeek.Wednesday ? 3 : 4);
                }
                else
                {
                    // Semis and Finals (indices 6+): 1 per week to stretch the season to June
                    d = d.AddDays(7);
                }
            }

            return regular.Concat(playoff).ToList();
        }

        int maxMatchweeks = GetMaxMatchweeks(competitionName);
        return GenerateSeasonMatchweekDatesCore(seasonYear, maxMatchweeks);
    }

    /// <summary>Spread league Saturdays Sep–May (winter break), same schedule shape for all leagues.</summary>
    private static List<DateTime> GenerateSeasonMatchweekDatesCore(int seasonYear, int maxMatchweeks, DateTime? customEnd = null)
    {
        var seasonStart = FirstSaturdayOnOrAfter(new DateTime(seasonYear, 9, 1));
        var seasonEnd = customEnd ?? LastSaturdayOnOrBefore(new DateTime(seasonYear + 1, 5, 25));

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

        if (candidates.Count <= maxMatchweeks)
            return candidates.Take(maxMatchweeks).ToList();

        var indices = new int[maxMatchweeks];
        double step = (candidates.Count - 1) / (double)(maxMatchweeks - 1);
        for (int i = 0; i < maxMatchweeks; i++)
            indices[i] = (int)Math.Round(i * step);

        for (int i = 1; i < indices.Length; i++)
        {
            if (indices[i] <= indices[i - 1])
                indices[i] = indices[i - 1] + 1;
        }

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

    /// <summary>Restores the season year when loading a saved game (or resetting for a new one) and clears the date cache.</summary>
    public static void RestoreSeasonYear(int year)
    {
        CurrentSeasonYear = year;
        _matchweekDatesCache.Clear();
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

    /// <summary>Human-readable league / cup-style banner for fixtures on Kvindeligaen calendar rounds.</summary>
    public static string FormatKvindeligaenLeagueBanner(int leagueRound, string? phase = null, string? playoffSeriesId = null,
        int playoffLeg = 0)
    {
        if (phase == "KvBo3")
        {
            string series = playoffSeriesId switch
            {
                "SF1" => "Semi-final 1 (best-of-3)",
                "SF2" => "Semi-final 2 (best-of-3)",
                "FIN" => "Final (best-of-3)",
                "THIRD" => "3rd place (best-of-3)",
                _ => "Playoffs (best-of-3)"
            };
            if (playoffLeg > 0)
                return $"{series} • Leg {playoffLeg}";
            return series;
        }

        if (leagueRound <= KvindeligaenRegularSeasonRounds)
            return $"Regular season — Matchweek {leagueRound}";

        int afterReg = leagueRound - KvindeligaenRegularSeasonRounds;

        if (phase == "Relegation")
            return $"Relegation group — Round {afterReg} of {KvindeligaenRelRoundsNum}";

        if (afterReg <= KvindeligaenChampRoundsNum)
            return $"Championship groups — Round {afterReg} of {KvindeligaenChampRoundsNum}";

        return $"Kvindeligaen playoffs — Scheduling round {leagueRound}";
    }

    /// <summary>Short subtitle for individual fixtures in result lists.</summary>
    public static string? FormatKvindeligaenFixtureListSubtitle(string phase, int round, string? playoffSeriesId, int playoffLeg)
        => FormatKvindeligaenLeagueBanner(round, phase, playoffSeriesId, playoffLeg);

    public async Task<List<LeagueEntry>> GetKvindeligaenComputedRegularStandingsAsync()
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        var fx = await _db.LeagueFixtures
            .Include(f => f.MatchRecord)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition
                && f.Round <= KvindeligaenRegularSeasonRounds && (f.Phase == "Regular" || f.Phase == ""))
            .AsSplitQuery()
            .ToListAsync();

        var allTeams = await _db.Teams.Where(t => t.CompetitionName == KvindeligaenCompetition).ToListAsync();
        return BuildKvMiniLeagueTable(fx, allTeams);
    }

    public async Task<List<LeagueEntry>> GetKvindeligaenComputedMiniLeagueStandingsAsync(string phase)
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        var fx = await _db.LeagueFixtures
            .Include(f => f.MatchRecord)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.Phase == phase)
            .AsSplitQuery()
            .ToListAsync();

        var teamIds = fx.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        var teams = await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToListAsync();
        return BuildKvMiniLeagueTable(fx, teams);
    }

    public async Task<List<KvKnockoutFixtureRow>> GetKvindeligaenKnockoutRowsAsync()
    {
        string season = $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";
        var fx = await _db.LeagueFixtures
            .Include(f => f.MatchRecord)
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.Phase == "KvBo3")
            .OrderBy(f => f.Round).ThenBy(f => f.PlayoffSeriesId).ThenBy(f => f.PlayoffLeg)
            .AsSplitQuery()
            .ToListAsync();

        var rows = new List<KvKnockoutFixtureRow>();
        foreach (var f in fx)
        {
            string cap = FormatKvindeligaenLeagueBanner(f.Round, f.Phase, f.PlayoffSeriesId, f.PlayoffLeg);
            string score = f is { IsPlayed: true, MatchRecord: { } r }
                ? $"{r.HomeGoals} – {r.AwayGoals}"
                : "—";
            rows.Add(new KvKnockoutFixtureRow
            {
                Caption = cap,
                HomeName = f.HomeTeam?.Name ?? "?",
                AwayName = f.AwayTeam?.Name ?? "?",
                ScoreText = score,
                HomeLogo = f.HomeTeam?.LogoPath ?? "",
                AwayLogo = f.AwayTeam?.LogoPath ?? "",
                IsFinal = f.PlayoffSeriesId == "FIN"
            });
        }

        return rows;
    }

    private static List<LeagueEntry> BuildKvMiniLeagueTable(List<LeagueFixture> fixtures, List<Team> teamsInTable)
    {
        var stats = teamsInTable.ToDictionary(t => t.Id, _ => (p: 0, w: 0, d: 0, l: 0, gf: 0, ga: 0));

        foreach (var f in fixtures.Where(x => x is { IsPlayed: true, MatchRecord: not null }))
        {
            var r = f.MatchRecord!;
            int hg = r.HomeGoals, ag = r.AwayGoals;
            var hs = stats[f.HomeTeamId];
            Update(ref hs, hg, ag);
            stats[f.HomeTeamId] = hs;
            var as_ = stats[f.AwayTeamId];
            Update(ref as_, ag, hg);
            stats[f.AwayTeamId] = as_;
        }

        var list = teamsInTable.Select(t =>
        {
            var s = stats[t.Id];
            return new LeagueEntry
            {
                TeamId = t.Id,
                Team = t,
                CompetitionName = KvindeligaenCompetition,
                Played = s.p,
                Won = s.w,
                Drawn = s.d,
                Lost = s.l,
                GoalsFor = s.gf,
                GoalsAgainst = s.ga
            };
        }).ToList();

        var sorted = list
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
            sorted[i].Rank = i + 1;

        return sorted;

        static void Update(ref (int p, int w, int d, int l, int gf, int ga) s, int goalsFor, int goalsAgainst)
        {
            s.p++;
            s.gf += goalsFor;
            s.ga += goalsAgainst;
            if (goalsFor > goalsAgainst) s.w++;
            else if (goalsFor < goalsAgainst) s.l++;
            else s.d++;
        }
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

    #region Kvindeligaen playoffs

    private static int KvindeligaenRegularEndRound => KvindeligaenRegularSeasonRounds;
    private const int KvindeligaenChampRoundsNum = 6;
    private const int KvindeligaenRelRoundsNum = 5;

    public string KvindeligaenSeasonStr => $"{CurrentSeasonYear}/{CurrentSeasonYear + 1}";

    public async Task AfterKvindeligaenLeagueDayAsync(DateTime date)
    {
        await TryGenerateKvindeligaenPostRegularAsync();
        await TryAdvanceKvindeligaenBo3Async();
    }

    private async Task TryAdvanceKvindeligaenBo3Async()
    {
        string season = KvindeligaenSeasonStr;
        bool hasPostRegular = await _db.LeagueFixtures.AnyAsync(f =>
            f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.Round > KvindeligaenRegularEndRound);

        if (!hasPostRegular) return;

        if (await KvindeligaenGroupsCompleteAsync(season))
            await EnsureKvindeligaenSemiLegsAsync(season);

        await ExtendKvindeligaenSeriesAsync(season, "SF1");
        await ExtendKvindeligaenSeriesAsync(season, "SF2");
        await EnsureKvindeligaenFinalThirdFirstAsync(season);
        await ExtendKvindeligaenSeriesAsync(season, "FIN");
        await ExtendKvindeligaenSeriesAsync(season, "THIRD");
    }

    private async Task<bool> AnyUnplayedKvindeligaenRegularAsync(string season) =>
        await _db.LeagueFixtures.AnyAsync(f =>
            f.Season == season && f.CompetitionName == KvindeligaenCompetition && !f.IsPlayed &&
            f.Round <= KvindeligaenRegularEndRound && (f.Phase == "Regular" || f.Phase == ""));

    public async Task TryGenerateKvindeligaenPostRegularAsync()
    {
        string season = KvindeligaenSeasonStr;
        if (await _db.LeagueFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.Round > KvindeligaenRegularEndRound))
            return;
        if (await AnyUnplayedKvindeligaenRegularAsync(season)) return;

        var entries = await _db.LeagueEntries.Include(e => e.Team)
            .Where(e => e.CompetitionName == KvindeligaenCompetition).ToListAsync();
        var ranked = entries.OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).ToList();
        if (ranked.Count < 14) return;

        var top8Teams = ranked.Take(8).Select(e => e.Team!).ToList();
        var relTeams = ranked.Skip(8).Take(6).Select(e => e.Team!).ToList();

        var shuffled = top8Teams.OrderBy(_ => _kvPlayoffRng.Next()).ToList();
        var groupA = shuffled.Take(4).Select(t => t.Id).ToList();
        var groupB = shuffled.Skip(4).Take(4).Select(t => t.Id).ToList();

        foreach (var tid in groupA.Concat(groupB).Concat(relTeams.Select(t => t.Id)))
        {
            var e = ranked.First(x => x.TeamId == tid);
            e.Played = e.Won = e.Drawn = e.Lost = 0;
            e.GoalsFor = e.GoalsAgainst = 0;
        }
        await _db.SaveChangesAsync();

        int r = KvindeligaenRegularEndRound + 1;
        var dblA = KvindeligaenDoubleRoundRobinPairings(groupA);
        var dblB = KvindeligaenDoubleRoundRobinPairings(groupB);
        for (int i = 0; i < KvindeligaenChampRoundsNum; i++)
        {
            foreach (var (home, away) in dblA[i])
                _db.LeagueFixtures.Add(KvindeligaenNewFx(season, r, "ChampGroupA", home, away));
            foreach (var (home, away) in dblB[i])
                _db.LeagueFixtures.Add(KvindeligaenNewFx(season, r, "ChampGroupB", home, away));
            r++;
        }

        int relStart = KvindeligaenRegularEndRound + 1; // same starting round as championship
        var relIds = relTeams.Select(t => t.Id).ToList();
        var srr = KvindeligaenSingleRoundRobinPairings(relIds);
        for (int i = 0; i < KvindeligaenRelRoundsNum; i++)
        {
            foreach (var (home, away) in srr[i])
                _db.LeagueFixtures.Add(KvindeligaenNewFx(season, relStart + i, "Relegation", home, away));
        }

        await _db.SaveChangesAsync();
    }

    private static LeagueFixture KvindeligaenNewFx(string season, int round, string phase, int home, int away) => new()
    {
        Season = season, CompetitionName = KvindeligaenCompetition, Round = round, Phase = phase,
        HomeTeamId = home, AwayTeamId = away
    };

    private static List<List<(int home, int away)>> KvindeligaenSingleRoundRobinPairings(List<int> teamIds)
    {
        var teams = new List<int>(teamIds);
        if (teams.Count % 2 != 0) teams.Add(-1);
        int n = teams.Count;
        var rotating = teams.Skip(1).ToList();
        var rounds = new List<List<(int, int)>>();
        for (int round = 0; round < n - 1; round++)
        {
            var cur = new List<int> { teams[0] };
            cur.AddRange(rotating);
            var matches = new List<(int, int)>();
            for (int i = 0; i < n / 2; i++)
            {
                int a = cur[i], b = cur[n - 1 - i];
                if (a != -1 && b != -1) matches.Add((a, b));
            }
            rounds.Add(matches);
            var last = rotating[^1];
            rotating.RemoveAt(rotating.Count - 1);
            rotating.Insert(0, last);
        }
        return rounds;
    }

    private static List<List<(int home, int away)>> KvindeligaenDoubleRoundRobinPairings(List<int> teamIds)
    {
        var first = KvindeligaenSingleRoundRobinPairings(teamIds);
        var all = new List<List<(int, int)>>();
        all.AddRange(first);
        all.AddRange(first.Select(rnd => rnd.Select(m => (m.away, m.home)).ToList()));
        return all;
    }

    private async Task<bool> KvindeligaenGroupsCompleteAsync(string season) =>
        !await _db.LeagueFixtures.AnyAsync(f =>
            f.Season == season && f.CompetitionName == KvindeligaenCompetition && !f.IsPlayed &&
            (f.Phase == "ChampGroupA" || f.Phase == "ChampGroupB" || f.Phase == "Relegation"));

    private async Task EnsureKvindeligaenSemiLegsAsync(string season)
    {
        if (await _db.LeagueFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.PlayoffSeriesId != null))
            return;
        if (!await KvindeligaenGroupsCompleteAsync(season)) return;

        var a = await KvindeligaenTopTwoForPhaseAsync(season, "ChampGroupA");
        var b = await KvindeligaenTopTwoForPhaseAsync(season, "ChampGroupB");
        if (a.Count < 2 || b.Count < 2) return;

        int a1 = a[0], a2 = a[1], b1 = b[0], b2 = b[1];
        int round = await NextKvindeligaenFreeRoundAsync(season);

        bool hahSf1 = _kvPlayoffRng.Next(2) == 0;
        bool hahSf2 = _kvPlayoffRng.Next(2) == 0;
        KvindeligaenAddFirstBo3(season, round, "SF1", a1, b2, hahSf1);
        KvindeligaenAddFirstBo3(season, round, "SF2", b1, a2, hahSf2);
        await _db.SaveChangesAsync();
    }

    private void KvindeligaenAddFirstBo3(string season, int round, string series, int t1, int t2, bool t1HomeGame1)
    {
        int h = t1HomeGame1 ? t1 : t2;
        int a = t1HomeGame1 ? t2 : t1;
        _db.LeagueFixtures.Add(new LeagueFixture
        {
            Season = season, CompetitionName = KvindeligaenCompetition, Round = round, Phase = "KvBo3",
            PlayoffSeriesId = series, PlayoffLeg = 1, HomeTeamId = h, AwayTeamId = a
        });
    }

    private async Task<List<int>> KvindeligaenTopTwoForPhaseAsync(string season, string phase)
    {
        var fixtures = await _db.LeagueFixtures
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.Phase == phase)
            .ToListAsync();

        var teamIds = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();

        var rows = new List<(int id, int pts, int gd, int gf)>();
        foreach (var tid in teamIds)
        {
            var e = await _db.LeagueEntries.FirstOrDefaultAsync(x => x.TeamId == tid && x.CompetitionName == KvindeligaenCompetition);
            if (e == null) continue;
            rows.Add((tid, e.Points, e.GoalDifference, e.GoalsFor));
        }

        return rows.OrderByDescending(x => x.pts).ThenByDescending(x => x.gd).ThenByDescending(x => x.gf)
            .Take(2).Select(x => x.id).ToList();
    }

    private async Task ExtendKvindeligaenSeriesAsync(string season, string series)
    {
        var all = await _db.LeagueFixtures
            .Include(f => f.MatchRecord)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.PlayoffSeriesId == series)
            .OrderBy(f => f.PlayoffLeg).ToListAsync();

        if (all.Count == 0) return;
        if (all.Any(f => !f.IsPlayed)) return;

        var played = all.Where(f => f.IsPlayed && f.MatchRecord != null).ToList();
        if (played.Count == 0) return;

        var (x, y) = KvindeligaenTwoTeams(played);
        var (wx, wy) = KvindeligaenCountWins(played, x, y);
        if (wx >= 2 || wy >= 2) return;

        var g1 = played[0];
        int leg = played.Count + 1;
        int home, away;
        if (leg == 2)
        {
            home = g1.AwayTeamId;
            away = g1.HomeTeamId;
        }
        else
        {
            home = g1.HomeTeamId;
            away = g1.AwayTeamId;
        }

        int round = await NextKvindeligaenFreeRoundAsync(season);
        _db.LeagueFixtures.Add(new LeagueFixture
        {
            Season = season, CompetitionName = KvindeligaenCompetition, Round = round, Phase = "KvBo3",
            PlayoffSeriesId = series, PlayoffLeg = leg,
            HomeTeamId = home, AwayTeamId = away
        });
        await _db.SaveChangesAsync();
    }

    private async Task EnsureKvindeligaenFinalThirdFirstAsync(string season)
    {
        if (await _db.LeagueFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition &&
                                                   (f.PlayoffSeriesId == "FIN" || f.PlayoffSeriesId == "THIRD")))
            return;

        var sf1 = await KvindeligaenSeriesCompleteWinnerAsync(season, "SF1");
        var sf2 = await KvindeligaenSeriesCompleteWinnerAsync(season, "SF2");
        if (sf1 == null || sf2 == null) return;

        var legs1 = await KvindeligaenPlayedSeriesAsync(season, "SF1");
        var legs2 = await KvindeligaenPlayedSeriesAsync(season, "SF2");
        int l1 = KvindeligaenLoserOfSeries(legs1);
        int l2 = KvindeligaenLoserOfSeries(legs2);

        int round = await NextKvindeligaenFreeRoundAsync(season);
        bool finH = _kvPlayoffRng.Next(2) == 0;
        KvindeligaenAddFirstBo3(season, round, "FIN", sf1.Value, sf2.Value, finH);
        bool thH = _kvPlayoffRng.Next(2) == 0;
        KvindeligaenAddFirstBo3(season, round, "THIRD", l1, l2, thH);
        await _db.SaveChangesAsync();
    }

    private async Task<List<LeagueFixture>> KvindeligaenPlayedSeriesAsync(string season, string series) =>
        await _db.LeagueFixtures.Include(f => f.MatchRecord)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.PlayoffSeriesId == series && f.IsPlayed)
            .OrderBy(f => f.PlayoffLeg).ToListAsync();

    private async Task<int?> KvindeligaenSeriesCompleteWinnerAsync(string season, string series)
    {
        var legs = await KvindeligaenPlayedSeriesAsync(season, series);
        if (legs.Count == 0) return null;
        var (x, y) = KvindeligaenTwoTeams(legs);
        var (wx, wy) = KvindeligaenCountWins(legs, x, y);
        if (wx < 2 && wy < 2) return null;
        return wx > wy ? x : y;
    }

    private static int KvindeligaenLoserOfSeries(List<LeagueFixture> legs)
    {
        var (x, y) = KvindeligaenTwoTeams(legs);
        var (wx, wy) = KvindeligaenCountWins(legs, x, y);
        return wx > wy ? y : x;
    }

    private static (int x, int y) KvindeligaenTwoTeams(List<LeagueFixture> legs)
    {
        var ids = legs.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().OrderBy(i => i).ToList();
        return (ids[0], ids[1]);
    }

    private static (int wx, int wy) KvindeligaenCountWins(List<LeagueFixture> legs, int x, int y)
    {
        int wx = 0, wy = 0;
        foreach (var f in legs)
        {
            if (f.MatchRecord == null) continue;
            int wid = KvindeligaenWinnerId(f);
            if (wid == x) wx++;
            else if (wid == y) wy++;
        }
        return (wx, wy);
    }

    private static int KvindeligaenWinnerId(LeagueFixture f)
    {
        var r = f.MatchRecord!;
        bool homeWins = r.HomeGoals > r.AwayGoals ||
                        (r.HomeGoals == r.AwayGoals && r.HomePenaltyGoals > r.AwayPenaltyGoals);
        return homeWins ? f.HomeTeamId : f.AwayTeamId;
    }

    private async Task<int> NextKvindeligaenFreeRoundAsync(string season)
    {
        var max = await _db.LeagueFixtures.Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition)
            .MaxAsync(f => (int?)f.Round) ?? 0;
        return max + 1;
    }

    public async Task<Team?> GetKvindeligaenPlayoffChampionTeamAsync(string season)
    {
        var fin = await _db.LeagueFixtures.Include(f => f.MatchRecord).Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.PlayoffSeriesId == "FIN" && f.IsPlayed && f.MatchRecord != null)
            .OrderByDescending(f => f.PlayoffLeg).ToListAsync();
        if (fin.Count == 0) return null;
        var (x, y) = KvindeligaenTwoTeams(fin);
        var (wx, wy) = KvindeligaenCountWins(fin, x, y);
        if (wx < 2 && wy < 2) return null;
        int wid = wx > wy ? x : y;
        return await _db.Teams.FirstOrDefaultAsync(t => t.Id == wid);
    }

    public async Task<Team?> GetKvindeligaenPlayoffFinalLoserTeamAsync(string season)
    {
        var fin = await _db.LeagueFixtures.Include(f => f.MatchRecord)
            .Where(f => f.Season == season && f.CompetitionName == KvindeligaenCompetition && f.PlayoffSeriesId == "FIN" && f.IsPlayed && f.MatchRecord != null)
            .OrderBy(f => f.PlayoffLeg).ToListAsync();
        if (fin.Count == 0) return null;
        var pair = KvindeligaenTwoTeams(fin);
        var (wx, wy) = KvindeligaenCountWins(fin, pair.x, pair.y);
        if (wx < 2 && wy < 2) return null;
        int lid = wx > wy ? pair.y : pair.x;
        return await _db.Teams.FirstOrDefaultAsync(t => t.Id == lid);
    }

    #endregion
}

public sealed class KvKnockoutFixtureRow
{
    public string Caption { get; init; } = "";
    public string HomeName { get; init; } = "";
    public string AwayName { get; init; } = "";
    public string ScoreText { get; init; } = "";
    public string HomeLogo { get; init; } = "";
    public string AwayLogo { get; init; } = "";
    public bool IsFinal { get; init; }
}