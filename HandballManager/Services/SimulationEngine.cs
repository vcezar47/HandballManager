using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

public class SimulationEngine
{
    private readonly HandballDbContext _db;
    private readonly PlayerProgressionService _progression;
    private readonly TransferService _transferService;
    private readonly YouthIntakeService _youthIntakeService;
    private readonly Random _rng = new();
    private const int PossessionsPerTeam = 55;
    private DateTime _lastDailyProgressionDate;
    private DateTime _lastWeeklyWageDate;

    public SimulationEngine(HandballDbContext db, PlayerProgressionService progression, TransferService transferService, YouthIntakeService youthIntakeService)
    {
        _db = db;
        _progression = progression;
        _transferService = transferService;
        _youthIntakeService = youthIntakeService;
        _lastDailyProgressionDate = LeagueService.GameSeasonStartDate;
        _lastWeeklyWageDate = LeagueService.GameSeasonStartDate;
    }

    /// <summary>
    /// Simulates all matches for the specified matchweek.
    /// </summary>
    public async Task<List<MatchRecord>> SimulateMatchweekAsync(int matchweek)
    {
        // Process daily progression for all elapsed days since last processing
        var matchDate = LeagueService.GetMatchweekDate(matchweek);
        await ProcessDailyProgressionAsync(matchDate);

        var teams = await _db.Teams.Select(t => t.Id).ToListAsync();
        var pairings = LeagueService.GetPairingsForMatchweek(teams, matchweek);

        var results = new List<MatchRecord>();
        foreach (var (homeId, awayId) in pairings)
        {
            var result = await SimulateMatchInternalAsync(homeId, awayId, matchweek);
            results.Add(result);
        }

        await _db.SaveChangesAsync();
        return results;
    }

    /// <summary>
    /// Processes daily attribute progression for all players, catching up
    /// for however many days have elapsed since last processing.
    /// </summary>
    public async Task ProcessDailyProgressionAsync(DateTime currentDate)
    {
        int daysSinceLast = (int)(currentDate.Date - _lastDailyProgressionDate.Date).TotalDays;
        if (daysSinceLast <= 0) return;

        await _transferService.ReleaseExpiredContractsAsync(currentDate);
        await _transferService.ProcessPendingTransfersAsync(currentDate);
        await _transferService.ExpireStaleOffersAsync(currentDate);
        await _youthIntakeService.GenerateIntakeForDateIfNeededAsync(currentDate);
        await _youthIntakeService.SignAiYouthPlayersAsync(currentDate);
        await _transferService.TryGenerateAiOfferAsync(currentDate);
        await _transferService.TryGenerateAiToAiTransfersAsync(currentDate);

        var allPlayers = await _db.Players.ToListAsync();
        foreach (var player in allPlayers)
        {
            _progression.ProcessDailyProgression(player, daysSinceLast);
        }

        // Check if a week has passed for wage deductions
        int wageDaysSinceLast = (int)(currentDate.Date - _lastWeeklyWageDate.Date).TotalDays;
        if (wageDaysSinceLast >= 7)
        {
            int weeksElapsed = wageDaysSinceLast / 7;
            await ProcessWeeklyWagesAsync(weeksElapsed);

            // Advance the last wage date by exact weeks to maintain the weekly cycle
            _lastWeeklyWageDate = _lastWeeklyWageDate.AddDays(weeksElapsed * 7);
        }

        await _db.SaveChangesAsync();
        _lastDailyProgressionDate = currentDate.Date;
    }

    private async Task ProcessWeeklyWagesAsync(int weeksElapsed)
    {
        var allTeams = await _db.Teams.Include(t => t.Players).ToListAsync();
        foreach (var team in allTeams)
        {
            // Calculate total weekly wage bill for the team
            // (MonthlyWage * 12) / 52 is roughly weekly, but since we already store MonthlyWage,
            // we figure out how much is owed for 1 week.
            decimal weeklyWageBill = team.Players.Sum(p => (p.MonthlyWage * 12m) / 52m);

            // Deduct from club balance 
            team.ClubBalance -= (weeklyWageBill * weeksElapsed);
        }
    }

