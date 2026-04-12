using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandballManager.ViewModels;

public partial class LiveMatchViewModel : BaseViewModel
{
    private readonly LiveMatchEngine _engine;
    private readonly bool _isUserHome;
    private readonly Action<LiveMatchEngine, SquadSelection, SquadSelection> _onMatchEnd;
    private System.Diagnostics.Stopwatch _stopwatch = new();
    private double _lastTotalSeconds = 0;
    private double _lastCardSyncGameSecond = -1;

    // AI timeout cooldown: don't call more than once per 10 game-minutes
    private int _lastAiTimeoutMinuteHome = -99;
    private int _lastAiTimeoutMinuteAway = -99;

    [ObservableProperty] private string _homeTeamName = string.Empty;
    [ObservableProperty] private string _awayTeamName = string.Empty;
    [ObservableProperty] private string _homeTeamLogo = string.Empty;
    [ObservableProperty] private string _awayTeamLogo = string.Empty;
    [ObservableProperty] private int _homeScore;
    [ObservableProperty] private int _awayScore;
    [ObservableProperty] private string _matchClock = "00:00";
    [ObservableProperty] private int _speedMultiplier = 1;
    [ObservableProperty] private bool _isPaused = true;
    [ObservableProperty] private bool _isMatchOver;
    [ObservableProperty] private bool _isHalfTime;
    [ObservableProperty] private bool _isExtraTimeHalfTime;
    [ObservableProperty] private string _venueName = string.Empty;
    [ObservableProperty] private int _attendance;
    [ObservableProperty] private int _timeoutsRemaining;

    // Timeout overlay
    [ObservableProperty] private bool _isTimeoutActive;
    [ObservableProperty] private string _timeoutCallerLogo = string.Empty;
    [ObservableProperty] private string _timeoutCallerName = string.Empty;

    // Statistical Dashboard Properties
    [ObservableProperty] private int _homeShots;
    [ObservableProperty] private int _awayShots;
    [ObservableProperty] private int _homeShotsOnTarget;
    [ObservableProperty] private int _awayShotsOnTarget;
    [ObservableProperty] private double _homePossessionPercent = 50.0;
    [ObservableProperty] private double _awayPossessionPercent = 50.0;
    [ObservableProperty] private string _homeEfficiencyText = "0%";
    [ObservableProperty] private string _awayEfficiencyText = "0%";

    // Extra time / shootout
    [ObservableProperty] private bool _isShootoutActive;
    [ObservableProperty] private int _homeShootoutScore;
    [ObservableProperty] private int _awayShootoutScore;
    [ObservableProperty] private string _matchPhaseLabel = "1st Half";

    public ObservableCollection<LivePlayerCard> UserStartingCards { get; } = new();
    public ObservableCollection<LivePlayerCard> OpponentStartingCards { get; } = new();
    public ObservableCollection<LiveMatchEvent> EventFeed => _engine.EventLog;
    public bool HomeHasPossession => _engine.HomeHasPossession;
    public string CurrentPhase => _engine.CurrentPhase;

    // Sub state
    [ObservableProperty] private bool _isSubstitutionPanelOpen;
    [ObservableProperty] private LivePlayerCard? _playerBeingSubstituted;
    [ObservableProperty] private ObservableCollection<Player> _availableSubstitutes = new();

