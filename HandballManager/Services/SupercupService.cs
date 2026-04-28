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

    public void SeedHistoricalWinners()
    {
        if (_db.SupercupWinnerRecords.Any())
            return;

        var winners = new List<(string Team, int[] Years)>
        {
            ("CSM București", [2016, 2017, 2019, 2022, 2023, 2024]),
            ("HCM Baia Mare", [2013, 2014, 2015]),
            ("CS Oltchim Râmnicu Vâlcea", [2007, 2011]),
            ("SCM Râmnicu Vâlcea", [2018, 2020]),
            ("SCM Gloria Buzău", [2021])
        };

        foreach (var w in winners)
        {
            foreach (var year in w.Years)
            {
                _db.SupercupWinnerRecords.Add(new SupercupWinnerRecord
                {
                    Season = year.ToString(),
                    TeamName = w.Team
                });
            }
        }
        _db.SaveChanges();
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
                HomeTeamId = csm.Id,
                AwayTeamId = minaur.Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            var semi2 = new SupercupFixture
            {
                Season = season,
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
                HomeTeamId = fallbackTeams[0].Id,
                AwayTeamId = fallbackTeams[3].Id,
                ScheduledDate = saturday,
                Round = "SemiFinal",
                VenueName = venue
            };

            var semi2 = new SupercupFixture
            {
                Season = season,
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
            Season = season, HomeTeamId = shuffled[0].Id, AwayTeamId = shuffled[1].Id,
            ScheduledDate = saturday, Round = "SemiFinal", VenueName = venue
        });
        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season, HomeTeamId = shuffled[2].Id, AwayTeamId = shuffled[3].Id,
            ScheduledDate = saturday, Round = "SemiFinal", VenueName = venue
        });

        await _db.SaveChangesAsync();
    }

    public async Task TryGenerateFinalsAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        if (await _db.SupercupFixtures.AnyAsync(f => f.Season == season && f.Round == "Final"))
            return;

        var sfFixtures = await _db.SupercupFixtures
            .Where(f => f.Season == season && f.Round == "SemiFinal")
            .OrderBy(f => f.Id)
            .ToListAsync();

        if (sfFixtures.Count != 2 || sfFixtures.Any(f => !f.IsPlayed))
            return;

        var (_, sunday) = GetSupercupDates(LeagueService.CurrentSeasonYear);
        // Use same venue from the semifinals
        string venue = sfFixtures[0].VenueName ?? "TeraPlast Arena, Bistrița";

        int Winner(SupercupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.HomeTeamId : f.AwayTeamId;
        int Loser(SupercupFixture f) => (f.HomeGoals > f.AwayGoals || (f.HomeGoals == f.AwayGoals && f.HomePenaltyGoals > f.AwayPenaltyGoals)) ? f.AwayTeamId : f.HomeTeamId;

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season, HomeTeamId = Loser(sfFixtures[0]), AwayTeamId = Loser(sfFixtures[1]),
            ScheduledDate = sunday, Round = "ThirdPlace", VenueName = venue
        });

        _db.SupercupFixtures.Add(new SupercupFixture
        {
            Season = season, HomeTeamId = Winner(sfFixtures[0]), AwayTeamId = Winner(sfFixtures[1]),
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

    public async Task<List<SupercupFixture>> GetKnockoutFixturesAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.SupercupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season)
            .OrderBy(f => f.ScheduledDate)
            .ThenBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<DateTime?> GetNextSupercupDateAsync()
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var next = await _db.SupercupFixtures
            .Where(f => f.Season == season && !f.IsPlayed)
            .OrderBy(f => f.ScheduledDate)
            .FirstOrDefaultAsync();
        return next?.ScheduledDate.Date;
    }

    public async Task<DateTime?> GetNextSupercupDateForTeamAsync(int teamId)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var next = await _db.SupercupFixtures
            .Where(f => f.Season == season && !f.IsPlayed && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .OrderBy(f => f.ScheduledDate)
            .FirstOrDefaultAsync();
        return next?.ScheduledDate.Date;
    }

    public async Task<SupercupFixture?> GetFixtureForTeamOnDateAsync(int teamId, DateTime date)
    {
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        return await _db.SupercupFixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => f.Season == season && f.ScheduledDate.Date == date.Date && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
            .FirstOrDefaultAsync();
    }
}