    private async Task<MatchRecord> SimulateMatchInternalAsync(int homeTeamId, int awayTeamId, int matchweek)
    {
        var homeTeam = await _db.Teams.Include(t => t.Players).FirstAsync(t => t.Id == homeTeamId);
        var awayTeam = await _db.Teams.Include(t => t.Players).FirstAsync(t => t.Id == awayTeamId);

        var record = new MatchRecord
        {
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeTeamName = homeTeam.Name,
            AwayTeamName = awayTeam.Name,
            HomeTeamLogo = homeTeam.LogoPath,
            AwayTeamLogo = awayTeam.LogoPath,
            PlayedOn = LeagueService.GetMatchweekDate(matchweek),
            MatchweekNumber = matchweek,
        };

        var playerStats = new Dictionary<int, MatchPlayerStat>();
        foreach (var p in homeTeam.Players.Concat(awayTeam.Players))
        {
            playerStats[p.Id] = new MatchPlayerStat
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                TeamId = p.TeamId ?? 0,
                MatchRecord = record
            };
        }

        // Simulating second half logic roughly
        SimulatePossessions(record, homeTeam, awayTeam, playerStats, true);
        SimulatePossessions(record, awayTeam, homeTeam, playerStats, false);

        // Finalize stats and ratings
        CalculateMatchRatings(record, homeTeam, awayTeam, playerStats);

        record.HomeGoals = record.MatchEvents.Count(e => e.TeamId == homeTeamId && e.EventType == "Goal");
        record.AwayGoals = record.MatchEvents.Count(e => e.TeamId == awayTeamId && e.EventType == "Goal");

        _db.MatchRecords.Add(record);
        record.PlayerStats.AddRange(playerStats.Values.Where(ps => ps.Goals > 0 || ps.Assists > 0 || ps.Saves > 0 || ps.Rating > 0));

        // Update league standings
        await UpdateLeagueEntryAsync(homeTeamId, record.HomeGoals, record.AwayGoals);
        await UpdateLeagueEntryAsync(awayTeamId, record.AwayGoals, record.HomeGoals);

        // Update player seasonal stats and apply progression
        foreach (var player in homeTeam.Players.Concat(awayTeam.Players))
        {
            if (playerStats.TryGetValue(player.Id, out var stat))
            {
                player.SeasonGoals += stat.Goals;
                player.SeasonAssists += stat.Assists;
                player.SeasonSaves += stat.Saves;
                player.MatchesPlayed++;
                player.SeasonRatingSum += stat.Rating;

                // Apply progression/regression based on match performance
                _progression.ProcessMatchProgression(player, stat, matchweek);
            }
        }