    public LiveMatchViewModel(LiveMatchEngine engine, bool isUserHome, Action<LiveMatchEngine, SquadSelection, SquadSelection> onMatchEnd)
    {
        Title = "LIVE MATCH";
        _engine = engine;
        _isUserHome = isUserHome;
        _onMatchEnd = onMatchEnd;

        HomeTeamName = engine.HomeTeam.Name;
        AwayTeamName = engine.AwayTeam.Name;
        HomeTeamLogo = engine.HomeTeam.LogoPath;
        AwayTeamLogo = engine.AwayTeam.LogoPath;
        VenueName = engine.VenueName;
        Attendance = (int)(engine.HomeTeam.StadiumCapacity * engine.HomeAdvantage);
        TimeoutsRemaining = isUserHome ? engine.HomeTimeoutsRemaining : engine.AwayTimeoutsRemaining;

        UpdateSubstitutesList();
        SyncVisuals();

        System.Windows.Media.CompositionTarget.Rendering += OnCompositionTargetRendering;
        ApplySpeed(1);
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (IsPaused || IsMatchOver || IsTimeoutActive) return;
        double currentTotalSeconds = _stopwatch.Elapsed.TotalSeconds;
        double deltaTime = currentTotalSeconds - _lastTotalSeconds;
        _lastTotalSeconds = currentTotalSeconds;
        double gameSecondsToAdvance = deltaTime * SpeedMultiplier;

        if (_engine.IsFullTime && !_engine.IsOvertime && !_engine.IsShootout)
        {
            _stopwatch.Stop();

            // Knockout draw → trigger extra time
            if (_engine.IsKnockout && _engine.HomeScore == _engine.AwayScore)
            {
                _engine.StartExtraTime();
                MatchPhaseLabel = "Extra Time - 1st Half";
                SyncVisuals();
                return;
            }

            IsMatchOver = true;
            IsPaused = true;
            return;
        }

        if (_engine.IsExtraTimeHalfTime)
        {
            _stopwatch.Stop();
            IsPaused = true;
            IsExtraTimeHalfTime = true;
            return;
        }

        if (_engine.IsFullTime && _engine.IsOvertime && !_engine.IsShootout)
        {
            _stopwatch.Stop();

            // Still level after ET → shootout
            if (_engine.HomeScore == _engine.AwayScore)
            {
                _engine.ResolveShootout();
                MatchPhaseLabel = "Penalty Shootout";
                SyncVisuals();
                IsShootoutActive = true;
                IsMatchOver = true;
                IsPaused = true;
            }
            else
            {
                IsMatchOver = true;
                IsPaused = true;
            }
            return;
        }

        if (_engine.IsHalfTime)
        {
            _stopwatch.Stop();
            IsPaused = true;
            IsHalfTime = true;
            return;
        }

        _engine.Tick(gameSecondsToAdvance);

        // Check AI timeouts periodically
        CheckAITimeout(true);
        CheckAITimeout(false);

        SyncVisuals();
    }

    private void CheckAITimeout(bool isHomeTeam)
    {
        // AI call only available if game is in progress, team is AI-controlled
        bool isAiTeam = isHomeTeam ? !_isUserHome : _isUserHome;
        if (!isAiTeam) return;

        int remaining = isHomeTeam ? _engine.HomeTimeoutsRemaining : _engine.AwayTimeoutsRemaining;
        if (remaining <= 0) return;

        int lastMinute = isHomeTeam ? _lastAiTimeoutMinuteHome : _lastAiTimeoutMinuteAway;
        if (_engine.GameMinute - lastMinute < 8) return; // cooldown: 8 game-minutes

        var squad = isHomeTeam ? _engine.HomeSquad : _engine.AwaySquad;
        var onCourt = squad.StartingLineup.Values
            .Where(p => p != null)
            .Select(p => p!)
            .Distinct();

        double avgEnergy = onCourt.Any()
            ? onCourt.Average(p => _engine.PlayerEnergy.GetValueOrDefault(p.Id, 100.0))
            : 100.0;

        // Trigger if very tired
        if (avgEnergy < 40.0)
        {
            TriggerTimeout(isHomeTeam);
            if (isHomeTeam) _lastAiTimeoutMinuteHome = _engine.GameMinute;
            else _lastAiTimeoutMinuteAway = _engine.GameMinute;
        }
    }

    private void TriggerTimeout(bool isHome)
    {
        _engine.RequestTimeout(isHome);
        SyncVisuals();

        var callerTeam = isHome ? _engine.HomeTeam : _engine.AwayTeam;
        TimeoutCallerLogo = callerTeam.LogoPath;
        TimeoutCallerName = callerTeam.Name;
        IsTimeoutActive = true;
        IsPaused = true;
        _stopwatch.Stop();
    }

    [RelayCommand]
    private void DismissTimeout()
    {
        IsTimeoutActive = false;
        // Stay paused after timeout — user manually resumes
    }

