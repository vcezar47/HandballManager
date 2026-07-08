using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class SupercupService
{
    private readonly HandballDbContext _db;
    private readonly Random _rng = new();

    public SupercupService(HandballDbContext db)
    {
        _db = db;
    }

    public static (DateTime Saturday, DateTime Sunday) GetSupercupDates(int year)
    {
        var d = new DateTime(year, 8, 1);
        int saturdays = 0;
        while (d.Month == 8)
        {
            if (d.DayOfWeek == DayOfWeek.Saturday)
            {
                saturdays++;
                // Late August is usually the 4th Saturday
                if (saturdays == 4) return (d, d.AddDays(1));
            }
            d = d.AddDays(1);
        }
        return (new DateTime(year, 8, 23), new DateTime(year, 8, 24));
    }

    public async Task InitializeInitialSupercupAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == season))
            return;

        var (saturday, sunday) = GetSupercupDates(LeagueService.CurrentSeasonYear);
        
        // Filter for Romanian teams only
        var teams = await _db.Teams.Where(t => t.CompetitionName == "Liga Florilor").ToListAsync();
        var csm = teams.FirstOrDefault(t => t.Name == "CSM București");
        var bistrita = teams.FirstOrDefault(t => t.Name == "Gloria Bistrița");
        var corona = teams.FirstOrDefault(t => t.Name == "CSM Corona Brașov");
        var minaur = teams.FirstOrDefault(t => t.Name == "Minaur Baia Mare");

        const string venue = "TeraPlast Arena, Bistrița";

        if (csm != null && bistrita != null && corona != null && minaur != null)
        {
            // Specifically requested pairings: 
            // Semi 1: CSM Bucuresti vs Minaur Baia Mare
            // Semi 2: Corona Brasov vs Gloria Bistrita
            var semi1 = new SupercupFixture
            {
                Season = season,
                CompetitionName = "Liga Florilor",
                HomeTeamId = csm.Id,
                AwayTeamId = minaur.Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            var semi2 = new SupercupFixture
            {
                Season = season,
                CompetitionName = "Liga Florilor",
                HomeTeamId = corona.Id,
                AwayTeamId = bistrita.Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            _db.SupercupFixtures.AddRange(semi1, semi2);
        }
        else
        {
            // Fallback: take top 4 teams if naming convention changed
            var fallbackTeams = teams.Take(4).ToList();
            if (fallbackTeams.Count < 4) return;
            
            var semi1 = new SupercupFixture
            {
                Season = season,
                CompetitionName = "Liga Florilor",
                HomeTeamId = fallbackTeams[0].Id,
                AwayTeamId = fallbackTeams[3].Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            var semi2 = new SupercupFixture
            {
                Season = season,
                CompetitionName = "Liga Florilor",
                HomeTeamId = fallbackTeams[1].Id,
                AwayTeamId = fallbackTeams[2].Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            _db.SupercupFixtures.AddRange(semi1, semi2);
        }

        await _db.SaveChangesAsync();
    }

    public async Task GenerateNextSupercupAsync(List<Team> sortedLeagueTeams, List<Team> cupFinalists)
    {
        // This will be called BEFORE LeagueService.AdvanceToNextSeason(),
        // so the 'next' season is CurrentSeasonYear + 1
        int nextYear = LeagueService.CurrentSeasonYear + 1;
        string season = $"{nextYear}/{nextYear + 1}";

        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == season))
            return;

        var participants = new List<Team>();
        if (cupFinalists != null)
        {
            foreach (var f in cupFinalists)
            {
                if (f != null && f.CompetitionName == "Liga Florilor") 
                    participants.Add(f);
            }
        }

        foreach (var t in sortedLeagueTeams)
        {
            if (participants.Count >= 4) break;
            if (t.CompetitionName == "Liga Florilor" && !participants.Any(p => p.Id == t.Id))
                participants.Add(t);
        }

        // Shuffle for semifinal draw
        var shuffled = participants.OrderBy(_ => _rng.Next()).ToList();
        
        var (saturday, sunday) = GetSupercupDates(nextYear);
        string compName = participants[0].CompetitionName;
        var venueTeam = (await _db.Teams.Where(t => t.CompetitionName == compName && t.StadiumCapacity > 1500).ToListAsync())
            .OrderBy(_ => _rng.Next()).FirstOrDefault() ?? participants[0];
        string venue = $"{venueTeam.StadiumName}, {venueTeam.City}";

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season,
            CompetitionName = "Liga Florilor",
            HomeTeamId = shuffled[0].Id, AwayTeamId = shuffled[1].Id,
            ScheduledDate = saturday, Round = "SemiFinal", VenueName = venue
        });
        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season,
            CompetitionName = "Liga Florilor",
            HomeTeamId = shuffled[2].Id, AwayTeamId = shuffled[3].Id,
            ScheduledDate = saturday, Round = "SemiFinal", VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>2025 Bambuni Super Cup: Odense vs Esbjerg (hardcoded).</summary>
    public async Task InitializeDanishSupercupAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == LeagueService.KvindeligaenCompetition))
            return;

        var odense = await _db.Teams.FirstOrDefaultAsync(t =>
            t.CompetitionName == LeagueService.KvindeligaenCompetition && t.Name == "Odense Håndbold");
        var esbjerg = await _db.Teams.FirstOrDefaultAsync(t =>
            t.CompetitionName == LeagueService.KvindeligaenCompetition && t.Name == "Team Esbjerg");
        if (odense == null || esbjerg == null) return;

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season,
            CompetitionName = LeagueService.KvindeligaenCompetition,
            HomeTeamId = odense.Id,
            AwayTeamId = esbjerg.Id,
            ScheduledDate = new DateTime(2025, 8, 24),
            Round = "Final",
            VenueName = "Biocirc Arena, Esbjerg"
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Next-season Danish supercup: league champion vs cup winner, or grand-final loser if same club won both.</summary>
    public async Task GenerateNextDanishSupercupAsync(LeagueService leagueService)
    {
        int nextYear = LeagueService.CurrentSeasonYear + 1;
        string nextSeason = $"{nextYear}/{nextYear + 1}";
        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == nextSeason && f.CompetitionName == LeagueService.KvindeligaenCompetition))
            return;

        string prevSeason = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var leagueWinner = await leagueService.GetKvindeligaenPlayoffChampionTeamAsync(prevSeason);
        var cupFinal = await _db.CupFixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.Season == prevSeason && f.CompetitionName == LeagueService.KvindeligaenCompetition
                && f.Round == "Final" && f.IsPlayed);

        Team? cupWinner = null;
        if (cupFinal != null)
            cupWinner = cupFinal.HomeGoals > cupFinal.AwayGoals ? cupFinal.HomeTeam
                : (cupFinal.HomeGoals == cupFinal.AwayGoals && cupFinal.HomePenaltyGoals > cupFinal.AwayPenaltyGoals) ? cupFinal.HomeTeam : cupFinal.AwayTeam;

        if (leagueWinner == null || cupWinner == null) return;

        Team homeTeam = leagueWinner;
        Team awayTeam = cupWinner;
        if (homeTeam.Id == awayTeam.Id)
        {
            var loser = await leagueService.GetKvindeligaenPlayoffFinalLoserTeamAsync(prevSeason);
            if (loser == null) return;
            awayTeam = loser;
        }

        var (saturday, _) = GetSupercupDates(nextYear);
        var venueTeam = (await _db.Teams.Where(t => t.CompetitionName == LeagueService.KvindeligaenCompetition && t.StadiumCapacity > 1000)
            .ToListAsync()).OrderBy(_ => _rng.Next()).FirstOrDefault() ?? homeTeam;
        string venue = $"{venueTeam.StadiumName}, {venueTeam.City}";

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = nextSeason,
            CompetitionName = LeagueService.KvindeligaenCompetition,
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            ScheduledDate = saturday,
            Round = "Final",
            VenueName = venue
        });
        await _db.SaveChangesAsync();
    }

    public async Task TryGenerateFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        var sfFixtures = await _db.SupercupFixtures
            .Where(f => f.Season == season && f.Round == "SemiFinal")
            .OrderBy(f => f.Id)
            .ToListAsync();

        if (sfFixtures.Count != 2 || sfFixtures.Any(f => !f.IsPlayed))
            return;

        string compName = sfFixtures[0].CompetitionName;

        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == season && f.CompetitionName == compName && f.Round == "Final"))
            return;

        var (_, sunday) = GetSupercupDates(LeagueService.CurrentSeasonYear);
        // Use same venue from the semifinals
        string venue = sfFixtures[0].VenueName ?? "TeraPlast Arena, Bistrița";

        int Winner(SupercupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.HomeTeamId : f.AwayTeamId;
        int Loser(SupercupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.AwayTeamId : f.HomeTeamId;

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season,
            CompetitionName = compName,
            HomeTeamId = Loser(sfFixtures[0]), AwayTeamId = Loser(sfFixtures[1]),
            ScheduledDate = sunday, Round = "ThirdPlace", VenueName = venue
        });

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season,
            CompetitionName = compName,
            HomeTeamId = Winner(sfFixtures[0]), AwayTeamId = Winner(sfFixtures[1]),
            ScheduledDate = sunday, Round = "Final", VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    public async Task<List<DateTime>> GetAllDatesAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.SupercupFixtures
            .Where(f => f.Season == season)
            .Select(f => f.ScheduledDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }

    public async Task<List<SupercupFixture>> GetFixturesForDateAsync(DateTime date)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.SupercupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.ScheduledDate.Date == date.Date)
            .ToListAsync();
    }

    public async Task<List<SupercupFixture>> GetKnockoutFixturesAsync(string? competitionName = null)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var q = _db.SupercupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season);
        if (!string.IsNullOrEmpty(competitionName))
            q = q.Where(f => f.CompetitionName == competitionName);
        return await q.OrderBy(f => f.ScheduledDate).ThenBy(f => f.Id).ToListAsync();
    }

    public async Task<DateTime?> GetNextSupercupDateAsync(string? competitionName = null)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var q = _db.SupercupFixtures.Where(f => f.Season == season && !f.IsPlayed);
        if (!string.IsNullOrEmpty(competitionName))
            q = q.Where(f => f.CompetitionName == competitionName);
        var next = await q.OrderBy(f => f.ScheduledDate).FirstOrDefaultAsync();
        return next?.ScheduledDate.Date;
    }

    public async Task<DateTime?> GetNextSupercupDateForTeamAsync(int teamId)
    {
        var team = await _db.Teams.FirstAsync(t => t.Id == teamId);
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var next = await _db.SupercupFixtures
            .Where(f => f.Season == season && !f.IsPlayed && f.CompetitionName == team.CompetitionName &&
                        (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .OrderBy(f => f.ScheduledDate)
            .FirstOrDefaultAsync();
        return next?.ScheduledDate.Date;
    }

    public async Task<SupercupFixture?> GetFixtureForTeamOnDateAsync(int teamId, DateTime date)
    {
        var team = await _db.Teams.FirstAsync(t => t.Id == teamId);
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.SupercupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.CompetitionName == team.CompetitionName &&
                        f.ScheduledDate.Date == date.Date && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .FirstOrDefaultAsync();
    }
}