        return record;
    }

    private void SimulatePossessions(MatchRecord record, Team attackers, Team defenders, Dictionary<int, MatchPlayerStat> stats, bool isAttackerHome)
    {
        var attackRating = CalculateAttackRating(attackers);
        var defenseRating = CalculateDefenseRating(defenders);
        var gk = defenders.Players.FirstOrDefault(p => p.Position == "GK") ?? defenders.Players.First();
        var gkRating = (gk.Reflexes * 2.5 + gk.OneOnOnes * 2.0 + gk.Handling + gk.Positioning * 1.5 + gk.Anticipation) / 8.0;

        double homeBonus = isAttackerHome ? 1.05 : 1.0;

        for (int i = 0; i < PossessionsPerTeam; i++)
        {
            double diff = (attackRating * homeBonus) - defenseRating;
            double shotChance = Math.Clamp(0.6 + (diff / 40.0), 0.3, 0.9);

            if (_rng.NextDouble() > shotChance) continue;

            // Pick a shooter
            var shooter = PickPlayerForAction(attackers, "Goal");
            if (shooter == null) continue;

            double goalChance = Math.Clamp(0.7 + (shooter.Finishing - gkRating) / 30.0, 0.35, 0.95);

            if (_rng.NextDouble() < goalChance)
            {
                // GOAL
                stats[shooter.Id].Goals++;
                var ev = new MatchEvent
                {
                    MatchRecord = record,
                    TeamId = attackers.Id,
                    PlayerId = shooter.Id,
                    PlayerName = shooter.Name,
                    EventType = "Goal",
                    Minute = _rng.Next(1, 60)
                };
                record.MatchEvents.Add(ev);

                // Potentially an assist
                if (_rng.NextDouble() < 0.8) // 80% chance of assisted goal in handball
                {
                    var assistant = PickPlayerForAction(attackers, "Assist", shooter.Id);
                    if (assistant != null)
                    {
                        stats[assistant.Id].Assists++;
                        record.MatchEvents.Add(new MatchEvent
                        {
                            MatchRecord = record,
                            TeamId = attackers.Id,
                            PlayerId = assistant.Id,
                            PlayerName = assistant.Name,
                            EventType = "Assist",
                            Minute = ev.Minute
                        });
                    }
                }
            }
            else
            {
                // SAVE
                if (stats.ContainsKey(gk.Id))
                    stats[gk.Id].Saves++;
            }
        }
    }

    private Player? PickPlayerForAction(Team team, string action, int excludePlayerId = -1)
    {
        var candidates = team.Players.Where(p => p.Id != excludePlayerId).ToList();
        if (!candidates.Any()) return null;

        // Weights based on position
        var weights = candidates.Select(p => {
            double w = 1.0;
            if (action == "Goal")
            {
                w = p.Position switch
                {
                    "LB" or "RB" => 3.0,
                    "LW" or "RW" => 2.5,
                    "Pivot" => 2.0,
                    "CB" => 1.5,
                    _ => 0.1
                };
                w *= (p.Finishing / 10.0);
            }
            else // Assist
            {
                w = p.Position switch
                {
                    "CB" => 4.0,
                    "LB" or "RB" => 2.0,
                    "Pivot" => 1.5,
                    _ => 0.5
                };
                w *= (p.Passing / 10.0);
            }
            return w;
        }).ToList();

        double totalWeight = weights.Sum();
        double r = _rng.NextDouble() * totalWeight;
        double current = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            current += weights[i];
            if (r <= current) return candidates[i];
        }
        return candidates.Last();
    }

    private double CalculateAttackRating(Team team)
    {
        var players = team.Players.Where(p => p.Position != "GK").ToList();
        return players.Any() ? players.Average(p => (p.Finishing + p.Passing + p.Technique + p.Decisions + p.Pace + p.Acceleration) / 6.0) : 10;
    }

    private double CalculateDefenseRating(Team team)
    {
        var players = team.Players.Where(p => p.Position != "GK").ToList();
        return players.Any() ? players.Average(p => (p.Marking + p.Tackling + p.Positioning + p.Strength + p.Aggression + p.Anticipation) / 6.0) : 10;
    }

    private void CalculateMatchRatings(MatchRecord record, Team home, Team away, Dictionary<int, MatchPlayerStat> stats)
    {
        foreach (var p in home.Players.Concat(away.Players))
        {
            if (!stats.TryGetValue(p.Id, out var stat)) continue;

            double baseRating = 6.0;
            // Contribution bonuses
            baseRating += stat.Goals * 0.4;
            baseRating += stat.Assists * 0.3;
            baseRating += stat.Saves * 0.25;

            // Attribute influence (did they play well relative to their quality?)
            baseRating += (p.Overall100 / 25.0) - 2.0; // -2 to +2 based on quality

            // Result bonus/penalty
            bool isHome = p.TeamId == home.Id;
            int myGoals = isHome ? record.HomeGoals : record.AwayGoals;
            int oppGoals = isHome ? record.AwayGoals : record.HomeGoals;

            if (myGoals > oppGoals) baseRating += 0.5;
            else if (myGoals < oppGoals) baseRating -= 0.3;

            stat.Rating = Math.Clamp(baseRating + (_rng.NextDouble() - 0.5), 4.0, 10.0);
        }
    }

    private async Task UpdateLeagueEntryAsync(int teamId, int goalsFor, int goalsAgainst)
    {
        var entry = await _db.LeagueEntries.FirstOrDefaultAsync(e => e.TeamId == teamId);
        if (entry is null) return;

        entry.Played++;
        entry.GoalsFor += goalsFor;
        entry.GoalsAgainst += goalsAgainst;

        if (goalsFor > goalsAgainst) entry.Won++;
        else if (goalsFor == goalsAgainst) entry.Drawn++;
        else entry.Lost++;
    }

    /// <summary>
    /// Ages all players, resets seasonal stats, and recalculates progression phases.
    /// Should be called once after the final matchweek.
    /// </summary>
    public async Task ProcessEndOfSeasonAsync(DateTime currentDate)
    {
        var toRetire = await _db.Players.IgnoreQueryFilters().Where(p => p.IsRetiringAtEndOfSeason).ToListAsync();
        foreach (var p in toRetire)
            p.IsRetired = true;

        var allPlayers = await _db.Players.ToListAsync();
        _progression.ProcessEndOfSeason(allPlayers);

        var allEntries = await _db.LeagueEntries
            .Include(e => e.Team)
            .ToListAsync();

        // Determine Champion before wiping entries
        var winnerEntry = allEntries
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .FirstOrDefault();

        if (winnerEntry?.Team != null)
        {
            var champRecord = new ChampionRecord
            {
                Season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}",
                TeamName = winnerEntry.Team.Name
            };
            _db.ChampionRecords.Add(champRecord);
        }

        foreach (var entry in allEntries)
        {
            entry.Played = 0;
            entry.Won = 0;
            entry.Drawn = 0;
            entry.Lost = 0;
            entry.GoalsFor = 0;
            entry.GoalsAgainst = 0;
        }

        // Wipe records for the new season
        var allMatchRecords = await _db.MatchRecords.ToListAsync();
        var allMatchEvents = await _db.MatchEvents.ToListAsync();
        var allMatchStats = await _db.MatchPlayerStats.ToListAsync();

        _db.MatchPlayerStats.RemoveRange(allMatchStats);
        _db.MatchEvents.RemoveRange(allMatchEvents);
        _db.MatchRecords.RemoveRange(allMatchRecords);

        LeagueService.AdvanceToNextSeason();
        // Continue daily / weekly progression seamlessly from the current in-game date
        _lastDailyProgressionDate = currentDate.Date;
        _lastWeeklyWageDate = currentDate.Date;

        await _db.SaveChangesAsync();

        var activePlayers = await _db.Players.IgnoreQueryFilters().Where(p => !p.IsRetired).ToListAsync();
        foreach (var p in activePlayers)
        {
            int age = currentDate.Year - p.Birthdate.Year;
            if (p.Birthdate.Date > currentDate.AddYears(-age)) age--;
            if (age < 35) continue;
            int chance = 30 + (age - 35) * 10;
            if (chance > 100) chance = 100;
            if (_rng.Next(1, 101) <= chance)
            {
                p.IsRetiringAtEndOfSeason = true;
                _db.NewsItems.Add(new NewsItem
                {
                    Title = $"{p.Name} announces retirement",
                    Body = $"{p.Name} ({p.Team?.Name}) has announced they will retire at the end of the season.",
                    PublishedAt = currentDate,
                    NewsType = "RetirementAnnouncement"
                });
            }
        }
        await _db.SaveChangesAsync();
    }
}

// Extension to avoid compilation error if I forget a field
public static class PlayerExtensions
{
    public static int Reflexes_Placeholder(this Player p) => (p.Agility + p.Anticipation) / 2;
}