    private void SyncVisuals()
    {
        HomeScore = _engine.HomeScore;
        AwayScore = _engine.AwayScore;
        HomeShootoutScore = _engine.HomeShootoutScore;
        AwayShootoutScore = _engine.AwayShootoutScore;
        MatchClock = $"{_engine.GameMinute:D2}:{(int)_engine.GameSecond:D2}";
        TimeoutsRemaining = _isUserHome ? _engine.HomeTimeoutsRemaining : _engine.AwayTimeoutsRemaining;
        IsHalfTime = _engine.IsHalfTime;

        if (_engine.IsOvertime && !_engine.IsExtraTimeHalfTime)
            MatchPhaseLabel = _engine.IsShootout ? "Penalty Shootout" : "Extra Time";

        // Sync New Stats
        HomeShots = _engine.HomeShots;
        AwayShots = _engine.AwayShots;
        HomeShotsOnTarget = _engine.HomeShotsOnTarget;
        AwayShotsOnTarget = _engine.AwayShotsOnTarget;
        HomePossessionPercent = _engine.HomePossessionPercent;
        AwayPossessionPercent = 100.0 - HomePossessionPercent;

        HomeEfficiencyText = HomeShots > 0 ? $"{(double)HomeScore / HomeShots * 100:F0}%" : "0%";
        AwayEfficiencyText = AwayShots > 0 ? $"{(double)AwayScore / AwayShots * 100:F0}%" : "0%";

        OnPropertyChanged(nameof(HomeHasPossession));
        OnPropertyChanged(nameof(CurrentPhase));

        if (Math.Abs(_engine.GameSecond - _lastCardSyncGameSecond) >= 0.5)
        {
            _lastCardSyncGameSecond = _engine.GameSecond;
            
            SyncCards(_isUserHome ? _engine.HomeSquad : _engine.AwaySquad, UserStartingCards);
            SyncCards(_isUserHome ? _engine.AwaySquad : _engine.HomeSquad, OpponentStartingCards);
        }
    }

    private void SyncCards(SquadSelection squad, ObservableCollection<LivePlayerCard> targetCol)
    {
        targetCol.Clear();
        var onCourt = squad.StartingLineup.Values.Where(p => p != null).Select(p => p!).Distinct();
        foreach (var p in onCourt)
        {
            targetCol.Add(new LivePlayerCard
            {
                PlayerId = p.Id,
                Name = p.Name,
                Position = p.Position,
                ShirtNumber = p.ShirtNumber,
                EnergyPercent = (int)_engine.PlayerEnergy.GetValueOrDefault(p.Id, 100.0),
                LiveRating = CalculateLiveRating(p.Id)
            });
        }
    }

    private double CalculateLiveRating(int playerId)
    {
        if (_engine.Stats.TryGetValue(playerId, out var stat))
        {
            // Find the player to check position
            var player = _engine.HomeTeam.Players.Concat(_engine.AwayTeam.Players)
                        .FirstOrDefault(p => p.Id == playerId);

            double finalRating = 5.5;

            if (player?.Position == "GK")
            {
                int totalShotsFaced = stat.Saves + stat.GoalsAgainst;
                double savePct = totalShotsFaced > 0 ? (double)stat.Saves / totalShotsFaced : 0.25;
                // 25% save rate is baseline 6.0. 40%+ will push towards 9-10.
                finalRating = 6.0 + (savePct - 0.25) * 15.0 + (stat.Saves * 0.05);
            }
            else
            {
                // Outfield: Goals and Assists boost, misses penalize
                finalRating = 5.5 + (stat.Goals * 0.5) + (stat.Assists * 0.3) - ((stat.Shots - stat.Goals) * 0.3);
            }

            return Math.Clamp(finalRating, 3.0, 10.0);
        }
        return 6.0;
    }

    private void UpdateSubstitutesList()
    {
        var mySquad = _isUserHome ? _engine.HomeSquad : _engine.AwaySquad;
        AvailableSubstitutes = new ObservableCollection<Player>(mySquad.Substitutes);
    }

