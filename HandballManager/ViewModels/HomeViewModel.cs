using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly LeagueService _leagueService;
    private readonly SimulationEngine _simulationEngine;
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

    private int _nextHomeTeamId;
    private int _nextAwayTeamId;
    private int _nextMatchweek;

    public HomeViewModel(HandballDbContext db, LeagueService leagueService, SimulationEngine simulationEngine, GameClock clock, Func<int, Task> onNavigateToMatchDetail, Func<Task>? onDayAdvanced = null)
    {
        Title = "Home";
        _db = db;
        _leagueService = leagueService;
        _simulationEngine = simulationEngine;
        _clock = clock;
        _onNavigateToMatchDetail = onNavigateToMatchDetail;
        _onDayAdvanced = onDayAdvanced;
    }

    public async Task InitializeAsync()
    {
        if (CurrentDate == default)
            CurrentDate = _clock.CurrentDate.Date;

        await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
        await LoadMatchweekResultsAsync();

        MatchSimulated = false;
        UpdateCalendarState();
    }

    private async Task LoadMatchweekResultsAsync()
    {
        MatchweekResults = await _leagueService.GetResultsForMatchweekAsync(ViewedMatchweek);
        MatchweekDateText = LeagueService.GetMatchweekDate(ViewedMatchweek).ToString("dddd, MMM d, yyyy");
    }

    private async Task UpdateNextFixtureAsync(bool setViewedMatchweekDefault)
    {
        var playerTeam = _db.Teams.First(t => t.IsPlayerTeam);
        var (homeId, awayId, matchweek) = await _leagueService.GetNextFixtureAsync(playerTeam.Id);

        if (matchweek == -1)
        {
            IsSeasonOver = true;
            NextMatchInfo = "Season Completed!";
            NextMatchDateText = string.Empty;
            if (setViewedMatchweekDefault)
                ViewedMatchweek = LeagueService.MaxMatchweeks;
            return;
        }

        IsSeasonOver = false;
        _nextHomeTeamId = homeId;
        _nextAwayTeamId = awayId;
        _nextMatchweek = matchweek;

        var homeTeam = _db.Teams.Find(homeId);
        var awayTeam = _db.Teams.Find(awayId);
        NextMatchInfo = $"Next: {homeTeam?.Name} vs {awayTeam?.Name}";

        var nextDate = LeagueService.GetMatchweekDate(matchweek).Date;
        NextMatchDateText = nextDate.ToString("dddd, MMM d, yyyy");

        if (setViewedMatchweekDefault)
            ViewedMatchweek = Math.Max(1, matchweek);
    }

    private void UpdateCalendarState()
    {
        CurrentDateText = CurrentDate.ToString("dddd, MMM d, yyyy");

        if (IsSeasonOver || _nextMatchweek <= 0)
        {
            IsMatchdayToday = false;
        }
        else
        {
            var matchdayDate = LeagueService.GetMatchweekDate(_nextMatchweek).Date;
            IsMatchdayToday = CurrentDate.Date == matchdayDate;
        }

        CanSimulateMatch = !IsBusy && !IsSeasonOver && IsMatchdayToday;
        CanAdvanceDay = !IsBusy && (!IsMatchdayToday || MatchSimulated || IsSeasonOver);
        CanSimulateToEndOfSeason = !IsBusy && !IsSeasonOver;

        if (IsSeasonOver)
        {
            // Unlock "Begin New Season" only once we've reached (or passed) June 10th
            // of the current calendar year, and freeze daily advancement beyond that date.
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

        int target = ViewedMatchweek + d;
        if (target < 1 || target > LeagueService.MaxMatchweeks) return;

        ViewedMatchweek = target;
        await LoadMatchweekResultsAsync();
    }

    [RelayCommand]
    private async Task SimulateMatchAsync()
    {
        if (!CanSimulateMatch) return;

        IsBusy = true;
        try
        {
            int simulatedWeek = _nextMatchweek;
            await _simulationEngine.SimulateMatchweekAsync(simulatedWeek);
            LastResultText = $"Matchweek {simulatedWeek} Completed";
            MatchSimulated = true;

            // Immediately jump to the week just simulated to show results
            ViewedMatchweek = simulatedWeek;
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
            // Keep simulating every remaining matchweek until the season ends
            while (!IsSeasonOver)
            {
                await _simulationEngine.SimulateMatchweekAsync(_nextMatchweek);

                // Advance clock to the simulated week's date
                var matchDate = LeagueService.GetMatchweekDate(_nextMatchweek).Date;
                while (_clock.CurrentDate.Date < matchDate)
                    _clock.AdvanceDay();

                CurrentDate = _clock.CurrentDate;
                if (_onDayAdvanced != null) await _onDayAdvanced();
                await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);

                if (IsSeasonOver) break;
            }

            // Show final matchweek results
            ViewedMatchweek = LeagueService.MaxMatchweeks;
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
        _clock.AdvanceDay();
        CurrentDate = _clock.CurrentDate;
        await _simulationEngine.ProcessDailyProgressionAsync(CurrentDate);
        if (_onDayAdvanced != null) await _onDayAdvanced();
        // OnCurrentDateChanged recalculates flags/text.
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
            // Process end-of-season transitions while keeping the in-game date continuous.
            await _simulationEngine.ProcessEndOfSeasonAsync(CurrentDate);

            // Reset UI state for the new season
            MatchSimulated = false;
            IsSeasonOver = false;

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