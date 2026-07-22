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
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;
    private readonly LeagueService _leagueService;
    private readonly FacilityService _facilityService;
    private readonly AwardsService _awards;
    private readonly Random _rng = new();
    private const int PossessionsPerTeam = 55;
    private DateTime _lastDailyProgressionDate;
    private DateTime _lastWeeklyWageDate;

    // Exposed so the save system can persist and restore this non-DB bookkeeping.
    public DateTime LastDailyProgressionDate => _lastDailyProgressionDate;
    public DateTime LastWeeklyWageDate => _lastWeeklyWageDate;

    /// <summary>Restores progression/wage cursors after loading a saved game.</summary>
    public void RestoreProgressionDates(DateTime lastDailyProgression, DateTime lastWeeklyWage)
    {
        _lastDailyProgressionDate = lastDailyProgression.Date;
        _lastWeeklyWageDate = lastWeeklyWage.Date;
    }

    public SimulationEngine(HandballDbContext db, PlayerProgressionService progression, TransferService transferService, YouthIntakeService youthIntakeService, CupService cupService, SupercupService supercupService, LeagueService leagueService, FacilityService facilityService)
    {
        _db = db;
        _progression = progression;
        _transferService = transferService;
        _youthIntakeService = youthIntakeService;
        _cupService = cupService;
        _supercupService = supercupService;
        _leagueService = leagueService;
        _facilityService = facilityService;
        // Derived from the same context rather than injected, so both hosts' existing
        // construction sites keep working unchanged.
        _awards = new AwardsService(db);
        _lastDailyProgressionDate = LeagueService.GameSeasonStartDate;
        _lastWeeklyWageDate = LeagueService.GameSeasonStartDate;
    }

    /// <summary>
    /// Simulates all matches for the specified matchweek.
    /// </summary>
    public async Task<List<MatchRecord>> SimulateMatchweekAsync(int matchweek, string competitionName = "Liga Florilor")
    {
        // One-time repair for any old 0-0 or incorrect progression data
        await SyncTournamentFixturesWithRecordsAsync();

        // Ensure fixtures exist for the current season
        await _leagueService.GenerateSeasonFixturesAsync();

        // Process daily progression for all elapsed days since last processing
        var matchDate = LeagueService.GetMatchweekDate(matchweek, competitionName);
        await ProcessDailyProgressionAsync(matchDate);

        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        var fixtures = await _db.LeagueFixtures
            .Where(f => f.Season == season && f.Round == matchweek && !f.IsPlayed && f.CompetitionName == competitionName)
            .ToListAsync();

        var results = new List<MatchRecord>();
        foreach (var fixture in fixtures)
        {
            bool standings = !(fixture.CompetitionName == LeagueService.KvindeligaenCompetition && fixture.Phase == "KvBo3");
            var result = await SimulateMatchInternalAsync(fixture.HomeTeamId, fixture.AwayTeamId, matchweek,
                updateLeagueStandings: standings, forceWinner: !standings);
            results.Add(result);
            fixture.IsPlayed = true;
            fixture.MatchRecord = result;
        }

        await _db.SaveChangesAsync();
        await _leagueService.AfterKvindeligaenLeagueDayAsync(LeagueService.GetMatchweekDate(matchweek, competitionName));
        return results;
    }

    public async Task SimulateAllLeaguesForDateAsync(DateTime date)
    {
        // Process daily progression for this date first
        await ProcessDailyProgressionAsync(date);

        var competitions = await _db.Teams.Select(t => t.CompetitionName).Distinct().ToListAsync();
        foreach (var comp in competitions)
        {
            int mw = LeagueService.GetMatchweekForDate(date, comp);
            if (mw > 0)
            {
                string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
                var fixtures = await _db.LeagueFixtures
                    .Where(f => f.Season == season && f.Round == mw && !f.IsPlayed && f.CompetitionName == comp)
                    .ToListAsync();

                foreach (var fixture in fixtures)
                {
                    bool standings = !(fixture.CompetitionName == LeagueService.KvindeligaenCompetition && fixture.Phase == "KvBo3");
                    var result = await SimulateMatchInternalAsync(fixture.HomeTeamId, fixture.AwayTeamId, mw,
                        updateLeagueStandings: standings, forceWinner: !standings);
                    fixture.IsPlayed = true;
                    fixture.MatchRecord = result;
                }
            }
        }
        await _db.SaveChangesAsync();
        await _leagueService.AfterKvindeligaenLeagueDayAsync(date);
    }

    /// <summary>
    /// Records the result of a Live Match that was manually played and then simulates the rest of that day's fixtures.
    /// </summary>
    public async Task<int> RecordLiveMatchAndSimulateRestAsync(LiveMatchEngine engine)
    {
        var playDate = _lastDailyProgressionDate.Date;
        
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        int matchweekNumber = 0;
        string roundLabel = "";
        bool isCup = false;
        bool isSupercup = false;
        bool isNeutral = false;
        
        var leagueFix = await _db.LeagueFixtures.FirstOrDefaultAsync(f => f.HomeTeamId == engine.HomeTeam.Id && f.AwayTeamId == engine.AwayTeam.Id && !f.IsPlayed && f.Season == season);
        var cupFix = await _cupService.GetFixtureForTeamOnDateAsync(engine.HomeTeam.Id, playDate);
        var supercupFix = await _supercupService.GetFixtureForTeamOnDateAsync(engine.HomeTeam.Id, playDate);

        if (cupFix != null && cupFix.HomeTeamId == engine.HomeTeam.Id && cupFix.AwayTeamId == engine.AwayTeam.Id) 
        {
            isCup = true;
            roundLabel = cupFix.Round;
            isNeutral = cupFix.VenueName != null;
        }

        if (supercupFix != null && supercupFix.HomeTeamId == engine.HomeTeam.Id && supercupFix.AwayTeamId == engine.AwayTeam.Id)
        {
            isSupercup = true;
            roundLabel = "Supercup";
            isNeutral = true;
        }

        if (leagueFix != null && !isCup && !isSupercup)
        {
            matchweekNumber = leagueFix.Round;
        }

        var record = new MatchRecord
        {
            HomeTeamId = engine.HomeTeam.Id,
            AwayTeamId = engine.AwayTeam.Id,
            HomeTeamName = engine.HomeTeam.Name,
            AwayTeamName = engine.AwayTeam.Name,
            HomeTeamLogo = engine.HomeTeam.LogoPath,
            AwayTeamLogo = engine.AwayTeam.LogoPath,
            MatchweekNumber = matchweekNumber,
            PlayedOn = playDate,
            HomeGoals = engine.HomeScore,
            AwayGoals = engine.AwayScore,
            HomePenaltyGoals = engine.HomeShootoutScore,
            AwayPenaltyGoals = engine.AwayShootoutScore,
            WasDecidedByShootout = engine.HomeShootoutScore > 0 || engine.AwayShootoutScore > 0,
            IsCupMatch = isCup || isSupercup,
            CupRound = roundLabel,
            VenueName = engine.VenueName ?? "",
            Attendance = (int)(engine.HomeTeam.StadiumCapacity * 0.8) // Simplified attendance for live match
        };

        // Transfer events
        foreach (var ev in engine.EventLog)
        {
            record.MatchEvents.Add(new MatchEvent
            {
                MatchRecord = record,
                Minute = ev.Minute,
                Second = ev.Second,
                TeamId = ev.TeamId,
                PlayerId = ev.PlayerId ?? 0,
                PlayerName = ev.PlayerName ?? "Unknown",
                EventType = ev.EventType,
                Description = ev.Description
            });
        }
        
        foreach(var kv in engine.Stats)
        {
            var pId = kv.Key;
            var st = kv.Value;
            var tId = engine.HomeTeam.Players.Any(p => p.Id == pId) ? engine.HomeTeam.Id : engine.AwayTeam.Id;
            
            bool isKeeper = st.Saves > 0 || st.GoalsAgainst > 0;
            double finalRating = isKeeper
                ? MatchRating.ForGoalkeeper(st.Saves, st.GoalsAgainst)
                : MatchRating.ForOutfield(st.Goals, st.Assists, st.Shots);

            record.PlayerStats.Add(new MatchPlayerStat
            {
                PlayerId = pId,
                PlayerName = st.PlayerName,
                TeamId = tId,
                Goals = st.Goals,
                Assists = st.Assists,
                Saves = st.Saves,
                Shots = st.Shots,
                GoalsAgainst = st.GoalsAgainst,
                Rating = finalRating
            });
        }
        
        _db.MatchRecords.Add(record);

        if (leagueFix != null && !isCup && !isSupercup)
        {
            leagueFix.IsPlayed = true;
            leagueFix.MatchRecord = record;
            bool dkBo3 = leagueFix.CompetitionName == LeagueService.KvindeligaenCompetition && leagueFix.Phase == "KvBo3";
            if (!dkBo3)
            {
                await UpdateLeagueEntryAsync(engine.HomeTeam.Id, engine.HomeScore, engine.AwayScore);
                await UpdateLeagueEntryAsync(engine.AwayTeam.Id, engine.AwayScore, engine.HomeScore);
            }
        }
        else if (isCup && cupFix != null)
        {
            cupFix.IsPlayed = true;
            cupFix.MatchRecord = record;
            cupFix.HomeGoals = engine.HomeScore;
            cupFix.AwayGoals = engine.AwayScore;
            cupFix.HomePenaltyGoals = engine.HomeShootoutScore;
            cupFix.AwayPenaltyGoals = engine.AwayShootoutScore;
            
            if (cupFix.Round == "Group" && cupFix.CupGroupId.HasValue)
            {
                await _cupService.UpdateGroupEntryAsync(cupFix.CupGroupId.Value, engine.HomeTeam.Id, engine.HomeScore, engine.AwayScore);
                await _cupService.UpdateGroupEntryAsync(cupFix.CupGroupId.Value, engine.AwayTeam.Id, engine.AwayScore, engine.HomeScore);
            }
        }
        else if (isSupercup && supercupFix != null)
        {
            supercupFix.IsPlayed = true;
            supercupFix.MatchRecord = record;
            supercupFix.HomeGoals = engine.HomeScore;
            supercupFix.AwayGoals = engine.AwayScore;
            supercupFix.HomePenaltyGoals = engine.HomeShootoutScore;
            supercupFix.AwayPenaltyGoals = engine.AwayShootoutScore;
        }

        // Update player seasonal stats for the live match
        foreach (var p in engine.HomeTeam.Players.Concat(engine.AwayTeam.Players))
        {
            if (engine.Stats.TryGetValue(p.Id, out var liveStat))
            {
                p.SeasonGoals += liveStat.Goals;
                p.SeasonAssists += liveStat.Assists;
                p.SeasonSaves += liveStat.Saves;
                p.MatchesPlayed++;
                
                p.SeasonRatingSum += p.Position == "GK"
                    ? MatchRating.ForGoalkeeper(liveStat.Saves, liveStat.GoalsAgainst)
                    : MatchRating.ForOutfield(liveStat.Goals, liveStat.Assists, liveStat.Shots);
            }
        }

        await _db.SaveChangesAsync();

        // Update Manager Stats
        UpdateManagerStats(engine.HomeTeam.Manager, engine.HomeScore, engine.AwayScore);
        UpdateManagerStats(engine.AwayTeam.Manager, engine.AwayScore, engine.HomeScore);
        
        await _db.SaveChangesAsync();

        // Simulate rest
        if (leagueFix != null && !isCup && !isSupercup)
        {
            await SimulateAllLeaguesForDateAsync(playDate);
        }
        else if (isCup)
        {
            await SimulateCupFixturesAsync(playDate);
        }
        else if (isSupercup)
        {
            await SimulateSupercupFixturesAsync(playDate);
        }

        return record.Id;
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
        await _youthIntakeService.RemoveStaleIntakeAsync(currentDate);
        await _transferService.TryGenerateAiOfferAsync(currentDate);
        await _transferService.TryGenerateAiToAiTransfersAsync(currentDate);
        await _facilityService.ProcessFacilityCompletionsAsync(currentDate);

        var allPlayers = await _db.Players.Include(p => p.Team).ThenInclude(t => t!.Manager).ToListAsync();
        foreach (var player in allPlayers)
        {
            double youthFactor = 1.0;
            if (player.Age <= 20 && player.Team?.Manager != null)
            {
                // Range: 0.7 (attribute 0) to 1.3 (attribute 20)
                youthFactor = 0.7 + (player.Team.Manager.YouthDevelopment / 20.0) * 0.6;
            }

            // Apply training facility multiplier
            if (player.Team != null)
            {
                int trainingLevel = Math.Clamp(player.Team.TrainingFacilityLevel, 0, Models.FacilityLevel.MaxLevel);
                youthFactor *= Models.FacilityLevel.TrainingMultipliers[trainingLevel];
            }

            _progression.ProcessDailyProgression(player, daysSinceLast, youthFactor);
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

    /// <summary>
    /// Simulates all cup fixtures for a given date.
    /// </summary>
    public async Task<List<MatchRecord>> SimulateCupFixturesAsync(DateTime date)
    {
        await ProcessDailyProgressionAsync(date);

        var fixtures = await _cupService.GetFixturesForDateAsync(date);
        var results = new List<MatchRecord>();

        foreach (var fixture in fixtures.Where(f => !f.IsPlayed))
        {
            var cupRoundLabel = fixture.Round switch
            {
                "Group" => $"Group {fixture.CupGroup?.GroupName}",
                "QuarterFinal" => "Quarter-Final",
                "SemiFinal" => "Semi-Final",
                "ThirdPlace" => "3rd Place",
                "Final" => "Final",
                _ => fixture.Round
            };

            var record = await SimulateMatchInternalAsync(
                fixture.HomeTeamId, fixture.AwayTeamId, 0,
                isCupMatch: true, cupRound: cupRoundLabel,
                playedOnOverride: date, isNeutralVenue: fixture.VenueName != null,
                venueName: fixture.VenueName);

            fixture.HomeGoals = record.HomeGoals;
            fixture.AwayGoals = record.AwayGoals;
            fixture.HomePenaltyGoals = record.HomePenaltyGoals;
            fixture.AwayPenaltyGoals = record.AwayPenaltyGoals;
            fixture.IsPlayed = true;
            fixture.MatchRecord = record;

            // Update group standings if this is a group match
            if (fixture.Round == "Group" && fixture.CupGroupId.HasValue)
            {
                await _cupService.UpdateGroupEntryAsync(fixture.CupGroupId.Value, fixture.HomeTeamId, record.HomeGoals, record.AwayGoals);
                await _cupService.UpdateGroupEntryAsync(fixture.CupGroupId.Value, fixture.AwayTeamId, record.AwayGoals, record.HomeGoals);
            }

            results.Add(record);
        }

        await _db.SaveChangesAsync();

        // Try to advance the bracket after simulating
        await _cupService.TryGenerateQuarterFinalsAsync();
        await _cupService.TryGenerateFinalFourAsync();
        await _cupService.TryGenerateFrenchSemiFinalsAsync();
        await _cupService.TryGenerateFinalsAsync();

        return results;
    }

    /// <summary>
    /// Simulates all supercup fixtures for a given date.
    /// </summary>
    public async Task<List<MatchRecord>> SimulateSupercupFixturesAsync(DateTime date)
    {
        await ProcessDailyProgressionAsync(date);

        var fixtures = await _supercupService.GetFixturesForDateAsync(date);
        var results = new List<MatchRecord>();

        foreach (var fixture in fixtures.Where(f => !f.IsPlayed))
        {
            var cupRoundLabel = fixture.Round switch
            {
                "SemiFinal" => "Supercup Semi-Final",
                "ThirdPlace" => "Supercup 3rd Place",
                "Final" => "Supercup Final",
                _ => fixture.Round
            };

            var record = await SimulateMatchInternalAsync(
                fixture.HomeTeamId, fixture.AwayTeamId, 0,
                isCupMatch: true, cupRound: cupRoundLabel,
                playedOnOverride: date, isNeutralVenue: true,
                venueName: fixture.VenueName);

            fixture.HomeGoals = record.HomeGoals;
            fixture.AwayGoals = record.AwayGoals;
            fixture.IsPlayed = true;
            fixture.MatchRecord = record;

            results.Add(record);
            
            if (fixture.Round == "Final")
            {
                var winner = fixture.HomeGoals > fixture.AwayGoals ? fixture.HomeTeam : fixture.AwayTeam;
                if (winner != null)
                {
                    _db.SupercupWinnerRecords.Add(new SupercupWinnerRecord
                    {
                        Season = fixture.Season,
                        TeamName = winner.Name,
                        TeamId = winner.Id,
                        CompetitionName = fixture.CompetitionName
                    });
                }
            }
        }

        await _db.SaveChangesAsync();

        await _supercupService.TryGenerateFinalsAsync();

        return results;
    }

    private async Task<MatchRecord> SimulateMatchInternalAsync(
        int homeTeamId, int awayTeamId, int matchweek,
        bool isCupMatch = false, string? cupRound = null,
        DateTime? playedOnOverride = null, bool isNeutralVenue = false,
        string? venueName = null, bool updateLeagueStandings = true,
        bool forceWinner = false)
    {
        var homeTeam = await _db.Teams.Include(t => t.Players).Include(t => t.Manager).FirstAsync(t => t.Id == homeTeamId);
        var awayTeam = await _db.Teams.Include(t => t.Players).Include(t => t.Manager).FirstAsync(t => t.Id == awayTeamId);

        // Filter: Only use top 16 players for statistical accuracy
        homeTeam.Players = homeTeam.Players.Where(p => !p.IsInjured).OrderByDescending(p => p.Overall100).Take(16).ToList();
        awayTeam.Players = awayTeam.Players.Where(p => !p.IsInjured).OrderByDescending(p => p.Overall100).Take(16).ToList();

        var record = new MatchRecord
        {
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeTeamName = homeTeam.Name,
            AwayTeamName = awayTeam.Name,
            HomeTeamLogo = homeTeam.LogoPath,
            AwayTeamLogo = awayTeam.LogoPath,
            PlayedOn = playedOnOverride ?? LeagueService.GetMatchweekDate(matchweek, homeTeam.CompetitionName),
            MatchweekNumber = matchweek,
            IsCupMatch = isCupMatch,
            CupRound = cupRound,
            VenueName = venueName ?? homeTeam.StadiumName
        };

        var playerStats = new Dictionary<int, MatchPlayerStat>();
        // Only initialize stats for the starting 7 (top 7 by position/overall)
        // Others will be added lazily if they record a stat (simulating being subbed in)
        foreach (var p in homeTeam.Players.Take(7).Concat(awayTeam.Players.Take(7)))
        {
            playerStats[p.Id] = new MatchPlayerStat
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                TeamId = p.TeamId ?? 0,
                MatchRecord = record
            };
        }

        // Calculate Form for both teams
        double homeForm = await GetTeamFormAsync(homeTeamId, record.PlayedOn);
        double awayForm = await GetTeamFormAsync(awayTeamId, record.PlayedOn);

        // Find capacity for venue (either home team's or neutral venue's)
        int effectiveCapacity = homeTeam.StadiumCapacity;
        if (isNeutralVenue && venueName != null)
        {
            var venueTeam = await _db.Teams.FirstOrDefaultAsync(t => venueName.Contains(t.StadiumName));
            if (venueTeam != null) effectiveCapacity = venueTeam.StadiumCapacity;
        }

        // Calculate Attendance
        record.Attendance = CalculateAttendance(homeTeam.ClubReputation, homeForm, awayForm, effectiveCapacity, isNeutralVenue);

        // Simulating both halves with dynamic home advantage
        double homeAdvantage = CalculateHomeAdvantage(homeTeam, record.Attendance, effectiveCapacity, isNeutralVenue);
        
        // Apply Final4 Host crowd advantage for Cup/Supercup matches
        if (isNeutralVenue && venueName != null)
        {
            if (venueName == homeTeam.StadiumName) homeAdvantage += 0.05;
            if (venueName == awayTeam.StadiumName) homeAdvantage -= 0.05;
        }

        // First half
        SimulatePossessions(record, homeTeam, awayTeam, playerStats, true, homeAdvantage);
        // Second half
        SimulatePossessions(record, awayTeam, homeTeam, playerStats, false, homeAdvantage);



        // Update Goals for record
        record.HomeGoals = record.MatchEvents.Count(e => e.TeamId == homeTeamId && e.EventType == "Goal");
        record.AwayGoals = record.MatchEvents.Count(e => e.TeamId == awayTeamId && e.EventType == "Goal");

        // Cup knockout matches or forced-winner league matches (playoffs) can't end in a draw
        bool canDraw = !forceWinner;
        if (isCupMatch && (cupRound == "Group" || cupRound?.StartsWith("Group") == true)) canDraw = true;

        if (!canDraw && record.HomeGoals == record.AwayGoals)
        {
            // --- Phase 1: Overtime (2 x 5 minutes) ---
            record.WasDecidedByOvertime = true;
            // Overtime is roughly 1/6th of a full match (60 min vs 10 min)
            int otPossessions = 10; 
            
            SimulatePossessions(record, homeTeam, awayTeam, playerStats, true, homeAdvantage, possessionCount: otPossessions);
            SimulatePossessions(record, awayTeam, homeTeam, playerStats, false, homeAdvantage, possessionCount: otPossessions);
            
            record.HomeGoals = record.MatchEvents.Count(e => e.TeamId == homeTeamId && e.EventType == "Goal");
            record.AwayGoals = record.MatchEvents.Count(e => e.TeamId == awayTeamId && e.EventType == "Goal");

            // --- Phase 2: Penalty Shootout (if still a draw) ---
            if (record.HomeGoals == record.AwayGoals)
            {
                record.WasDecidedByShootout = true;
                SimulateShootout(record, homeTeam, awayTeam);
            }
        }

        // Calculate final ratings for all players before saving
        foreach (var ps in playerStats.Values)
        {
            // Identify player position for contextual rating
            var player = homeTeam.Players.Concat(awayTeam.Players).FirstOrDefault(p => p.Id == ps.PlayerId);

            ps.Rating = player?.Position == "GK"
                ? MatchRating.ForGoalkeeper(ps.Saves, ps.GoalsAgainst)
                : MatchRating.ForOutfield(ps.Goals, ps.Assists, ps.Shots);
        }

        _db.MatchRecords.Add(record);
        // Include players in summary if they recorded any stat OR played significant time (Rating is now always >= 3.0)
        // GoalsAgainst counts too: a keeper who was beaten every time still faced shots,
        // and dropping that row would flatter the save percentage.
        record.PlayerStats.AddRange(playerStats.Values.Where(ps => ps.Goals > 0 || ps.Assists > 0 || ps.Saves > 0 || ps.Shots > 0 || ps.GoalsAgainst > 0));

        // Only update league standings for league matches
        if (!isCupMatch && updateLeagueStandings)
        {
            await UpdateLeagueEntryAsync(homeTeamId, record.HomeGoals, record.AwayGoals);
            await UpdateLeagueEntryAsync(awayTeamId, record.AwayGoals, record.HomeGoals);
        }

        // Update Manager Stats
        UpdateManagerStats(homeTeam.Manager, record.HomeGoals, record.AwayGoals);
        UpdateManagerStats(awayTeam.Manager, record.AwayGoals, record.HomeGoals);

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

                _progression.ProcessMatchProgression(player, stat, matchweek);
            }
        }

        return record;
    }

    private void SimulateShootout(MatchRecord record, Team homeTeam, Team awayTeam)
    {
        // Pick 5 shooters per team, prioritizing SevenMeterTaking then Finishing
        var homeShooters = homeTeam.Players.Where(p => p.Position != "GK")
            .OrderByDescending(p => p.SevenMeterTaking)
            .ThenByDescending(p => p.Finishing)
            .Take(5).ToList();
        
        var awayShooters = awayTeam.Players.Where(p => p.Position != "GK")
            .OrderByDescending(p => p.SevenMeterTaking)
            .ThenByDescending(p => p.Finishing)
            .Take(5).ToList();

        // Fallback if a side has no outfield players (avoids modulo-by-zero below).
        if (homeShooters.Count == 0) homeShooters = homeTeam.Players.Take(5).ToList();
        if (awayShooters.Count == 0) awayShooters = awayTeam.Players.Take(5).ToList();
        if (homeShooters.Count == 0 || awayShooters.Count == 0) return;

        // Best GK for each team
        var homeGK = homeTeam.Players.FirstOrDefault(p => p.Position == "GK") ?? homeTeam.Players.OrderByDescending(p => p.Reflexes).First();
        var awayGK = awayTeam.Players.FirstOrDefault(p => p.Position == "GK") ?? awayTeam.Players.OrderByDescending(p => p.Reflexes).First();

        int homeScore = 0;
        int awayScore = 0;

        // Standard 5 rounds
        for (int i = 0; i < 5; i++)
        {
            // Home shooter vs Away GK
            if (SimulateSevenMeter(homeShooters[i % homeShooters.Count], awayGK)) homeScore++;
            // Away shooter vs Home GK
            if (SimulateSevenMeter(awayShooters[i % awayShooters.Count], homeGK)) awayScore++;
        }

        // Sudden death if still draw
        int roundCount = 5;
        while (homeScore == awayScore && roundCount < 20)
        {
            if (SimulateSevenMeter(homeShooters[roundCount % homeShooters.Count], awayGK)) homeScore++;
            if (SimulateSevenMeter(awayShooters[roundCount % awayShooters.Count], homeGK)) awayScore++;
            roundCount++;
        }

        record.HomePenaltyGoals = homeScore;
        record.AwayPenaltyGoals = awayScore;

        // Log an event for the shootout results
        record.MatchEvents.Add(new MatchEvent
        {
            MatchRecord = record,
            EventType = "Shootout",
            PlayerName = "Penalty Shootout",
            Minute = 70 // Indicative after OT
        });
    }

    private bool SimulateSevenMeter(Player shooter, Player gk)
    {
        // 7-meter taking (0-20) vs Reflexes (0-20)
        double baseChance = 0.75; // Average 7m conversion is high in handball
        double diff = (shooter.SevenMeterTaking - gk.Reflexes) / 20.0;
        double goalChance = Math.Clamp(baseChance + (diff * 0.2), 0.5, 0.95);
        return _rng.NextDouble() < goalChance;
    }

    private void SimulatePossessions(MatchRecord record, Team attackers, Team defenders, Dictionary<int, MatchPlayerStat> stats, bool isAttackerHome, double homeAdvantage, int possessionCount = PossessionsPerTeam)
    {
        var minutesShare = PlayerActionWeights.MinutesShareByPlayer(attackers);
        var attackRating = CalculateAttackRating(attackers);
        var defenseRating = CalculateDefenseRating(defenders);
        var gk = defenders.Players.FirstOrDefault(p => p.Position == "GK") ?? defenders.Players.First();
        var gkRating = (gk.Reflexes * 2.5 + gk.OneOnOnes * 2.0 + gk.Handling + gk.Positioning * 1.5 + gk.Anticipation) / 8.0;

        double currentBonus = (isAttackerHome) ? homeAdvantage : 1.0;

        // Mental attributes. Neutral at league-average mentals, so team scoring is
        // unchanged for a typical side and only shifts for notably strong/weak squads.
        var attackOutfield = attackers.Players.Where(p => p.Position != "GK").ToList();
        double mentalFactor = TeamIntangibles.PossessionSecurity(attackOutfield)
                            * TeamIntangibles.ChanceCreation(attackOutfield)
                            * TeamIntangibles.Drive(attackOutfield);

        for (int i = 0; i < possessionCount; i++)
        {
            double diff = (attackRating * currentBonus) - defenseRating;
            // Reduced shot chance from 0.73 to 0.69 to avoid excessive goals (35+)
            double shotChance = Math.Clamp(Math.Clamp(0.69 + (diff / 35.0), 0.35, 0.95) * mentalFactor, 0.30, 0.96);

            if (_rng.NextDouble() > shotChance) continue;

            // Pick a shooter
            var shooter = PickPlayerForAction(attackers, "Goal", minutesShare);
            if (shooter == null) continue;

            // Goal Chance (Reduced base from 0.82 to 0.77 for realistic handball scores)
            double goalChance = Math.Clamp(0.77 + (shooter.Finishing - gkRating) / 30.0, 0.40, 0.96);

            // Composure shows up in the closing stretch. There is no running score
            // to read here, so "pressure" is simply the end of the match.
            bool clutch = i >= possessionCount - 6;
            goalChance = Math.Clamp(goalChance + TeamIntangibles.ClutchShift(shooter, clutch), 0.35, 0.97);

            bool brilliance = _rng.NextDouble() < TeamIntangibles.MomentOfBrillianceChance(shooter);

            if (brilliance || _rng.NextDouble() < goalChance)
            {
                // GOAL
                if (!stats.ContainsKey(shooter.Id))
                    stats[shooter.Id] = new MatchPlayerStat { PlayerId = shooter.Id, PlayerName = shooter.Name, TeamId = attackers.Id };
                
                stats[shooter.Id].Goals++;
                stats[shooter.Id].Shots++;

                // Track goal against for GK
                if (!stats.ContainsKey(gk.Id))
                    stats[gk.Id] = new MatchPlayerStat { PlayerId = gk.Id, PlayerName = gk.Name, TeamId = defenders.Id };
                stats[gk.Id].GoalsAgainst++;

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
                if (_rng.NextDouble() < PlayerActionWeights.AssistedGoalChance)
                {
                    var assistant = PickPlayerForAction(attackers, "Assist", minutesShare, shooter.Id);
                    if (assistant != null)
                    {
                        if (!stats.ContainsKey(assistant.Id))
                            stats[assistant.Id] = new MatchPlayerStat { PlayerId = assistant.Id, PlayerName = assistant.Name, TeamId = attackers.Id };
                        
                        stats[assistant.Id].Assists++;
                    }
                }
            }
            else
            {
                // SAVE
                if (!stats.ContainsKey(shooter.Id))
                    stats[shooter.Id] = new MatchPlayerStat { PlayerId = shooter.Id, PlayerName = shooter.Name, TeamId = attackers.Id };
                stats[shooter.Id].Shots++;

                if (!stats.ContainsKey(gk.Id))
                    stats[gk.Id] = new MatchPlayerStat { PlayerId = gk.Id, PlayerName = gk.Name, TeamId = defenders.Id };
                
                stats[gk.Id].Saves++;
            }
        }
    }

    /// <summary>
    /// Weighted pick over a full roster. There is no lineup at this level, so a
    /// per-position depth share stands in for minutes played — otherwise fourth-choice
    /// reserves accumulate goals at the same rate as the starters.
    /// </summary>
    private Player? PickPlayerForAction(Team team, string action, int excludePlayerId = -1)
        => PickPlayerForAction(team, action, PlayerActionWeights.MinutesShareByPlayer(team), excludePlayerId);

    private Player? PickPlayerForAction(Team team, string action, Dictionary<int, double> minutes, int excludePlayerId = -1)
    {
        Func<Player, double> baseWeight = action == "Goal"
            ? PlayerActionWeights.ShotWeight
            : PlayerActionWeights.AssistWeight;

        return PlayerActionWeights.Pick(
            team.Players,
            p => baseWeight(p) * minutes.GetValueOrDefault(p.Id, 0.12),
            _rng,
            excludePlayerId);
    }

    private double CalculateAttackRating(Team team)
    {
        var players = team.Players.Where(p => p.Position != "GK").ToList();
        if (players.Count == 0) return 10;
        double raw = players.Average(p => (p.Finishing + p.Passing + p.Technique + p.Decisions + p.Pace + p.Acceleration) / 6.0);
        return raw * TeamIntangibles.Cohesion(players);
    }

    private double CalculateDefenseRating(Team team)
    {
        var players = team.Players.Where(p => p.Position != "GK").ToList();
        if (players.Count == 0) return 10;
        double raw = players.Average(p => (p.Marking + p.Tackling + p.Positioning + p.Strength + p.Aggression + p.Anticipation) / 6.0);
        return raw * TeamIntangibles.Cohesion(players);
    }

    /// <summary>
    /// Simulates a single Kvindeligaen playoff fixture (used by the Bo3 completion loop).
    /// Does not rely on matchweek-date calendar lookup, since these fixtures are generated dynamically.
    /// </summary>
    public async Task<MatchRecord> SimulateKvindeligaenPlayoffFixtureAsync(
        int homeTeamId, int awayTeamId, int round, bool updateLeagueStandings)
    {
        return await SimulateMatchInternalAsync(homeTeamId, awayTeamId, round,
            updateLeagueStandings: updateLeagueStandings, forceWinner: true);
    }

    /// <summary>
    /// Drops every tracked instance of a type. Used before an ExecuteDelete, which goes
    /// straight to the database — anything still tracked would be written back on the
    /// next save as an update to a row that no longer exists.
    /// </summary>
    private void DetachTracked<T>() where T : class
    {
        foreach (var entry in _db.ChangeTracker.Entries<T>().ToList())
            entry.State = EntityState.Detached;
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
        // Must run first: this reads the season's match stats, which get wiped below,
        // and the player rows, which lose their seasonal totals to the progression reset.
        await _awards.CaptureTeamsOfTheSeasonAsync($"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}");

        // A season of play leaves tens of thousands of match rows in the change tracker,
        // and every SaveChanges below would scan all of them. They are about to be
        // deleted wholesale, so let them go now.
        DetachTracked<MatchPlayerStat>();
        DetachTracked<MatchEvent>();
        DetachTracked<MatchRecord>();

        var toRetire = await _db.Players.IgnoreQueryFilters().Where(p => p.IsRetiringAtEndOfSeason).ToListAsync();
        foreach (var p in toRetire)
            p.IsRetired = true;

        var allPlayers = await _db.Players.ToListAsync();
        _progression.ProcessEndOfSeason(allPlayers);

        var allEntries = await _db.LeagueEntries
            .Include(e => e.Team)
            .ToListAsync();

        // Determine Champions for each competition before wiping entries
        var competitions = allEntries.Select(e => e.CompetitionName).Distinct().ToList();
        string seasonStrChamp = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";
        foreach (var comp in competitions)
        {
            if (comp == LeagueService.KvindeligaenCompetition)
            {
                var dkChamp = await _leagueService.GetKvindeligaenPlayoffChampionTeamAsync(seasonStrChamp);
                if (dkChamp != null)
                {
                    _db.ChampionRecords.Add(new ChampionRecord
                    {
                        Season = seasonStrChamp,
                        TeamName = dkChamp.Name,
                        TeamId = dkChamp.Id,
                        CompetitionName = comp
                    });
                }
                continue;
            }

            var winnerEntry = allEntries
                .Where(e => e.CompetitionName == comp)
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.GoalsFor)
                .FirstOrDefault();

            if (winnerEntry?.Team != null)
            {
                var champRecord = new ChampionRecord
                {
                    Season = seasonStrChamp,
                    TeamName = winnerEntry.Team.Name,
                    TeamId = winnerEntry.Team.Id,
                    CompetitionName = comp
                };
                _db.ChampionRecords.Add(champRecord);
            }
        }

        // --- Romanian Cup & Supercup Processing ---
        var sortedRomanianTeams = allEntries
            .Where(e => e.CompetitionName == "Liga Florilor" && e.Team != null)
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .Select(e => e.Team!)
            .ToList();

        string currentSeasonStr = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        // Record Romanian cup winner
        var roCupFinal = await _db.CupFixtures
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.Round == "Final" && f.IsPlayed && f.Season == currentSeasonStr && f.CompetitionName == "Liga Florilor");

        var cupFinalists = new List<Team>();
        if (roCupFinal != null)
        {
            var cupWinner = roCupFinal.HomeGoals > roCupFinal.AwayGoals ? roCupFinal.HomeTeam
                : (roCupFinal.HomeGoals == roCupFinal.AwayGoals && roCupFinal.HomePenaltyGoals > roCupFinal.AwayPenaltyGoals) ? roCupFinal.HomeTeam : roCupFinal.AwayTeam;
            if (cupWinner != null)
            {
                if (!await _db.CupWinnerRecords.AnyAsync(r => r.Season == seasonStrChamp && r.CompetitionName == "Liga Florilor"))
                    _db.CupWinnerRecords.Add(new CupWinnerRecord { Season = seasonStrChamp, TeamName = cupWinner.Name, TeamId = cupWinner.Id, CompetitionName = "Liga Florilor" });
            }
            if (roCupFinal.HomeTeam != null) cupFinalists.Add(roCupFinal.HomeTeam);
            if (roCupFinal.AwayTeam != null) cupFinalists.Add(roCupFinal.AwayTeam);
        }

        // Record Hungarian cup winner
        var huCupFinal = await _db.CupFixtures
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.Round == "Final" && f.IsPlayed && f.Season == currentSeasonStr && f.CompetitionName == "NB I");
        if (huCupFinal != null)
        {
            var huWinner = huCupFinal.HomeGoals > huCupFinal.AwayGoals ? huCupFinal.HomeTeam
                : (huCupFinal.HomeGoals == huCupFinal.AwayGoals && huCupFinal.HomePenaltyGoals > huCupFinal.AwayPenaltyGoals) ? huCupFinal.HomeTeam : huCupFinal.AwayTeam;
            if (huWinner != null)
            {
                if (!await _db.CupWinnerRecords.AnyAsync(r => r.Season == seasonStrChamp && r.CompetitionName == "NB I"))
                    _db.CupWinnerRecords.Add(new CupWinnerRecord { Season = seasonStrChamp, TeamName = huWinner.Name, TeamId = huWinner.Id, CompetitionName = "NB I" });
            }
        }

        // Record French cup winner
        var frCupFinal = await _db.CupFixtures
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.Round == "Final" && f.IsPlayed && f.Season == currentSeasonStr && f.CompetitionName == "Ligue Butagaz Énergie");
        if (frCupFinal != null)
        {
            var frWinner = frCupFinal.HomeGoals > frCupFinal.AwayGoals ? frCupFinal.HomeTeam
                : (frCupFinal.HomeGoals == frCupFinal.AwayGoals && frCupFinal.HomePenaltyGoals > frCupFinal.AwayPenaltyGoals) ? frCupFinal.HomeTeam : frCupFinal.AwayTeam;
            if (frWinner != null)
            {
                if (!await _db.CupWinnerRecords.AnyAsync(r => r.Season == seasonStrChamp && r.CompetitionName == "Ligue Butagaz Énergie"))
                    _db.CupWinnerRecords.Add(new CupWinnerRecord { Season = seasonStrChamp, TeamName = frWinner.Name, TeamId = frWinner.Id, CompetitionName = "Ligue Butagaz Énergie" });
            }
        }

        var dkCupFinal = await _db.CupFixtures
            .Include(f => f.HomeTeam).Include(f => f.AwayTeam)
            .FirstOrDefaultAsync(f => f.Round == "Final" && f.IsPlayed && f.Season == currentSeasonStr && f.CompetitionName == LeagueService.KvindeligaenCompetition);
        if (dkCupFinal != null)
        {
            var dkCw = dkCupFinal.HomeGoals > dkCupFinal.AwayGoals ? dkCupFinal.HomeTeam
                : (dkCupFinal.HomeGoals == dkCupFinal.AwayGoals && dkCupFinal.HomePenaltyGoals > dkCupFinal.AwayPenaltyGoals) ? dkCupFinal.HomeTeam : dkCupFinal.AwayTeam;
            if (dkCw != null)
            {
                if (!await _db.CupWinnerRecords.AnyAsync(r => r.Season == seasonStrChamp && r.CompetitionName == LeagueService.KvindeligaenCompetition))
                    _db.CupWinnerRecords.Add(new CupWinnerRecord { Season = seasonStrChamp, TeamName = dkCw.Name, TeamId = dkCw.Id, CompetitionName = LeagueService.KvindeligaenCompetition });
            }
        }

        await DistributePrizeMoneyAsync(currentDate, seasonStrChamp, allEntries);

        await _supercupService.GenerateNextSupercupAsync(sortedRomanianTeams, cupFinalists);
        await _supercupService.GenerateNextDanishSupercupAsync(_leagueService);
        await _cupService.ClearSeasonDataAsync();
        // ------------------------------------------

        foreach (var entry in allEntries)
        {
            entry.Played = 0;
            entry.Won = 0;
            entry.Drawn = 0;
            entry.Lost = 0;
            entry.GoalsFor = 0;
            entry.GoalsAgainst = 0;
        }

        // Wipe the season's match data.
        //
        // Set-based on purpose. Loading it all (a world season runs to ~60k match events)
        // into the change tracker and removing it row by row took seconds on a desktop and
        // hung a phone long enough for Android to kill the app. ExecuteDelete issues one
        // statement per table and never materialises a row.
        //
        // League, cup and supercup fixtures all outlive the records they point at, and
        // SQLite does enforce those foreign keys. The old row-by-row delete got away with
        // it because EF nulled the links on tracked dependents for us; a set-based delete
        // has no such fix-up, so the links are dropped explicitly — in memory as well as
        // on disk, or a later save would write the stale id straight back.
        foreach (var tracked in _db.ChangeTracker.Entries<LeagueFixture>())
        {
            tracked.Entity.MatchRecordId = null;
            tracked.Entity.MatchRecord = null;
        }
        foreach (var tracked in _db.ChangeTracker.Entries<CupFixture>())
        {
            tracked.Entity.MatchRecordId = null;
            tracked.Entity.MatchRecord = null;
        }
        foreach (var tracked in _db.ChangeTracker.Entries<SupercupFixture>())
        {
            tracked.Entity.MatchRecordId = null;
            tracked.Entity.MatchRecord = null;
        }

        await _db.SaveChangesAsync();

        await _db.LeagueFixtures.Where(f => f.MatchRecordId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.MatchRecordId, (int?)null));
        await _db.CupFixtures.Where(f => f.MatchRecordId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.MatchRecordId, (int?)null));
        await _db.SupercupFixtures.Where(f => f.MatchRecordId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.MatchRecordId, (int?)null));

        DetachTracked<MatchPlayerStat>();
        DetachTracked<MatchEvent>();
        DetachTracked<MatchRecord>();

        await _db.MatchPlayerStats.ExecuteDeleteAsync();
        await _db.MatchEvents.ExecuteDeleteAsync();
        await _db.MatchRecords.ExecuteDeleteAsync();

        // Purge retired players now that the season's match data (which referenced
        // them) has been wiped. Nothing in the UI displays retired players, so this
        // keeps the Players table from growing unbounded over a long save. Also drop
        // any dangling transfer offers/pending moves that pointed at them.
        var retiredPlayers = await _db.Players.IgnoreQueryFilters().Where(p => p.IsRetired).ToListAsync();
        if (retiredPlayers.Count > 0)
        {
            var retiredIds = retiredPlayers.Select(p => p.Id).ToList();
            var deadOffers = await _db.TransferOffers.Where(o => retiredIds.Contains(o.ForPlayerId)).ToListAsync();
            var deadPending = await _db.PendingTransfers.Where(t => retiredIds.Contains(t.PlayerId)).ToListAsync();
            _db.TransferOffers.RemoveRange(deadOffers);
            _db.PendingTransfers.RemoveRange(deadPending);
            _db.Players.RemoveRange(retiredPlayers);
        }

        // Stale youth and other cleanups
        await _youthIntakeService.RemoveStaleIntakeAsync(currentDate);

        LeagueService.AdvanceToNextSeason();
        await _leagueService.GenerateSeasonFixturesAsync();
        _lastDailyProgressionDate = currentDate.Date;
        _lastWeeklyWageDate = currentDate.Date;

        await _db.SaveChangesAsync();

        // Generate new cup for the next season
        await _cupService.GenerateCupAsync();

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

    private async Task<double> GetTeamFormAsync(int teamId, DateTime beforeDate)
    {
        var recentMatches = await _db.MatchRecords
            .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.PlayedOn < beforeDate.Date)
            .OrderByDescending(m => m.PlayedOn)
            .Take(5)
            .ToListAsync();

        if (recentMatches.Count == 0) return 0.5; // Neutral form for start of season

        double totalPoints = 0;
        foreach (var m in recentMatches)
        {
            bool isHome = m.HomeTeamId == teamId;
            int myGoals = isHome ? m.HomeGoals : m.AwayGoals;
            int oppGoals = isHome ? m.AwayGoals : m.HomeGoals;

            if (myGoals > oppGoals) totalPoints += 1.0;
            else if (myGoals == oppGoals) totalPoints += 0.5;
        }

        return totalPoints / recentMatches.Count;
    }

    private double CalculateHomeAdvantage(Team homeTeam, int attendance, int capacity, bool isNeutralVenue)
    {
        if (isNeutralVenue) return 1.0;

        double advantage = 1.02; // Base

        // Reputation bonus
        advantage += homeTeam.ClubReputation switch
        {
            ReputationLevel.Local => 0.01,
            ReputationLevel.Regional => 0.02,
            ReputationLevel.National => 0.03,
            ReputationLevel.European => 0.04,
            ReputationLevel.International => 0.045,
            ReputationLevel.Global => 0.05,
            _ => 0.01
        };

        // Manager Timeout Talks bonus
        if (homeTeam.Manager != null)
        {
            advantage += (homeTeam.Manager.TimeoutTalks / 20.0) * 0.01;
        }

        // Crowd Pressure Factor (Attendance / Capacity)
        if (capacity > 0)
        {
            double fillRate = (double)attendance / capacity;
            advantage += (fillRate * 0.04); // Up to +0.04 for full stadium
        }
        
        // Large Arena "Atmosphere" Factor
        if (capacity > 3000)
        {
            advantage += 0.01;
        }

        return Math.Min(advantage, 1.15); // Hard cap at 1.15
    }

    private int CalculateAttendance(ReputationLevel clubReputation, double homeForm, double awayForm, int capacity, bool isNeutralVenue)
    {
        if (isNeutralVenue) return (int)(capacity * 0.5); // Neutral venues get 50% base fill

        double fillRate = 0.35; // Base

        // Reputation influence
        fillRate += clubReputation switch
        {
            ReputationLevel.Local => 0.05,
            ReputationLevel.Regional => 0.15,
            ReputationLevel.National => 0.3,
            ReputationLevel.European => 0.4,
            ReputationLevel.International => 0.45,
            ReputationLevel.Global => 0.5,
            _ => 0.1
        };

        // Performance (Form) influence
        fillRate += (homeForm - 0.5) * 0.3; // Up to +/- 0.15 based on form

        // Opponent quality (awayForm)
        fillRate += (awayForm - 0.5) * 0.1;

        // Random variance
        fillRate += (_rng.NextDouble() * 0.15) - 0.05;

        int attendance = (int)(capacity * Math.Clamp(fillRate, 0.1, 1.0));
        return attendance;
    }

    private void UpdateManagerStats(Manager? manager, int myGoals, int oppGoals)
    {
        if (manager == null) return;
        if (myGoals > oppGoals) manager.GamesWon++;
        else if (myGoals == oppGoals) manager.GamesDrawn++;
        else manager.GamesLost++;
    }

    public async Task SyncTournamentFixturesWithRecordsAsync()
    {
        var cupFixtures = await _db.CupFixtures.Include(f => f.MatchRecord).Where(f => f.IsPlayed && f.MatchRecord != null).ToListAsync();
        foreach (var f in cupFixtures)
        {
            f.HomeGoals = f.MatchRecord!.HomeGoals;
            f.AwayGoals = f.MatchRecord.AwayGoals;
            f.HomePenaltyGoals = f.MatchRecord.HomePenaltyGoals;
            f.AwayPenaltyGoals = f.MatchRecord.AwayPenaltyGoals;
        }

        var supercupFixtures = await _db.SupercupFixtures.Include(f => f.MatchRecord).Where(f => f.IsPlayed && f.MatchRecord != null).ToListAsync();
        foreach (var f in supercupFixtures)
        {
            f.HomeGoals = f.MatchRecord!.HomeGoals;
            f.AwayGoals = f.MatchRecord.AwayGoals;
            f.HomePenaltyGoals = f.MatchRecord.HomePenaltyGoals;
            f.AwayPenaltyGoals = f.MatchRecord.AwayPenaltyGoals;
        }

        await _db.SaveChangesAsync();
    }

    private async Task DistributePrizeMoneyAsync(DateTime currentDate, string seasonStr, List<LeagueEntry> allEntries)
    {
        var allTeams = await _db.Teams.ToListAsync();

        // 1. League Prize Money
        var comps = allEntries.Select(e => e.CompetitionName).Distinct().ToList();
        foreach (var comp in comps)
        {
            List<Team> sortedTeams = new();
            if (comp == LeagueService.KvindeligaenCompetition)
            {
                // DK: 1st=Champ, 2nd=FinalLoser, 3rd/4th from ThirdPlace playoff, rest from standings.
                var champ = await _leagueService.GetKvindeligaenPlayoffChampionTeamAsync(seasonStr);
                var runnerUp = await _leagueService.GetKvindeligaenPlayoffFinalLoserTeamAsync(seasonStr);
                var thirdFixture = await _db.LeagueFixtures.Include(f => f.MatchRecord).Where(f => f.Season == seasonStr && f.CompetitionName == comp && f.PlayoffSeriesId == "THIRD" && f.IsPlayed && f.MatchRecord != null).OrderByDescending(f => f.PlayoffLeg).ToListAsync();
                Team? third = null, fourth = null;
                if (thirdFixture.Count > 0)
                {
                    var ids = thirdFixture.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
                    int w1 = 0, w2 = 0;
                    foreach(var f in thirdFixture)
                    {
                        var r = f.MatchRecord!;
                        bool homeWins = r.HomeGoals > r.AwayGoals || (r.HomeGoals == r.AwayGoals && r.HomePenaltyGoals > r.AwayPenaltyGoals);
                        if (homeWins && f.HomeTeamId == ids[0]) w1++;
                        else if (!homeWins && f.AwayTeamId == ids[0]) w1++;
                        else w2++;
                    }
                    if (w1 >= 2 || w2 >= 2)
                    {
                        third = allTeams.FirstOrDefault(t => t.Id == (w1 > w2 ? ids[0] : ids[1]));
                        fourth = allTeams.FirstOrDefault(t => t.Id == (w1 > w2 ? ids[1] : ids[0]));
                    }
                }
                var otherEntries = allEntries.Where(e => e.CompetitionName == comp).OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).Select(e => e.Team!).ToList();
                if (champ != null) sortedTeams.Add(champ);
                if (runnerUp != null) sortedTeams.Add(runnerUp);
                if (third != null) sortedTeams.Add(third);
                if (fourth != null) sortedTeams.Add(fourth);
                foreach(var t in otherEntries)
                    if (!sortedTeams.Any(st => st.Id == t.Id)) sortedTeams.Add(t);
            }
            else
            {
                sortedTeams = allEntries.Where(e => e.CompetitionName == comp).OrderByDescending(e => e.Points).ThenByDescending(e => e.GoalDifference).ThenByDescending(e => e.GoalsFor).Select(e => e.Team!).ToList();
            }

            for (int i = 0; i < sortedTeams.Count; i++)
            {
                int rank = i + 1;
                decimal prize = 0;
                if (comp == "Liga Florilor") prize = rank == 1 ? 150000 : rank <= 4 ? 80000 : rank <= 8 ? 40000 : 10000;
                else if (comp == "NB I") prize = rank == 1 ? 150000 : rank <= 4 ? 100000 : rank <= 8 ? 50000 : 15000;
                else if (comp == "Ligue Butagaz Énergie") prize = rank == 1 ? 100000 : rank <= 4 ? 75000 : rank <= 8 ? 40000 : 10000;
                else if (comp == LeagueService.KvindeligaenCompetition) prize = rank == 1 ? 100000 : rank <= 4 ? 85000 : rank <= 8 ? 35000 : 10000;

                if (prize > 0)
                {
                    sortedTeams[i].ClubBalance += prize;
                    sortedTeams[i].TransferBudget += prize;
                    _db.Transactions.Add(new Transaction { TeamId = sortedTeams[i].Id, Amount = prize, Date = currentDate, Description = $"League {rank}{(rank == 1 ? "st" : rank == 2 ? "nd" : rank == 3 ? "rd" : "th")} place prize money", Type = "PrizeMoney" });
                    if (sortedTeams[i].IsPlayerTeam)
                        _db.NewsItems.Add(new NewsItem { Title = "League Prize Money Received", Body = $"The club has received {prize:N0} € in prize money for finishing {rank} in the league.", PublishedAt = currentDate, NewsType = "Finance" });
                }
            }
        }

        // 2. Cup Prize Money
        var cupFinals = await _db.CupFixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Where(f => f.Season == seasonStr && (f.Round == "Final" || f.Round == "ThirdPlace") && f.IsPlayed).ToListAsync();
        foreach (var c in comps)
        {
            var final = cupFinals.FirstOrDefault(f => f.CompetitionName == c && f.Round == "Final");
            var thirdMatch = cupFinals.FirstOrDefault(f => f.CompetitionName == c && f.Round == "ThirdPlace");

            decimal firstPrize = c == "Liga Florilor" ? 50000 : c == "NB I" ? 50000 : c == "Ligue Butagaz Énergie" ? 40000 : 45000;
            decimal secondPrize = c == "Liga Florilor" ? 25000 : c == "NB I" ? 25000 : c == "Ligue Butagaz Énergie" ? 20000 : 20000;
            decimal thirdPrize = 10000;
            decimal fourthPrize = 5000;

            if (final != null && final.HomeTeam != null && final.AwayTeam != null)
            {
                bool homeWon = final.HomeGoals > final.AwayGoals || (final.HomeGoals == final.AwayGoals && final.HomePenaltyGoals > final.AwayPenaltyGoals);
                var winner = homeWon ? final.HomeTeam : final.AwayTeam;
                var loser = homeWon ? final.AwayTeam : final.HomeTeam;

                winner.ClubBalance += firstPrize;
                winner.TransferBudget += firstPrize;
                loser.ClubBalance += secondPrize;
                loser.TransferBudget += secondPrize;

                _db.Transactions.Add(new Transaction { TeamId = winner.Id, Amount = firstPrize, Date = currentDate, Description = "Cup Winner prize money", Type = "PrizeMoney" });
                _db.Transactions.Add(new Transaction { TeamId = loser.Id, Amount = secondPrize, Date = currentDate, Description = "Cup Runner-Up prize money", Type = "PrizeMoney" });
                
                if (winner.IsPlayerTeam) _db.NewsItems.Add(new NewsItem { Title = "Cup Prize Money", Body = $"Received {firstPrize:N0} € for winning the cup.", PublishedAt = currentDate, NewsType = "Finance" });
                if (loser.IsPlayerTeam) _db.NewsItems.Add(new NewsItem { Title = "Cup Prize Money", Body = $"Received {secondPrize:N0} € for reaching the cup final.", PublishedAt = currentDate, NewsType = "Finance" });
            }

            if (thirdMatch != null && thirdMatch.HomeTeam != null && thirdMatch.AwayTeam != null)
            {
                bool homeWon = thirdMatch.HomeGoals > thirdMatch.AwayGoals || (thirdMatch.HomeGoals == thirdMatch.AwayGoals && thirdMatch.HomePenaltyGoals > thirdMatch.AwayPenaltyGoals);
                var winner = homeWon ? thirdMatch.HomeTeam : thirdMatch.AwayTeam;
                var loser = homeWon ? thirdMatch.AwayTeam : thirdMatch.HomeTeam;

                winner.ClubBalance += thirdPrize;
                winner.TransferBudget += thirdPrize;
                loser.ClubBalance += fourthPrize;
                loser.TransferBudget += fourthPrize;

                _db.Transactions.Add(new Transaction { TeamId = winner.Id, Amount = thirdPrize, Date = currentDate, Description = "Cup 3rd place prize money", Type = "PrizeMoney" });
                _db.Transactions.Add(new Transaction { TeamId = loser.Id, Amount = fourthPrize, Date = currentDate, Description = "Cup 4th place prize money", Type = "PrizeMoney" });
                
                if (winner.IsPlayerTeam) _db.NewsItems.Add(new NewsItem { Title = "Cup Prize Money", Body = $"Received {thirdPrize:N0} € for 3rd place in the cup.", PublishedAt = currentDate, NewsType = "Finance" });
                if (loser.IsPlayerTeam) _db.NewsItems.Add(new NewsItem { Title = "Cup Prize Money", Body = $"Received {fourthPrize:N0} € for 4th place in the cup.", PublishedAt = currentDate, NewsType = "Finance" });
            }
        }
    }
}