    private void ApplySpeed(int mult)
    {
        SpeedMultiplier = mult;
        IsPaused = false;
        _lastTotalSeconds = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Start();
    }

    [RelayCommand] private void TogglePause() { IsPaused = !IsPaused; if (IsPaused) _stopwatch.Stop(); else { _lastTotalSeconds = _stopwatch.Elapsed.TotalSeconds; _stopwatch.Start(); } }
    [RelayCommand] private void SetSpeed(object parameter) { if (parameter is string s && int.TryParse(s, out int mult)) ApplySpeed(mult); else if (parameter is int i) ApplySpeed(i); }

    [RelayCommand]
    private void StartSecondHalf()
    {
        if (_engine.IsHalfTime)
        {
            _engine.StartSecondHalf();
            IsHalfTime = false;
            MatchPhaseLabel = "2nd Half";
            IsPaused = false;
            _lastTotalSeconds = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Start();
        }
    }

    [RelayCommand]
    private void StartExtraTimeSecondHalf()
    {
        if (_engine.IsExtraTimeHalfTime)
        {
            _engine.StartExtraTimeSecondHalf();
            IsExtraTimeHalfTime = false;
            MatchPhaseLabel = "Extra Time - 2nd Half";
            IsPaused = false;
            _lastTotalSeconds = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Start();
        }
    }

    [RelayCommand]
    private void CallTimeout()
    {
        if (TimeoutsRemaining > 0 && !IsMatchOver && !IsHalfTime)
        {
            TriggerTimeout(_isUserHome);
        }
    }

    [RelayCommand]
    private void OpenSubstitution(LivePlayerCard card)
    {
        if (IsMatchOver) return;
        IsPaused = true;
        _stopwatch.Stop();
        PlayerBeingSubstituted = card;
        IsSubstitutionPanelOpen = true;
    }

    [RelayCommand]
    private void CloseSubstitution()
    {
        IsSubstitutionPanelOpen = false;
        PlayerBeingSubstituted = null;
        // INTENTIONALLY stays paused — user must press play manually
    }

    [RelayCommand]
    private void ConfirmSubstitute(Player subIn)
    {
        if (PlayerBeingSubstituted != null)
        {
            var mySquad = _isUserHome ? _engine.HomeSquad : _engine.AwaySquad;
            var subOut = mySquad.StartingLineup.Values
                .FirstOrDefault(x => x?.Id == PlayerBeingSubstituted.PlayerId);
            if (subOut != null)
            {
                var positionIn = mySquad.StartingLineup.FirstOrDefault(x => x.Value?.Id == subOut.Id).Key;
                if (!string.IsNullOrEmpty(positionIn)) _engine.PerformSubstitution(_isUserHome, subOut, subIn, positionIn);
            }
            UpdateSubstitutesList();
            SyncVisuals();
            // Close panel but STAY PAUSED
            IsSubstitutionPanelOpen = false;
            PlayerBeingSubstituted = null;
        }
    }

    [RelayCommand] private void ViewMatchSummary() => _onMatchEnd?.Invoke(_engine, _engine.HomeSquad, _engine.AwaySquad);

    [RelayCommand]
    private void SkipToResult()
    {
        IsPaused = true;
        _stopwatch.Stop();

        // Fast-forward through regular time
        while (!_engine.IsFullTime)
        {
            if (_engine.IsHalfTime) _engine.StartSecondHalf();
            _engine.Tick(1.0);
        }

        // Handle Extra time and shootout for knockout
        if (_engine.IsKnockout && _engine.HomeScore == _engine.AwayScore)
        {
            _engine.StartExtraTime();
            while (!_engine.IsFullTime)
            {
                if (_engine.IsExtraTimeHalfTime) _engine.StartExtraTimeSecondHalf();
                _engine.Tick(1.0);
            }
            if (_engine.HomeScore == _engine.AwayScore)
                _engine.ResolveShootout();
        }

        SyncVisuals();
        IsMatchOver = true;
    }

    public void Cleanup()
    {
        System.Windows.Media.CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _stopwatch.Stop();
    }
}
