using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly LeagueService _leagueService;
    private readonly SimulationEngine _simulationEngine;
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;
    private readonly HandballDbContext _db;
    private readonly Func<int, Task> _onNavigateToMatchDetail;
    private readonly Func<Task>? _onDayAdvanced;
    private readonly Action<int, int, string, string, bool>? _onNavigateToSquadSelection;
    private readonly GameClock _clock;

    [ObservableProperty]
    private DateTime _currentDate;

    [ObservableProperty]
    private string _currentDateText = string.Empty;

    [ObservableProperty]
    private bool _isMatchdayToday;

    [ObservableProperty]
    private string _nextMatchDateText = string.Empty;

    [ObservableProperty]
    private bool _canSimulateMatch;

    [ObservableProperty]
    private bool _canAdvanceDay;

    [ObservableProperty]
    private bool _canSimulateToEndOfSeason;

    [ObservableProperty]
    private string _nextMatchInfo = "Loading...";

    [ObservableProperty]
    private string _lastResultText = string.Empty;

    [ObservableProperty]
    private List<MatchRecord> _matchweekResults = [];

    [ObservableProperty]
    private int _viewedMatchweek;

    [ObservableProperty]
    private string _matchweekDateText = string.Empty;

    [ObservableProperty]
    private bool _matchSimulated;

    [ObservableProperty]
    private bool _isSeasonOver;

    [ObservableProperty]
    private string _seasonCompletedMessage = string.Empty;

    [ObservableProperty]
    private bool _canBeginNewSeason;

    [ObservableProperty]
    private bool _isCupMatchday;

    [ObservableProperty]
    private string _nextCupMatchInfo = string.Empty;

    private int _nextHomeTeamId;
    private int _nextAwayTeamId;
    private int _nextMatchweek;
    private DateTime? _nextCupDate;
    private DateTime? _nextSupercupDate;
    private bool _playerHasCupFixturePending;
    private bool _playerHasSupercupFixturePending;
    private DateTime? _nextCupDateForTeam;
    private DateTime? _nextSupercupDateForTeam;
    private string _nextVenueName = string.Empty;

    // Total "event slots" for the matchweek browser: league matchweeks + cup dates
    private List<DateTime> _allEventDates = [];
    private int _viewedEventIndex;

    public HomeViewModel(HandballDbContext db, LeagueService leagueService, SimulationEngine simulationEngine,
        CupService cupService, SupercupService supercupService, GameClock clock, Func<int, Task> onNavigateToMatchDetail, Action<int, int, string, string, bool>? onNavigateToSquadSelection = null, Func<Task>? onDayAdvanced = null)
    {
        Title = "Home";
        _db = db;
        _leagueService = leagueService;
        _simulationEngine = simulationEngine;
        _cupService = cupService;
        _supercupService = supercupService;
        _clock = clock;
        _onNavigateToMatchDetail = onNavigateToMatchDetail;
        _onNavigateToSquadSelection = onNavigateToSquadSelection;
        _onDayAdvanced = onDayAdvanced;
    }

    public async Task InitializeAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        string competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";

        if (CurrentDate == default)
            CurrentDate = _clock.CurrentDate.Date;

        await _leagueService.GenerateSeasonFixturesAsync();
        await BuildAllEventDatesAsync();
        await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
        await LoadMatchweekResultsAsync(competitionName);

        MatchSimulated = false;
        UpdateCalendarState();
    }

    /// <summary>
    /// Builds a merged, sorted list of all league + cup event dates for the results browser.
    /// </summary>
    private async Task BuildAllEventDatesAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        string comp = playerTeam?.CompetitionName ?? "Liga Florilor";

        var leagueDates = LeagueService.GetMatchweekDates(comp).ToList();
        var allDates = new HashSet<DateTime>(leagueDates);

        // Add cup and supercup dates for the relevant competition
        var cupDates = await _cupService.GetAllCupDatesAsync(comp);
        var supercupDates = await _supercupService.GetAllDatesAsync();
        
        foreach (var cd in cupDates) allDates.Add(cd.Date);
        // Only Romanian Supercup exists currently
        if (comp == "Liga Florilor")
        {
            foreach (var sd in supercupDates) allDates.Add(sd.Date);
        }

        _allEventDates = allDates.OrderBy(d => d).ToList();
    }

    private async Task LoadMatchweekResultsAsync(string competitionName = "Liga Florilor")
    {
        if (_allEventDates.Count == 0) return;

        var date = _allEventDates[_viewedEventIndex];

        // Get league results for this date (by matchweek)
        var leagueMatchweekIndex = LeagueService.GetMatchweekDates(competitionName).IndexOf(date);
        var leagueResults = new List<MatchRecord>();
        if (leagueMatchweekIndex >= 0)
        {
            var matchweek = leagueMatchweekIndex + 1;
            leagueResults = await _leagueService.GetResultsForMatchweekAsync(matchweek, competitionName);
            
            // Add unplayed league fixtures as stubs
            var unplayedLeague = await _leagueService.GetFixturesForRoundAsync(matchweek, competitionName);
            foreach (var f in unplayedLeague.Where(f => !f.IsPlayed))
            {
                leagueResults.Add(new MatchRecord
                {
                    Id = -1,
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId,
                    HomeTeamName = f.HomeTeam?.Name ?? "TBD",
                    AwayTeamName = f.AwayTeam?.Name ?? "TBD",
                    HomeTeamLogo = f.HomeTeam?.LogoPath ?? "",
                    AwayTeamLogo = f.AwayTeam?.LogoPath ?? "",
                    HomeGoals = 0,
                    AwayGoals = 0,
                    IsCupMatch = false,
                    PlayedOn = date,
                    MatchweekNumber = matchweek
                });
            }
        }

        // Get cup results for this date (filtered by competition)
        var cupResults = new List<MatchRecord>();
        
        // Load cup results for the current competition
        var playedCup = await _db.MatchRecords
            .Where(m => m.IsCupMatch && m.PlayedOn.Date == date.Date)
            .Where(m => _db.Teams.Any(t => t.Id == m.HomeTeamId && t.CompetitionName == competitionName))
            .OrderBy(m => m.Id)
            .ToListAsync();
        cupResults.AddRange(playedCup);

        // Also get unplayed cup fixtures for this date
        var unplayedCupFixtures = await _cupService.GetFixturesForDateAsync(date, competitionName);
        foreach (var f in unplayedCupFixtures.Where(f => !f.IsPlayed))
        {
            // Create a temporary stub record for display
            cupResults.Add(new MatchRecord
            {
                Id = -1, // Use -1 to indicate unplayed
                HomeTeamId = f.HomeTeamId,
                AwayTeamId = f.AwayTeamId,
                HomeTeamName = f.HomeTeam?.Name ?? "TBD",
                AwayTeamName = f.AwayTeam?.Name ?? "TBD",
                HomeTeamLogo = f.HomeTeam?.LogoPath ?? "",
                AwayTeamLogo = f.AwayTeam?.LogoPath ?? "",
                HomeGoals = 0,
                AwayGoals = 0,
                IsCupMatch = true,
                CupRound = f.Round == "Group" ? $"Group {f.CupGroup?.GroupName}" : f.Round,
                PlayedOn = f.ScheduledDate
            });
        }
        
        if (competitionName == "Liga Florilor")
        {
            var unplayedSupercupFixtures = await _supercupService.GetFixturesForDateAsync(date);
            foreach (var f in unplayedSupercupFixtures.Where(f => !f.IsPlayed))
            {
                cupResults.Add(new MatchRecord
                {
                    Id = -1,
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId,
                    HomeTeamName = f.HomeTeam?.Name ?? "TBD",
                    AwayTeamName = f.AwayTeam?.Name ?? "TBD",
                    HomeTeamLogo = f.HomeTeam?.LogoPath ?? "",
                    AwayTeamLogo = f.AwayTeam?.LogoPath ?? "",
                    HomeGoals = 0,
                    AwayGoals = 0,
                    IsCupMatch = true,
                    CupRound = f.Round == "SemiFinal" ? "Supercup Semi-Final" : (f.Round == "Final" ? "Supercup Final" : "Supercup 3rd Place"),
                    PlayedOn = f.ScheduledDate
                });
            }
        }

        MatchweekResults = leagueResults.Concat(cupResults).ToList();

        // Build header text
        bool hasSupercup = cupResults.Any(r => r.IsCupMatch && 
            (r.CupRound?.Contains("Supercup", StringComparison.OrdinalIgnoreCase) == true || 
             r.CupRound?.Contains("Szuperkupa", StringComparison.OrdinalIgnoreCase) == true ||
             r.CupRound?.Contains("Supercupa", StringComparison.OrdinalIgnoreCase) == true));
        
        bool hasCup = cupResults.Any(r => r.IsCupMatch && !hasSupercup);
        
        string compCupName = competitionName == "NB I" ? "Magyar Kupa" : "Cupa României";
        string compSupercupName = competitionName == "NB I" ? "Szuperkupa" : "Supercupa României";

        if (leagueMatchweekIndex >= 0 && (hasCup || hasSupercup))
        {
            string tournamentPart = hasSupercup ? compSupercupName : compCupName;
            MatchweekDateText = $"Matchweek {leagueMatchweekIndex + 1} + {tournamentPart} • {date:dddd, MMM d, yyyy}";
        }
        else if (leagueMatchweekIndex >= 0)
            MatchweekDateText = $"Matchweek {leagueMatchweekIndex + 1} • {date:dddd, MMM d, yyyy}";
        else if (hasSupercup)
            MatchweekDateText = $"{compSupercupName} • {date:dddd, MMM d, yyyy}";
        else if (hasCup)
            MatchweekDateText = $"{compCupName} • {date:dddd, MMM d, yyyy}";
        else
            MatchweekDateText = date.ToString("dddd, MMM d, yyyy");

        // Also set ViewedMatchweek for compatibility
        ViewedMatchweek = leagueMatchweekIndex >= 0 ? leagueMatchweekIndex + 1 : 0;
    }

    private async Task UpdateNextFixtureAsync(bool setViewedMatchweekDefault)
    {
        var playerTeam = _db.Teams.First(t => t.IsPlayerTeam);

        // Next league fixture
        var (homeId, awayId, matchweek) = await _leagueService.GetNextFixtureAsync(playerTeam.Id);

        // Next cup fixture
        _nextCupDate = await _cupService.GetNextCupDateAsync();
        _nextCupDateForTeam = await _cupService.GetNextCupDateForTeamAsync(playerTeam.Id);
        
        var cupFixtureForTeam = _nextCupDateForTeam.HasValue
            ? await _cupService.GetFixtureForTeamOnDateAsync(playerTeam.Id, _nextCupDateForTeam.Value)
            : null;

        _nextSupercupDate = await _supercupService.GetNextSupercupDateAsync();
        _nextSupercupDateForTeam = await _supercupService.GetNextSupercupDateForTeamAsync(playerTeam.Id);
        
        var supercupFixtureForTeam = _nextSupercupDateForTeam.HasValue
            ? await _supercupService.GetFixtureForTeamOnDateAsync(playerTeam.Id, _nextSupercupDateForTeam.Value)
            : null;

        // Has the player a pending fixture (today or in the past)?
        _playerHasCupFixturePending = _nextCupDateForTeam.HasValue && _nextCupDateForTeam.Value.Date <= CurrentDate.Date;
        _playerHasSupercupFixturePending = _nextSupercupDateForTeam.HasValue && _nextSupercupDateForTeam.Value.Date <= CurrentDate.Date;

        DateTime? nextLeagueDate = matchweek > 0 ? LeagueService.GetMatchweekDate(matchweek, playerTeam.CompetitionName) : null;

        DateTime? minCupDate = _nextCupDate;
        if (_nextSupercupDate.HasValue && (!minCupDate.HasValue || _nextSupercupDate.Value < minCupDate.Value)) 
            minCupDate = _nextSupercupDate;
            
        DateTime? minCupDateForTeam = _nextCupDateForTeam;
        bool isSupercupNextForTeam = false;
        if (_nextSupercupDateForTeam.HasValue && (!minCupDateForTeam.HasValue || _nextSupercupDateForTeam.Value < minCupDateForTeam.Value))
        {
            minCupDateForTeam = _nextSupercupDateForTeam;
            isSupercupNextForTeam = true;
        }

        bool leagueFirst = nextLeagueDate.HasValue &&
            (!minCupDateForTeam.HasValue || nextLeagueDate.Value <= minCupDateForTeam.Value);
        bool cupFirst = minCupDateForTeam.HasValue &&
            (!nextLeagueDate.HasValue || minCupDateForTeam.Value < nextLeagueDate.Value);

        if (matchweek == -1 && !minCupDateForTeam.HasValue)
        {
            IsSeasonOver = true;
            NextMatchInfo = "Season Completed!";
            NextMatchDateText = string.Empty;
            NextCupMatchInfo = string.Empty;
            IsCupMatchday = false;
            if (setViewedMatchweekDefault && _allEventDates.Count > 0)
                _viewedEventIndex = _allEventDates.Count - 1;
            return;
        }

        IsSeasonOver = false;
        _nextMatchweek = matchweek;
        
        // Default to league, will be overwritten if cup is first or pending
        _nextHomeTeamId = homeId;
        _nextAwayTeamId = awayId;

        // Build next match info text
        if (leagueFirst && matchweek > 0)
        {
            var homeTeam = _db.Teams.Find(homeId);
            var awayTeam = _db.Teams.Find(awayId);
            _nextVenueName = homeTeam?.StadiumName ?? "";
            NextMatchInfo = $"Next: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = nextLeagueDate!.Value.ToString("dddd, MMM d, yyyy");
        }
        else if (cupFirst && isSupercupNextForTeam && supercupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(supercupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(supercupFixtureForTeam.AwayTeamId);
            _nextHomeTeamId = supercupFixtureForTeam.HomeTeamId;
            _nextAwayTeamId = supercupFixtureForTeam.AwayTeamId;
            _nextVenueName = supercupFixtureForTeam.VenueName ?? "Neutral Venue";
            NextMatchInfo = $"🏆 Supercup: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = _nextSupercupDateForTeam!.Value.ToString("dddd, MMM d, yyyy");
        }
        else if (cupFirst && !isSupercupNextForTeam && cupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(cupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(cupFixtureForTeam.AwayTeamId);
            _nextHomeTeamId = cupFixtureForTeam.HomeTeamId;
            _nextAwayTeamId = cupFixtureForTeam.AwayTeamId;
            _nextVenueName = cupFixtureForTeam.VenueName ?? homeTeam?.StadiumName ?? "";
            NextMatchInfo = $"🏆 Cup: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = _nextCupDateForTeam!.Value.ToString("dddd, MMM d, yyyy");
        }
        else if (cupFirst)
        {
            NextMatchInfo = matchweek > 0
                ? $"Next: {_db.Teams.Find(homeId)?.Name} vs {_db.Teams.Find(awayId)?.Name}"
                : "Season Completed!";
            NextMatchDateText = matchweek > 0
                ? nextLeagueDate!.Value.ToString("dddd, MMM d, yyyy")
                : string.Empty;
        }

        // Cup match info (secondary) - always show player's next cup or supercup game
        if (isSupercupNextForTeam && supercupFixtureForTeam != null && _nextSupercupDateForTeam.HasValue)
        {
            var ch = _db.Teams.Find(supercupFixtureForTeam.HomeTeamId);
            var ca = _db.Teams.Find(supercupFixtureForTeam.AwayTeamId);
            NextCupMatchInfo = $"🏆 {ch?.Name} vs {ca?.Name} • {_nextSupercupDateForTeam.Value:MMM d}";
        }
        else if (!isSupercupNextForTeam && cupFixtureForTeam != null && _nextCupDateForTeam.HasValue)
        {
            var ch = _db.Teams.Find(cupFixtureForTeam.HomeTeamId);
            var ca = _db.Teams.Find(cupFixtureForTeam.AwayTeamId);
            NextCupMatchInfo = $"🏆 {ch?.Name} vs {ca?.Name} • {_nextCupDateForTeam.Value:MMM d}";
        }
        else
        {
            NextCupMatchInfo = string.Empty;
        }

        if (setViewedMatchweekDefault && _allEventDates.Count > 0)
        {
            // Jump to the event closest to the next fixture
            var targetDate = leagueFirst && nextLeagueDate.HasValue ? nextLeagueDate.Value.Date
                : minCupDate?.Date ?? _allEventDates[0];
            _viewedEventIndex = _allEventDates.FindIndex(d => d.Date >= targetDate);
            if (_viewedEventIndex < 0) _viewedEventIndex = _allEventDates.Count - 1;
        }

        UpdateCalendarState();
    }

    private async Task<DateTime?> GetNextGlobalLeagueDateAsync(string season)
    {
        var unplayedRounds = await _db.LeagueFixtures
            .Where(f => f.Season == season && !f.IsPlayed)
            .Select(f => new { f.Round, f.CompetitionName })
            .Distinct()
            .ToListAsync();

        if (!unplayedRounds.Any()) return null;

        return unplayedRounds
            .Select(x => LeagueService.GetMatchweekDate(x.Round, x.CompetitionName))
            .Min();
    }

    private void UpdateCalendarState()
    {
        CurrentDateText = CurrentDate.ToString("dddd, MMM d, yyyy");

        if (IsSeasonOver || (_nextMatchweek <= 0 && !_nextCupDate.HasValue && !_nextSupercupDate.HasValue))
        {
            IsMatchdayToday = false;
            IsCupMatchday = false;
            
            var pTeam = _db.Teams.FirstOrDefault(t => t.IsPlayerTeam);
            string comp = pTeam?.CompetitionName ?? "Liga Florilor";
            SeasonCompletedMessage = $"The {comp} season has come to an end.";
        }
        else
        {
            var pTeam = _db.Teams.FirstOrDefault(t => t.IsPlayerTeam);
            DateTime leagueDate = LeagueService.GetMatchweekDate(_nextMatchweek, pTeam?.CompetitionName ?? "Liga Florilor");
            bool leaguePending = _nextMatchweek > 0 && CurrentDate.Date >= leagueDate.Date;
            bool cupPending = _playerHasCupFixturePending || _playerHasSupercupFixturePending;

            IsMatchdayToday = leaguePending || cupPending;
            IsCupMatchday = cupPending;
        }

        CanSimulateMatch = !IsBusy && !IsSeasonOver && IsMatchdayToday;
        CanAdvanceDay = !IsBusy && (!IsMatchdayToday || MatchSimulated || IsSeasonOver);
        CanSimulateToEndOfSeason = !IsBusy && !IsSeasonOver;

        if (IsSeasonOver)
        {
            var unlockDate = new DateTime(CurrentDate.Year, 6, 10);
            CanBeginNewSeason = CurrentDate.Date >= unlockDate;

            if (CurrentDate.Date >= unlockDate)
            {
                CanAdvanceDay = false;
            }
        }
        else
        {
            CanBeginNewSeason = false;
        }
    }

    partial void OnCurrentDateChanged(DateTime value) => UpdateCalendarState();

    [RelayCommand]
    private async Task ChangeMatchweekAsync(string? delta)
    {
        if (delta == null || !int.TryParse(delta, out int d)) return;

        int target = _viewedEventIndex + d;
        if (target < 0 || target >= _allEventDates.Count) return;

        _viewedEventIndex = target;
        
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        string competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        await LoadMatchweekResultsAsync(competitionName);
    }

    [RelayCommand]
    private void NavigateToSquadSelection()
    {
        if (!CanSimulateMatch) return;
        _onNavigateToSquadSelection?.Invoke(_nextHomeTeamId, _nextAwayTeamId, _nextVenueName, NextMatchInfo, IsCupMatchday);
    }

    [RelayCommand]
    private async Task SimulateMatchAsync()
    {
        if (!CanSimulateMatch) return;

        IsBusy = true;
        try
        {
            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            string competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
            
            DateTime leagueDate = LeagueService.GetMatchweekDate(_nextMatchweek, competitionName);
            bool isLeagueDay = _nextMatchweek > 0 && CurrentDate.Date >= leagueDate.Date;

            // Simulate league matches if it's a league matchday
            if (isLeagueDay)
            {
                var pTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
                string comp = pTeam?.CompetitionName ?? "Liga Florilor";
                
                // Simulate player's matchweek AND any other leagues' matches today
                await _simulationEngine.SimulateAllLeaguesForDateAsync(CurrentDate);
                
                LastResultText = $"Matchweek {_nextMatchweek} Completed";
            }
            else if (_playerHasSupercupFixturePending && _nextSupercupDateForTeam.HasValue)
            {
                await _simulationEngine.SimulateSupercupFixturesAsync(_nextSupercupDateForTeam.Value);
                LastResultText = "Supercup Matchday Completed";
            }
            else if (_playerHasCupFixturePending && _nextCupDateForTeam.HasValue)
            {
                await _simulationEngine.SimulateCupFixturesAsync(_nextCupDateForTeam.Value);
                LastResultText = "Cup Matchday Completed";
            }

            MatchSimulated = true;

            await BuildAllEventDatesAsync();

            var simulatedDate = CurrentDate.Date;
            _viewedEventIndex = _allEventDates.FindIndex(d => d.Date == simulatedDate);
            if (_viewedEventIndex < 0) _viewedEventIndex = Math.Max(0, _allEventDates.Count - 1);

            await LoadMatchweekResultsAsync(competitionName);
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);
        }
        finally
        {
            IsBusy = false;
            MatchSimulated = false;
            UpdateCalendarState();
        }
    }

    [RelayCommand]
    private async Task SimulateToEndOfSeasonAsync()
    {
        if (!CanSimulateToEndOfSeason) return;

        IsBusy = true;
        CanSimulateToEndOfSeason = false;
        try
        {
            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            string competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
            string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

            // Walk forward day by day, simulating every league on every matchday,
            // until no unplayed fixtures remain in ANY competition.
            var seasonEndCeiling = new DateTime(LeagueService.CurrentSeasonYear + 1, 6, 1);

            while (CurrentDate < seasonEndCeiling)
            {
                // Check if ALL leagues are done
                bool anyLeft = await _db.LeagueFixtures.AnyAsync(f => f.Season == season && !f.IsPlayed);
                if (!anyLeft) break;

                _clock.AdvanceDay();
                CurrentDate = _clock.CurrentDate;

                // Simulate all league fixtures for this date (handles every competition)
                await _simulationEngine.SimulateAllLeaguesForDateAsync(CurrentDate);

                // Simulate any supercup fixtures today (query DB directly — no stale fields)
                var supercupToday = await _supercupService.GetFixturesForDateAsync(CurrentDate);
                if (supercupToday.Any(f => !f.IsPlayed))
                    await _simulationEngine.SimulateSupercupFixturesAsync(CurrentDate);

                // Simulate any cup fixtures today
                var cupToday = await _cupService.GetFixturesForDateAsync(CurrentDate);
                if (cupToday.Any(f => !f.IsPlayed))
                    await _simulationEngine.SimulateCupFixturesAsync(CurrentDate);
            }

            // Finalise UI state
            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);
            UpdateCalendarState();

            if (_allEventDates.Count > 0)
            {
                _viewedEventIndex = _allEventDates.Count - 1;
                await LoadMatchweekResultsAsync(competitionName);
            }
        }
        finally
        {
            IsBusy = false;
            UpdateCalendarState();
        }
    }

    [RelayCommand]
    private async Task AdvanceDayAsync()
    {
        if (!CanAdvanceDay) return;

        IsBusy = true;
        try
        {
            MatchSimulated = false;

            while (_nextSupercupDate.HasValue && _nextSupercupDate.Value.Date <= CurrentDate.Date)
            {
                var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
                var playerFixture = await _supercupService.GetFixtureForTeamOnDateAsync(playerTeam!.Id, _nextSupercupDate.Value);
                
                if (playerFixture == null)
                {
                    await _simulationEngine.SimulateSupercupFixturesAsync(_nextSupercupDate.Value.Date);
                    _nextSupercupDate = await _supercupService.GetNextSupercupDateAsync();
                }
                else break;
            }

            while (_nextCupDate.HasValue && _nextCupDate.Value.Date <= CurrentDate.Date)
            {
                var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
                var playerFixture = await _cupService.GetFixtureForTeamOnDateAsync(playerTeam!.Id, _nextCupDate.Value);
                
                if (playerFixture == null)
                {
                    await _simulationEngine.SimulateCupFixturesAsync(_nextCupDate.Value.Date);
                    _nextCupDate = await _cupService.GetNextCupDateAsync();
                }
                else break;
            }

            _clock.AdvanceDay();
            CurrentDate = _clock.CurrentDate;
            // This ensures all leagues (including player's if not played manually) advance
            await _simulationEngine.SimulateAllLeaguesForDateAsync(CurrentDate);
            if (_onDayAdvanced != null) await _onDayAdvanced();

            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);
        }
        finally
        {
            IsBusy = false;
            UpdateCalendarState();
        }
    }

    [RelayCommand]
    private async Task ViewMatchDetailAsync(int matchId)
    {
        await _onNavigateToMatchDetail(matchId);
    }

    [RelayCommand]
    private async Task BeginNewSeasonAsync()
    {
        if (!IsSeasonOver || !CanBeginNewSeason || IsBusy) return;

        IsBusy = true;
        try
        {
            await _simulationEngine.ProcessEndOfSeasonAsync(CurrentDate);

            MatchSimulated = false;
            IsSeasonOver = false;

            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            string comp = playerTeam?.CompetitionName ?? "Liga Florilor";

            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
            await LoadMatchweekResultsAsync(comp);
            LastResultText = "New Season Started! Players Aged & Stats Archived.";
        }
        finally
        {
            IsBusy = false;
            UpdateCalendarState();
        }
    }
}