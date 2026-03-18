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

    // Total "event slots" for the matchweek browser: league matchweeks + cup dates
    private List<DateTime> _allEventDates = [];
    private int _viewedEventIndex;

    public HomeViewModel(HandballDbContext db, LeagueService leagueService, SimulationEngine simulationEngine,
        CupService cupService, SupercupService supercupService, GameClock clock, Func<int, Task> onNavigateToMatchDetail, Func<Task>? onDayAdvanced = null)
    {
        Title = "Home";
        _db = db;
        _leagueService = leagueService;
        _simulationEngine = simulationEngine;
        _cupService = cupService;
        _supercupService = supercupService;
        _clock = clock;
        _onNavigateToMatchDetail = onNavigateToMatchDetail;
        _onDayAdvanced = onDayAdvanced;
    }

    public async Task InitializeAsync()
    {
        if (CurrentDate == default)
            CurrentDate = _clock.CurrentDate.Date;

        await BuildAllEventDatesAsync();
        await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
        await LoadMatchweekResultsAsync();

        MatchSimulated = false;
        UpdateCalendarState();
    }

    /// <summary>
    /// Builds a merged, sorted list of all league + cup event dates for the results browser.
    /// </summary>
    private async Task BuildAllEventDatesAsync()
    {
        var leagueDates = LeagueService.MatchweekDates.ToList();
        var cupDates = await _cupService.GetAllCupDatesAsync();
        var supercupDates = await _supercupService.GetAllDatesAsync();

        var allDates = new HashSet<DateTime>(leagueDates);
        foreach (var cd in cupDates)
            allDates.Add(cd.Date);
        foreach (var sd in supercupDates)
            allDates.Add(sd.Date);

        _allEventDates = allDates.OrderBy(d => d).ToList();
    }

    private async Task LoadMatchweekResultsAsync()
    {
        if (_allEventDates.Count == 0) return;

        var date = _allEventDates[_viewedEventIndex];

        // Get league results for this date (by matchweek)
        var leagueMatchweekIndex = LeagueService.MatchweekDates.IndexOf(date);
        var leagueResults = leagueMatchweekIndex >= 0
            ? await _leagueService.GetResultsForMatchweekAsync(leagueMatchweekIndex + 1)
            : new List<MatchRecord>();

        // Get cup results for this date
        var cupResults = await _db.MatchRecords
            .Where(m => m.IsCupMatch && m.PlayedOn.Date == date.Date)
            .OrderBy(m => m.Id)
            .ToListAsync();

        // Also get unplayed cup fixtures for this date
        var unplayedCupFixtures = await _cupService.GetFixturesForDateAsync(date);
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

        MatchweekResults = leagueResults.Concat(cupResults).ToList();

        // Build header text
        if (leagueMatchweekIndex >= 0 && cupResults.Any())
            MatchweekDateText = $"Matchweek {leagueMatchweekIndex + 1} + Cupa României • {date:dddd, MMM d, yyyy}";
        else if (leagueMatchweekIndex >= 0)
            MatchweekDateText = $"Matchweek {leagueMatchweekIndex + 1} • {date:dddd, MMM d, yyyy}";
        else if (cupResults.Any())
            MatchweekDateText = $"Cupa României • {date:dddd, MMM d, yyyy}";
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

        DateTime? nextLeagueDate = matchweek > 0 ? LeagueService.GetMatchweekDate(matchweek) : null;

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
        _nextHomeTeamId = homeId;
        _nextAwayTeamId = awayId;
        _nextMatchweek = matchweek;

        // Build next match info text
        if (leagueFirst && matchweek > 0)
        {
            var homeTeam = _db.Teams.Find(homeId);
            var awayTeam = _db.Teams.Find(awayId);
            NextMatchInfo = $"Next: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = nextLeagueDate!.Value.ToString("dddd, MMM d, yyyy");
        }
        else if (cupFirst && isSupercupNextForTeam && supercupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(supercupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(supercupFixtureForTeam.AwayTeamId);
            NextMatchInfo = $"🏆 Supercup: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = _nextSupercupDateForTeam!.Value.ToString("dddd, MMM d, yyyy");
        }
        else if (cupFirst && !isSupercupNextForTeam && cupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(cupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(cupFixtureForTeam.AwayTeamId);
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

    private void UpdateCalendarState()
    {
        CurrentDateText = CurrentDate.ToString("dddd, MMM d, yyyy");

        if (IsSeasonOver || (_nextMatchweek <= 0 && !_nextCupDate.HasValue && !_nextSupercupDate.HasValue))
        {
            IsMatchdayToday = false;
            IsCupMatchday = false;
        }
        else
        {
            DateTime leagueDate = LeagueService.GetMatchweekDate(_nextMatchweek);
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
        await LoadMatchweekResultsAsync();
    }

    [RelayCommand]
    private async Task SimulateMatchAsync()
    {
        if (!CanSimulateMatch) return;

        IsBusy = true;
        try
        {
            DateTime leagueDate = LeagueService.GetMatchweekDate(_nextMatchweek);
            bool isLeagueDay = _nextMatchweek > 0 && CurrentDate.Date >= leagueDate.Date;

            // Simulate league matches if it's a league matchday
            if (isLeagueDay)
            {
                int simulatedWeek = _nextMatchweek;
                await _simulationEngine.SimulateMatchweekAsync(simulatedWeek);
                LastResultText = $"Matchweek {simulatedWeek} Completed";
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

            await LoadMatchweekResultsAsync();
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
            while (!IsSeasonOver)
            {
                DateTime? nextLeagueDate = _nextMatchweek > 0
                    ? LeagueService.GetMatchweekDate(_nextMatchweek)
                    : null;

                DateTime? minCupDate = _nextCupDate;
                if (_nextSupercupDate.HasValue && (!minCupDate.HasValue || _nextSupercupDate.Value < minCupDate.Value)) 
                    minCupDate = _nextSupercupDate;

                DateTime? nextEvent = null;
                if (nextLeagueDate.HasValue && minCupDate.HasValue)
                    nextEvent = nextLeagueDate.Value <= minCupDate.Value ? nextLeagueDate : minCupDate;
                else
                    nextEvent = nextLeagueDate ?? minCupDate;

                if (nextEvent == null) break;

                while (_clock.CurrentDate.Date < nextEvent.Value.Date)
                    _clock.AdvanceDay();
                CurrentDate = _clock.CurrentDate;

                bool isLeague = nextLeagueDate.HasValue && CurrentDate.Date == nextLeagueDate.Value.Date;
                bool isCup = _nextCupDate.HasValue && CurrentDate.Date == _nextCupDate.Value.Date;
                bool isSupercup = _nextSupercupDate.HasValue && CurrentDate.Date == _nextSupercupDate.Value.Date;

                if (isLeague)
                    await _simulationEngine.SimulateMatchweekAsync(_nextMatchweek);
                if (isSupercup)
                    await _simulationEngine.SimulateSupercupFixturesAsync(_nextSupercupDate!.Value);
                if (isCup)
                    await _simulationEngine.SimulateCupFixturesAsync(_nextCupDate!.Value);

                if (_onDayAdvanced != null) await _onDayAdvanced();

                await BuildAllEventDatesAsync();
                await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);

                if (IsSeasonOver) break;
            }

            if (_allEventDates.Count > 0)
                _viewedEventIndex = _allEventDates.Count - 1;
            await LoadMatchweekResultsAsync();
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
            await _simulationEngine.ProcessDailyProgressionAsync(CurrentDate);
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

            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
            await LoadMatchweekResultsAsync();
            LastResultText = "New Season Started! Players Aged & Stats Archived.";
        }
        finally
        {
            IsBusy = false;
            UpdateCalendarState();
        }
    }
}