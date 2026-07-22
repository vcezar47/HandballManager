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
    private readonly GameNotificationService? _notifications;

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

    /// <summary>Season finished, but the new one cannot start yet.</summary>
    [ObservableProperty]
    private bool _isAwaitingNewSeason;

    /// <summary>The season rollover is running. Drives the blocking progress overlay.</summary>
    [ObservableProperty]
    private bool _isRollingOverSeason;

    [ObservableProperty]
    private string _newSeasonUnlockText = string.Empty;

    [ObservableProperty]
    private bool _isCupMatchday;

    [ObservableProperty]
    private string _nextCupMatchInfo = string.Empty;

    [ObservableProperty]
    private bool _isNextMatchTrophyEvent;

    /// <summary>
    /// Pause between simulated days. Fast enough to feel like skipping, slow enough
    /// that STOP is a comfortable target rather than a timing exercise.
    /// </summary>
    private const int AutoAdvanceDayDelayMs = 250;

    private bool _stopAutoAdvanceRequested;

    [ObservableProperty]
    private bool _isAutoAdvancing;

    /// <summary>Why continuous advance came to a halt, shown once it stops.</summary>
    [ObservableProperty]
    private string _autoAdvanceStoppedReason = string.Empty;

    [ObservableProperty]
    private bool _isTransferWindowOpen;

    /// <summary>e.g. "Summer window · 12 days left". Empty when no window is open.</summary>
    [ObservableProperty]
    private string _transferWindowBadge = string.Empty;

    /// <summary>Green while there's time, amber inside the last week, red on the closing day.</summary>
    [ObservableProperty]
    private TransferWindowUrgency _transferWindowUrgency;

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
        CupService cupService, SupercupService supercupService, GameClock clock, Func<int, Task> onNavigateToMatchDetail, Action<int, int, string, string, bool>? onNavigateToSquadSelection = null, Func<Task>? onDayAdvanced = null,
        GameNotificationService? notifications = null)
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
        _notifications = notifications;
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
        string season = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        int maxRound = await _db.LeagueFixtures
            .Where(f => f.Season == season && f.CompetitionName == comp)
            .MaxAsync(f => (int?)f.Round) ?? 0;

        // Ensure we at least show dates up to maxRound (handles dynamic playoffs)
        var leagueDates = LeagueService.GetMatchweekDates(comp).Take(maxRound).Select(d => d.Date);
        var allDates = new HashSet<DateTime>(leagueDates);

        var cupDates = await _cupService.GetAllCupDatesAsync(comp);
        foreach (var cd in cupDates) allDates.Add(cd.Date);

        foreach (var f in await _supercupService.GetKnockoutFixturesAsync(comp))
            allDates.Add(f.ScheduledDate.Date);

        _allEventDates = allDates.OrderBy(d => d.Date).ToList();
    }

    private async Task LoadMatchweekResultsAsync(string competitionName = "Liga Florilor")
    {
        if (_allEventDates.Count == 0) return;

        var date = _allEventDates[_viewedEventIndex];

        var leagueCalendar = LeagueService.GetMatchweekDates(competitionName);
        var leagueMatchweekIndex = leagueCalendar.FindIndex(d => d.Date == date.Date);
        var leagueResults = new List<MatchRecord>();
        if (leagueMatchweekIndex >= 0)
        {
            var matchweek = leagueMatchweekIndex + 1;

            if (string.Equals(competitionName, LeagueService.KvindeligaenCompetition, StringComparison.Ordinal))
            {
                var playedLeagueFx = await _db.LeagueFixtures
                    .Include(f => f.MatchRecord)
                    .Where(f => f.IsPlayed && f.MatchRecord != null && f.MatchRecord.PlayedOn.Date == date.Date && f.CompetitionName == competitionName)
                    .OrderBy(f => f.MatchRecordId)
                    .ToListAsync();
                    
                foreach (var f in playedLeagueFx)
                {
                    var m = f.MatchRecord!;
                    m.LeagueSubtitle = LeagueService.FormatKvindeligaenFixtureListSubtitle(f.Phase, f.Round, f.PlayoffSeriesId, f.PlayoffLeg);
                    leagueResults.Add(m);
                }
            }
            else
                leagueResults = await _leagueService.GetResultsForMatchweekAsync(matchweek, competitionName);

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
                    IsUnplayedPlaceholder = true,
                    PlayedOn = date,
                    MatchweekNumber = matchweek,
                    LeagueSubtitle = string.Equals(competitionName, LeagueService.KvindeligaenCompetition, StringComparison.Ordinal)
                        ? LeagueService.FormatKvindeligaenFixtureListSubtitle(f.Phase, f.Round, f.PlayoffSeriesId, f.PlayoffLeg)
                        : null
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
                IsUnplayedPlaceholder = true,
                CupRound = f.Round == "Group" ? $"Group {f.CupGroup?.GroupName}" : f.Round,
                PlayedOn = f.ScheduledDate
            });
        }
        
        if (competitionName == "Liga Florilor" || competitionName == "Kvindeligaen")
        {
            var unplayedSupercupFixtures = await _supercupService.GetFixturesForDateAsync(date);
            foreach (var f in unplayedSupercupFixtures.Where(f => !f.IsPlayed && f.CompetitionName == competitionName))
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
                    IsUnplayedPlaceholder = true,
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
             r.CupRound?.Contains("Supercupa", StringComparison.OrdinalIgnoreCase) == true ||
             r.CupRound?.Contains("Bambuni", StringComparison.OrdinalIgnoreCase) == true));
        
        bool hasCup = cupResults.Any(r => r.IsCupMatch && !hasSupercup);
        
        string compCupName = competitionName switch
        {
            "NB I" => "Magyar Kupa",
            "Ligue Butagaz Énergie" => "Coupe de France",
            "Kvindeligaen" => "Landspokalturnering",
            _ => "Cupa României"
        };
        string compSupercupName = competitionName switch
        {
            "NB I" => "Szuperkupa",
            "Kvindeligaen" => "Bambuni Supercup",
            _ => "Supercupa României"
        };

        string leagueCaption = "";
        if (leagueMatchweekIndex >= 0)
        {
            int mwSlot = leagueMatchweekIndex + 1;
            if (string.Equals(competitionName, LeagueService.KvindeligaenCompetition, StringComparison.Ordinal))
            {
                var sampleFx = await _leagueService.GetFixturesForRoundAsync(mwSlot, competitionName);
                var sample = sampleFx.FirstOrDefault();
                leagueCaption = sample != null
                    ? LeagueService.FormatKvindeligaenLeagueBanner(sample.Round, sample.Phase, sample.PlayoffSeriesId, sample.PlayoffLeg)
                    : $"Kvindeligaen — Round {mwSlot}";
            }
            else
                leagueCaption = $"Matchweek {mwSlot}";
        }

        if (leagueMatchweekIndex >= 0 && (hasCup || hasSupercup))
        {
            string tournamentPart = hasSupercup ? compSupercupName : compCupName;
            MatchweekDateText = $"{leagueCaption} + {tournamentPart} • {date:dddd, MMM d, yyyy}";
        }
        else if (leagueMatchweekIndex >= 0)
            MatchweekDateText = $"{leagueCaption} • {date:dddd, MMM d, yyyy}";
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

        _nextSupercupDate = await _supercupService.GetNextSupercupDateAsync(playerTeam.CompetitionName);
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
            IsNextMatchTrophyEvent = false;
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
            IsNextMatchTrophyEvent = false;
        }
        else if (cupFirst && isSupercupNextForTeam && supercupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(supercupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(supercupFixtureForTeam.AwayTeamId);
            _nextHomeTeamId = supercupFixtureForTeam.HomeTeamId;
            _nextAwayTeamId = supercupFixtureForTeam.AwayTeamId;
            _nextVenueName = supercupFixtureForTeam.VenueName ?? "Neutral Venue";
            NextMatchInfo = $"Supercup: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = _nextSupercupDateForTeam!.Value.ToString("dddd, MMM d, yyyy");
            IsNextMatchTrophyEvent = true;
        }
        else if (cupFirst && !isSupercupNextForTeam && cupFixtureForTeam != null)
        {
            var homeTeam = _db.Teams.Find(cupFixtureForTeam.HomeTeamId);
            var awayTeam = _db.Teams.Find(cupFixtureForTeam.AwayTeamId);
            _nextHomeTeamId = cupFixtureForTeam.HomeTeamId;
            _nextAwayTeamId = cupFixtureForTeam.AwayTeamId;
            _nextVenueName = cupFixtureForTeam.VenueName ?? homeTeam?.StadiumName ?? "";
            NextMatchInfo = $"Cup: {homeTeam?.Name} vs {awayTeam?.Name}";
            NextMatchDateText = _nextCupDateForTeam!.Value.ToString("dddd, MMM d, yyyy");
            IsNextMatchTrophyEvent = true;
        }
        else if (cupFirst)
        {
            NextMatchInfo = matchweek > 0
                ? $"Next: {_db.Teams.Find(homeId)?.Name} vs {_db.Teams.Find(awayId)?.Name}"
                : "Season Completed!";
            NextMatchDateText = matchweek > 0
                ? nextLeagueDate!.Value.ToString("dddd, MMM d, yyyy")
                : string.Empty;
            IsNextMatchTrophyEvent = false;
        }

        // Cup match info (secondary) - always show player's next cup or supercup game
        if (isSupercupNextForTeam && supercupFixtureForTeam != null && _nextSupercupDateForTeam.HasValue)
        {
            var ch = _db.Teams.Find(supercupFixtureForTeam.HomeTeamId);
            var ca = _db.Teams.Find(supercupFixtureForTeam.AwayTeamId);
            NextCupMatchInfo = $"{ch?.Name} vs {ca?.Name} • {_nextSupercupDateForTeam.Value:MMM d}";
        }
        else if (!isSupercupNextForTeam && cupFixtureForTeam != null && _nextCupDateForTeam.HasValue)
        {
            var ch = _db.Teams.Find(cupFixtureForTeam.HomeTeamId);
            var ca = _db.Teams.Find(cupFixtureForTeam.AwayTeamId);
            NextCupMatchInfo = $"{ch?.Name} vs {ca?.Name} • {_nextCupDateForTeam.Value:MMM d}";
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

        var window = LeagueService.GetTransferWindowStatus(CurrentDate);
        IsTransferWindowOpen = window.IsOpen;
        TransferWindowBadge = window.BadgeText;
        TransferWindowUrgency = window.Urgency;

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

        CanSimulateMatch = !IsBusy && !IsAutoAdvancing && !IsSeasonOver && IsMatchdayToday;
        CanAdvanceDay = !IsBusy && (!IsMatchdayToday || MatchSimulated || IsSeasonOver);
        CanSimulateToEndOfSeason = !IsBusy && !IsAutoAdvancing && !IsSeasonOver;

        if (IsSeasonOver)
        {
            var unlockDate = new DateTime(CurrentDate.Year, 6, 30);
            CanBeginNewSeason = CurrentDate.Date >= unlockDate;

            if (CanBeginNewSeason)
            {
                CanAdvanceDay = false;
                IsAwaitingNewSeason = false;
                NewSeasonUnlockText = string.Empty;
            }
            else
            {
                // Pre-season still has days to run. Say so, rather than showing a button
                // that looks live and does nothing when tapped.
                IsAwaitingNewSeason = true;
                NewSeasonUnlockText = $"Pre-season runs until {unlockDate:MMMM d} — keep advancing.";
            }
        }
        else
        {
            CanBeginNewSeason = false;
            IsAwaitingNewSeason = false;
            NewSeasonUnlockText = string.Empty;
        }
    }

    partial void OnCurrentDateChanged(DateTime value) => UpdateCalendarState();

    /// <summary>
    /// Toasts the transfer window opening and its final day. Only called from the
    /// day-by-day advance — bulk simulation would otherwise fire these mid-skip.
    /// </summary>
    private void AnnounceTransferWindowChange(DateTime date)
    {
        if (_notifications == null) return;

        var window = LeagueService.GetTransferWindowStatus(date);
        if (!window.IsOpen) return;

        if (window.IsOpeningDay)
        {
            _notifications.Post(GameNotificationKind.TransferWindow,
                $"{window.Name} is open",
                $"Open until {window.End:MMMM d}. You can buy, sell and negotiate.",
                route: NotificationRoutes.Transfers);
        }
        else if (window.IsFinalDay)
        {
            _notifications.Post(GameNotificationKind.TransferWindow,
                "Final day of the transfer window",
                $"The {window.Name.ToLowerInvariant()} closes tonight. Last chance to get deals done.",
                route: NotificationRoutes.Transfers);
        }
    }

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

            if (_onDayAdvanced != null) await _onDayAdvanced();
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
            var seasonEndCeiling = new DateTime(LeagueService.CurrentSeasonYear + 1, 6, 30);

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

            // Kvindeligaen Bo3 playoff completion: legs are generated dynamically
            // after each round is played, so the day-by-day loop above may exit
            // before all series (semi-finals, final, 3rd place) are created and played.
            // Keep simulating until every Kvindeligaen fixture is done.
            for (int kvAttempt = 0; kvAttempt < 30; kvAttempt++)
            {
                bool kvLeft = await _db.LeagueFixtures.AnyAsync(f =>
                    f.Season == season && !f.IsPlayed
                    && f.CompetitionName == LeagueService.KvindeligaenCompetition);
                if (!kvLeft) break;

                // Reuse the normal simulation path (which calls AfterKvindeligaenLeagueDayAsync internally)
                await _simulationEngine.SimulateAllLeaguesForDateAsync(CurrentDate);
                await _db.SaveChangesAsync();
            }

            // Finalise UI state
            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);
            UpdateCalendarState();

            if (_allEventDates.Count > 0)
            {
                var exact = _allEventDates.FindIndex(d => d.Date == CurrentDate.Date);
                _viewedEventIndex = exact >= 0 ? exact : _allEventDates.FindLastIndex(d => d.Date <= CurrentDate.Date);
                if (_viewedEventIndex < 0) _viewedEventIndex = _allEventDates.Count - 1;
                await LoadMatchweekResultsAsync(competitionName);
            }

            if (_onDayAdvanced != null) await _onDayAdvanced();
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
            await AdvanceOneDayAsync();
        }
        finally
        {
            IsBusy = false;
            UpdateCalendarState();
        }
    }

    /// <summary>
    /// Advances the world by one day and reports why the player might want to stop
    /// here. Returns null when the day passed without anything worth surfacing.
    /// </summary>
    private async Task<string?> AdvanceOneDayAsync()
    {
        MatchSimulated = false;
        bool seasonWasOver = IsSeasonOver;

        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);

        // Taken before the day runs so the day's changes can be diffed out of it.
        var before = await SnapshotAsync(playerTeam);

        while (_nextSupercupDate.HasValue && _nextSupercupDate.Value.Date <= CurrentDate.Date)
        {
            var playerFixture = await _supercupService.GetFixtureForTeamOnDateAsync(playerTeam!.Id, _nextSupercupDate.Value);

            if (playerFixture == null)
            {
                await _simulationEngine.SimulateSupercupFixturesAsync(_nextSupercupDate.Value.Date);
                _nextSupercupDate = await _supercupService.GetNextSupercupDateAsync(playerTeam!.CompetitionName);
            }
            else break;
        }

        while (_nextCupDate.HasValue && _nextCupDate.Value.Date <= CurrentDate.Date)
        {
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
        AnnounceTransferWindowChange(CurrentDate);

        // Always process daily progression for the new date (transfers, contracts, training progression, etc.)
        await _simulationEngine.ProcessDailyProgressionAsync(CurrentDate);

        // Only simulate other league/cup matches if the player does NOT have a match today.
        // If the player has a match today, we stop advancing and wait for the player to play/simulate it via MATCHDAY.
        bool playerHasMatchToday = playerTeam != null && await PlayerHasMatchOnDateAsync(playerTeam.Id, CurrentDate);

        if (!playerHasMatchToday)
        {
            await _simulationEngine.SimulateAllLeaguesForDateAsync(CurrentDate);
        }

        if (_onDayAdvanced != null) await _onDayAdvanced();

        await BuildAllEventDatesAsync();
        await UpdateNextFixtureAsync(setViewedMatchweekDefault: false);

        var after = await SnapshotAsync(playerTeam);
        AnnounceDayEvents(before, after);
        return DetectHaltReason(before, after, playerHasMatchToday, seasonWasOver);
    }

    /// <summary>
    /// The bits of club state worth watching across a single day. Cheap enough to take
    /// twice per simulated day during continuous advance.
    /// </summary>
    private sealed record DaySnapshot(
        int PendingOffers,
        int YouthIntake,
        int Trophies,
        IReadOnlyList<(int Id, string Name)> Squad);

    private static readonly DaySnapshot EmptySnapshot = new(0, 0, 0, []);

    private async Task<DaySnapshot> SnapshotAsync(Team? team)
    {
        if (team == null) return EmptySnapshot;

        int offers = await _db.TransferOffers
            .CountAsync(o => o.ForPlayer != null && o.ForPlayer.TeamId == team.Id && o.Status == "Pending");

        int youth = await _db.YouthIntakePlayers
            .CountAsync(y => y.ClubId == team.Id && y.IntakeYear == CurrentDate.Year);

        int trophies = await _db.ChampionRecords.CountAsync(r => r.TeamId == team.Id)
                     + await _db.CupWinnerRecords.CountAsync(r => r.TeamId == team.Id)
                     + await _db.SupercupWinnerRecords.CountAsync(r => r.TeamId == team.Id);

        var squad = await _db.Players
            .Where(p => p.TeamId == team.Id && !p.IsRetired)
            .Select(p => new ValueTuple<int, string>(p.Id, p.FirstName + " " + p.LastName))
            .ToListAsync();

        return new DaySnapshot(offers, youth, trophies, squad);
    }

    /// <summary>
    /// Toasts what changed today. Squad arrivals/departures are diffed rather than read
    /// from the news feed, which carries every transfer in the world and would spam.
    /// </summary>
    private void AnnounceDayEvents(DaySnapshot before, DaySnapshot after)
    {
        if (_notifications == null) return;

        if (after.Trophies > before.Trophies)
        {
            _notifications.Post(GameNotificationKind.Trophy,
                "Trophy won!",
                "Your club has lifted silverware. Check the trophy cabinet.",
                NotificationRoutes.Honours);
        }

        if (after.YouthIntake > before.YouthIntake)
        {
            _notifications.Post(GameNotificationKind.YouthIntake,
                "Youth intake has arrived",
                $"{after.YouthIntake - before.YouthIntake} prospects have come through the academy.",
                NotificationRoutes.Youth);
        }

        var beforeIds = before.Squad.Select(p => p.Id).ToHashSet();
        var afterIds = after.Squad.Select(p => p.Id).ToHashSet();

        foreach (var arrival in after.Squad.Where(p => !beforeIds.Contains(p.Id)))
        {
            _notifications.Post(GameNotificationKind.Transfer,
                "Signing confirmed", $"{arrival.Name} has joined the club.", NotificationRoutes.Transfers);
        }

        foreach (var departure in before.Squad.Where(p => !afterIds.Contains(p.Id)))
        {
            _notifications.Post(GameNotificationKind.Transfer,
                "Departure confirmed", $"{departure.Name} has left the club.", NotificationRoutes.Transfers);
        }

        if (after.PendingOffers > before.PendingOffers)
        {
            _notifications.Post(GameNotificationKind.Transfer,
                "New transfer offer",
                "A club has made an offer for one of your players.",
                NotificationRoutes.Transfers);
        }
    }

    /// <summary>
    /// Anything the player would not want skipped past. Routine days — league results
    /// elsewhere, news, training ticks — deliberately return null so auto-advance rolls on.
    /// </summary>
    private string? DetectHaltReason(DaySnapshot before, DaySnapshot after, bool playerHasMatchToday, bool seasonWasOver)
    {
        if (playerHasMatchToday) return "Matchday";

        // Only the day the season ends is worth stopping for. Reporting it every day
        // afterwards pinned auto-advance in place through the whole of pre-season.
        if (IsSeasonOver && !seasonWasOver) return "The season is over";
        if (after.PendingOffers > before.PendingOffers) return "A new transfer offer has arrived";
        if (after.YouthIntake > before.YouthIntake) return "Your youth intake has arrived";
        if (after.Trophies > before.Trophies) return "Your club has won a trophy";

        var window = LeagueService.GetTransferWindowStatus(CurrentDate);
        if (window.IsOpeningDay) return $"{window.Name} is open";
        if (window.IsFinalDay) return "Final day of the transfer window";

        return null;
    }

    /// <summary>
    /// Asks the running advance to finish its current day and stop.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="StartAutoAdvanceCommand"/> on purpose. An
    /// <c>AsyncRelayCommand</c> reports <c>CanExecute == false</c> for as long as its
    /// task is running, so a single toggle command is unclickable during the very loop
    /// it is meant to interrupt.
    /// </remarks>
    [RelayCommand]
    private void StopAutoAdvance() => _stopAutoAdvanceRequested = true;

    /// <summary>
    /// Runs days back to back until something needs attention or the player taps stop.
    /// Deliberately does not set <see cref="BaseViewModel.IsBusy"/> — the busy overlay
    /// would cover the screen and swallow the stop tap.
    /// </summary>
    [RelayCommand]
    private async Task StartAutoAdvanceAsync()
    {
        if (IsAutoAdvancing || !CanAdvanceDay) return;

        IsAutoAdvancing = true;
        _stopAutoAdvanceRequested = false;
        AutoAdvanceStoppedReason = string.Empty;

        try
        {
            while (!_stopAutoAdvanceRequested)
            {
                string? halt = await AdvanceOneDayAsync();
                UpdateCalendarState();

                if (halt != null)
                {
                    AutoAdvanceStoppedReason = halt;
                    break;
                }

                if (!CanAdvanceDay)
                {
                    if (CanBeginNewSeason) AutoAdvanceStoppedReason = "Pre-season is over — the new season can begin";
                    break;
                }

                // Lets the date tick visibly and keeps the stop button responsive.
                await Task.Delay(AutoAdvanceDayDelayMs);
            }
        }
        finally
        {
            IsAutoAdvancing = false;
            _stopAutoAdvanceRequested = false;
            UpdateCalendarState();
        }
    }

    [RelayCommand]
    private async Task ViewMatchDetailAsync(int matchId)
    {
        if (matchId <= 0) return;
        await _onNavigateToMatchDetail(matchId);
    }

    [RelayCommand]
    private async Task BeginNewSeasonAsync()
    {
        if (!IsSeasonOver || !CanBeginNewSeason || IsBusy) return;

        IsBusy = true;
        IsRollingOverSeason = true;
        try
        {
            // Off the UI thread: even trimmed down this clears a season of match data for
            // every league in the game, and Android kills an app whose main thread stalls
            // for a few seconds. Nothing else touches the DbContext meanwhile — the
            // overlay covers the screen and the tab bar is hidden.
            var seasonEnd = CurrentDate;
            await Task.Run(() => _simulationEngine.ProcessEndOfSeasonAsync(seasonEnd));

            MatchSimulated = false;
            IsSeasonOver = false;

            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            string comp = playerTeam?.CompetitionName ?? "Liga Florilor";

            await BuildAllEventDatesAsync();
            await UpdateNextFixtureAsync(setViewedMatchweekDefault: true);
            await LoadMatchweekResultsAsync(comp);
            LastResultText = "New Season Started! Players Aged & Stats Archived.";

            if (_onDayAdvanced != null) await _onDayAdvanced();
        }
        finally
        {
            IsBusy = false;
            IsRollingOverSeason = false;
            UpdateCalendarState();
        }
    }

    private async Task<bool> PlayerHasMatchOnDateAsync(int teamId, DateTime date)
    {
        var playerTeam = await _db.Teams.FindAsync(teamId);
        if (playerTeam == null) return false;

        // 1. League match
        var (_, _, matchweek) = await _leagueService.GetNextFixtureAsync(teamId);
        if (matchweek > 0)
        {
            var leagueDate = LeagueService.GetMatchweekDate(matchweek, playerTeam.CompetitionName);
            if (leagueDate.Date == date.Date)
                return true;
        }

        // 2. Cup match
        var cupFixture = await _cupService.GetFixtureForTeamOnDateAsync(teamId, date);
        if (cupFixture != null)
            return true;

        // 3. Supercup match
        var supercupFixture = await _supercupService.GetFixtureForTeamOnDateAsync(teamId, date);
        if (supercupFixture != null)
            return true;

        return false;
    }
}