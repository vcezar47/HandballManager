using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;
using HandballManager.ViewModels;

namespace HandballManager.Engine2D;

/// <summary>One line of the match feed beside the court.</summary>
public sealed record FeedItem(int Minute, string Text, bool IsGoal, bool IsHome, string Colour = "#FFFFFF");

/// <summary>A player as the bench panel lists her.</summary>
public sealed record BenchRow(int Id, string Name, string Position, int ShirtNumber, int Energy, bool OnCourt, bool Available);

/// <summary>
/// One candidate on the seven-metre panel: who she is, the one number that decides it — her
/// seven-metre skill, or a keeper's one-on-ones — and a line of context under her name.
/// </summary>
public sealed record SevenMetreCandidate(int Id, string Name, int Number, string Shirt, string NumberColour,
    int Score, string ScoreLabel, string Detail, string Tag, int Energy)
{
    public bool HasTag => Tag.Length > 0;
    public double EnergyShare => Energy / 100.0;
    public string EnergyColour => Energy >= 70 ? "#4CAF50" : Energy >= 50 ? "#FFB300" : "#E53935";
}

/// <summary>
/// Drives a watched match on the 2D court: steps the engine at its fixed ten ticks a second
/// whatever the frame rate, and tells the renderer how far between two ticks it is so the
/// players glide rather than hop.
/// </summary>
/// <remarks>
/// The engine decides everything; this only paces it, relays what happened, and carries the
/// manager's instructions back in. Skipping to the result is the same engine run headless on a
/// worker thread — the match the player chose not to watch is still the same match.
/// </remarks>
public partial class Match2DViewModel : BaseViewModel
{
    private readonly Action<IPlayedMatch> _onFinished;
    private readonly IFrameTicker _ticker;
    private readonly Stopwatch _clock = new();
    private readonly Random _commentaryRng = new();
    private double _lastFrame;
    private double _accumulator;
    private int _eventsSeen;
    private bool _finishing;

    public PhysicsMatch Match { get; }
    public MatchPresentation Look { get; }

    /// <summary>The side the manager is running: 0 home, 1 away.</summary>
    public int UserSide { get; }

    /// <summary>How far the renderer is between <see cref="From"/> and <see cref="To"/>, 0–1.</summary>
    public float Alpha { get; private set; }

    // ── Frames: what the court shows ───────────────────────────────────────────
    //
    // Every tick is recorded. Live, the court draws the last two; at 16× and 32× it plays back a
    // stretch around each goal at normal speed and then catches up — so the fast speeds still
    // show the moments that matter, and not only a blur between them.

    private const int RingSize = 160;
    private readonly FrameShot[] _ring = Enumerable.Range(0, RingSize).Select(_ => new FrameShot()).ToArray();
    private long _newest = -1;

    private bool _replaying;

    /// <summary>The latest frame the court has shown, which a replay may not rewind past.</summary>
    private long _shownTick;
    private long _cursor, _replayEnd;
    private double _replayAcc;

    /// <summary>The frame the court is drawn from, and the one it is moving towards.</summary>
    public FrameShot From { get; private set; } = new();
    public FrameShot To { get; private set; } = new();

    /// <summary>Showing a key moment at normal speed while the match is being played at 16× or 32×.</summary>
    public bool IsReplayingMoment => _replaying;

    private FrameShot At(long tick) => _ring[(int)(tick % RingSize)];

    private void Record()
    {
        _newest++;
        At(_newest).Capture(Match, _newest);
    }

    private static bool IsKeyMoment(EventKind k) => k is EventKind.Goal or EventKind.SevenMetreGoal or EventKind.SevenMetreAwarded
        or EventKind.RedCard or EventKind.KempaAttempt or EventKind.SevenMetreSaved;

    public string HomeTeamName => Match.Sides[0].Team.Name;
    public string AwayTeamName => Match.Sides[1].Team.Name;
    public string HomeTeamLogo => Match.Sides[0].Team.LogoPath;
    public string AwayTeamLogo => Match.Sides[1].Team.LogoPath;

    public ObservableCollection<FeedItem> Feed { get; } = [];

    /// <summary>The two side panels beside the court: on court first, then the bench.</summary>
    public ObservableCollection<SideRow> HomeRoster { get; } = [];
    public ObservableCollection<SideRow> AwayRoster { get; } = [];

    [ObservableProperty] private string _bannerColour = "#FFD54F";
    private int _bannerPriority;
    private double _lastRosterTick;
    public ObservableCollection<BenchRow> CourtPlayers { get; } = [];
    public ObservableCollection<BenchRow> BenchPlayers { get; } = [];

    [ObservableProperty] private int _homeScore;
    [ObservableProperty] private int _awayScore;
    [ObservableProperty] private string _clockText = "00:00";
    [ObservableProperty] private string _phaseText = "1st half";
    [ObservableProperty] private int _speed = 1;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isHalfTime;
    [ObservableProperty] private bool _isFinished;
    [ObservableProperty] private bool _isSkipping;
    [ObservableProperty] private string _banner = "";
    [ObservableProperty] private bool _showBanner;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeoutText))]
    private int _timeoutsLeft = 3;

    /// <summary>A time-out asked for and waiting for the ball, as the green card on the table.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeoutText))]
    private bool _timeoutRequested;

    public string TimeoutText => TimeoutRequested ? "TIME-OUT · WAITING" : $"TIME-OUT · {TimeoutsLeft}";
    [ObservableProperty] private string _homeShortHanded = "";
    [ObservableProperty] private string _awayShortHanded = "";
    [ObservableProperty] private bool _passiveWarning;

    [ObservableProperty] private bool _isChoosingTaker;
    [ObservableProperty] private bool _emptyGoal;
    public ObservableCollection<SevenMetreCandidate> TakerChoices { get; } = [];

    [ObservableProperty] private AttackTempo _tempo;
    [ObservableProperty] private AttackFocus _focus;
    [ObservableProperty] private DefensiveSystem _defenceSystem;
    [ObservableProperty] private DefensiveAggression _aggression;

    /// <summary>At 16× and 32× nobody can follow the play: the court shows it fast-forwarded.</summary>
    public bool IsFastForward => Speed >= 16;

    private float _bannerUntil;

    private readonly Func<double> _now;
    private readonly IMatchAudio? _audio;
    private bool _goalSoundThisFrame;

    [ObservableProperty] private bool _isSoundOn;

    /// <param name="now">Seconds of real time, for tests that drive the frames themselves; a stopwatch otherwise.</param>
    /// <param name="audio">The crowd and the whistle — the same sounds as the classic match screen.</param>
    /// <param name="ads">The half-time break's interstitial, as on the classic match screen.</param>
    /// <param name="preferences">Whether to stop for seven-metres, remembered between matches.</param>
    public Match2DViewModel(PhysicsMatch match, MatchPresentation look, int userSide, Action<IPlayedMatch> onFinished, IFrameTicker ticker,
        Func<double>? now = null, IMatchAudio? audio = null, IInterstitialAds? ads = null, IMatchPreferences? preferences = null)
    {
        _now = now ?? (() => _clock.Elapsed.TotalSeconds);
        _audio = audio;
        _ads = ads;
        _preferences = preferences;
        _isSoundOn = audio?.IsEnabled ?? false;
        Match = match;
        Look = look;
        UserSide = userSide;
        _onFinished = onFinished;
        _ticker = ticker;

        var t = match.Sides[userSide].Tactics;
        _asksForSevenMetres = preferences?.AskForSevenMetres ?? true;
        match.AskForSevenMetreTaker = _asksForSevenMetres;
        _autoSubstitutions = preferences?.AutoSubstitutions ?? true;
        match.UserAutoSubstitutions = _autoSubstitutions;
        _emptyGoal = t.UseEmptyGoal;
        _playForTime = t.PlayForTime;
        _tempo = t.Tempo;
        _focus = t.Focus;
        _defenceSystem = t.System;
        _aggression = t.Aggression;

        RefreshSquadLists();
        RebuildRosters();
        Record();
        Record();
        From = At(_newest - 1);
        To = At(_newest);
        Sync();
        _ticker.Tick += OnFrame;
        _clock.Start();
        _ticker.Start();
        _audio?.StartAtmosphere();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = _now();
        double real = Math.Min(0.1, now - _lastFrame); // a stalled frame does not become a burst
        _lastFrame = now;
        KeepCrowdGoing(now);
        if (IsPaused || IsSkipping || IsChoosingTaker) return;
        if ((IsHalfTime || IsFinished) && !_replaying) return;
        _goalSoundThisFrame = false;

        if (_replaying) PlayBack(real);
        else PlayLive(real);

        _shownTick = Math.Max(_shownTick, To.Tick);
        if (ShowBanner && To.Time > _bannerUntil) { ShowBanner = false; _bannerPriority = 0; }
        if (now - _lastRosterTick > 1.0) { _lastRosterTick = now; UpdateRosterEnergy(); }
        Sync();
        Frame?.Invoke();
    }

    private void PlayLive(double real)
    {
        _accumulator += real * Speed;
        int budget = 400;
        while (_accumulator >= PhysicsMatch.Dt && budget-- > 0)
        {
            int before = Match.Events.Count;
            Match.Tick();
            Record();
            _accumulator -= PhysicsMatch.Dt;

            // The manager's own time-out is something to watch, whatever the speed was.
            if (Match.TimeoutSide == UserSide && Speed > 1)
            {
                SlowForTimeout();
                _accumulator = 0;
                break;
            }

            // Fast-forwarding and something happened worth seeing: go back and show it.
            if (IsFastForward && Match.Events.Skip(before).Any(e => IsKeyMoment(e.Kind)))
            {
                _replaying = true;
                // 16×: five seconds of build-up and three of the aftermath; 32×: a quick clip.
                int before5 = Speed >= 32 ? 30 : 50, after = Speed >= 32 ? 15 : 30;
                // Never back past what the screen has already shown: two moments close together
                // play on from one to the next rather than showing the same seconds twice.
                _cursor = Math.Max(Math.Max(_newest - before5, _newest - RingSize + 20), _shownTick);
                _replayEnd = _newest + after;
                _replayAcc = 0;
                _accumulator = 0;
                break;
            }
            if (Match.AwaitingChoice) { OpenTakerChoice(); _accumulator = 0; break; }
            if (Match.IsHalfTime || Match.IsFinished) { _accumulator = 0; break; }
        }
        From = At(_newest - 1);
        To = At(_newest);
        Alpha = _replaying ? 0f : (float)(_accumulator / PhysicsMatch.Dt);
        if (_replaying) { From = At(_cursor); To = At(_cursor + 1); }

        // The feed catches up with what the court is showing, once the frame is settled — never
        // tick by tick, or a moment about to be replayed would already be written up.
        DrainEvents(From.Time);
    }

    /// <summary>A key moment at normal speed, the match running on just ahead to feed it.</summary>
    private void PlayBack(double real)
    {
        _replayAcc += real;
        while (_replayAcc >= PhysicsMatch.Dt)
        {
            _replayAcc -= PhysicsMatch.Dt;
            _cursor++;
            while (_newest < _cursor + 1 && !Match.IsHalfTime && !Match.IsFinished && !Match.AwaitingChoice)
            {
                int before = Match.Events.Count;
                Match.Tick();
                Record();
                if (Match.Events.Skip(before).Any(e => IsKeyMoment(e.Kind))) _replayEnd = Math.Max(_replayEnd, _newest + (Speed >= 32 ? 15 : 30));
            }
        }

        if (Match.TimeoutSide == UserSide) SlowForTimeout();

        long last = Math.Min(_cursor + 1, _newest);
        From = At(Math.Min(_cursor, last));
        To = At(last);
        Alpha = (float)(_replayAcc / PhysicsMatch.Dt);
        DrainEvents(From.Time);

        bool caughtUp = _cursor + 1 >= _newest && (Match.IsHalfTime || Match.IsFinished || Match.AwaitingChoice);
        if (_cursor >= _replayEnd || caughtUp)
        {
            // Back to live: the court jumps to now in the same step as the feed does.
            _replaying = false;
            From = At(_newest - 1);
            To = At(_newest);
            Alpha = 0f;
            DrainEvents(Match.Clock);
            if (Match.AwaitingChoice) OpenTakerChoice();
        }
    }

    /// <summary>Raised after every frame the match moved, for the court to redraw.</summary>
    public event Action? Frame;

    /// <summary>Feeds the commentary up to what the court is showing — never ahead of it.</summary>
    private void DrainEvents(float upTo)
    {
        while (_eventsSeen < Match.Events.Count && Match.Events[_eventsSeen].Time <= upTo + 0.01f)
        {
            var e = Match.Events[_eventsSeen++];
            if (e.Kind is EventKind.ThrowOff) continue;

            string text = Commentary.Describe(Match, e, _commentaryRng);
            if (string.IsNullOrEmpty(text)) continue;
            bool goal = e.Kind is EventKind.Goal or EventKind.SevenMetreGoal;
            string colour = EventColour(e.Kind);

            // Free throws are dozens a match: they flash on the court but stay out of the feed.
            if (e.Kind != EventKind.FreeThrow)
                Feed.Insert(0, new FeedItem(e.Minute, text, goal, e.Side == 0, colour));

            // The crowd: one roar a frame at most, however many went in during it.
            if (goal && !_goalSoundThisFrame) { _audio?.PlayGoal(); _goalSoundThisFrame = true; }
            if (e.Kind == EventKind.Timeout) _audio?.PlayTimeout();

            var (headline, priority, seconds) = BannerFor(e);
            if (headline != null && (priority >= _bannerPriority || !ShowBanner || e.Time > _bannerUntil))
            {
                Banner = headline;
                BannerColour = colour;
                ShowBanner = true;
                _bannerPriority = priority;
                _bannerUntil = e.Time + seconds;
            }
            if (e.Kind is EventKind.Substitution or EventKind.TwoMinutes or EventKind.RedCard or EventKind.SuspensionOver
                or EventKind.Injury or EventKind.EmptyGoal)
            {
                RefreshSquadLists();
                RebuildRosters();
            }
        }
    }

    private void Sync()
    {
        // The scoreboard shows what the court shows: during a replayed moment, the score then.
        HomeScore = To.HomeScore;
        AwayScore = To.AwayScore;
        float inPeriod = To.GameTime;
        int m = (int)(inPeriod / 60f), s = (int)(inPeriod % 60f);
        ClockText = $"{m:00}:{s:00}";
        PhaseText = Match.Phase switch
        {
            MatchPhase.HalfTime => "Half-time",
            MatchPhase.FullTime => Match.WentToShootout ? $"Full time — shoot-out {Match.ShootoutScore[0]}–{Match.ShootoutScore[1]}" : "Full time",
            MatchPhase.Timeout => "Time-out",
            _ => Match.Period switch { 1 => "1st half", 2 => "2nd half", _ => "Extra time" },
        };
        IsHalfTime = Match.IsHalfTime && !_replaying;
        IsFinished = Match.IsFinished && !_replaying;
        TimeoutsLeft = Match.Sides[UserSide].TimeoutsLeft;
        TimeoutRequested = Match.IsTimeoutQueued(UserSide);
        PassiveWarning = Match.PassiveWarned;
        HomeShortHanded = ShortHanded(0);
        AwayShortHanded = ShortHanded(1);
    }

    /// <summary>The colour an event is written in, in the feed and on the court.</summary>
    public static string EventColour(EventKind k) => k switch
    {
        EventKind.Goal or EventKind.SevenMetreGoal or EventKind.ShootoutGoal => "#66BB6A",
        EventKind.TwoMinutes or EventKind.RedCard => "#EF5350",
        EventKind.SevenMetreAwarded or EventKind.SevenMetreMissed or EventKind.SevenMetreSaved => "#FFA726",
        EventKind.Save or EventKind.Block => "#42A5F5",
        EventKind.Injury => "#AB47BC",
        EventKind.PassiveWarning or EventKind.PassivePlay => "#FFD54F",
        EventKind.Turnover or EventKind.Interception or EventKind.OffensiveFoul => "#FFCC80",
        EventKind.KempaAttempt or EventKind.KempaFailed => "#26C6DA",
        EventKind.Substitution or EventKind.Timeout or EventKind.SuspensionOver or EventKind.EmptyGoal or EventKind.FreeThrow => "#B0BEC5",
        _ => "#FFFFFF",
    };

    /// <summary>What goes up on the court for an event, how important it is, and for how long — or nothing.</summary>
    private (string? Text, int Priority, float Seconds) BannerFor(MatchEvent2D e)
    {
        string team = e.Side >= 0 ? Match.Sides[e.Side].Team.Name : "";
        return e.Kind switch
        {
            EventKind.Goal or EventKind.SevenMetreGoal => ($"GOAL — {e.PlayerName}", 3, 4f),
            EventKind.RedCard => ($"RED CARD — {e.PlayerName}", 3, 4f),
            EventKind.TwoMinutes => ($"2 MINUTES — {e.PlayerName} ({team})", 2, 3f),
            EventKind.SevenMetreAwarded => ($"7 METRES — {team}", 2, 3f),
            EventKind.SevenMetreSaved => ($"7 METRES SAVED — {e.PlayerName}", 2, 2.5f),
            EventKind.SevenMetreMissed => ($"7 METRES MISSED — {e.PlayerName}", 2, 2.5f),
            EventKind.Injury => ($"INJURY — {e.PlayerName}", 2, 3f),
            EventKind.Timeout => ($"TIME-OUT — {team}", 2, 3f),
            EventKind.PassiveWarning => ($"PASSIVE PLAY WARNING — {team}", 1, 2.5f),
            EventKind.PassivePlay => ($"PASSIVE PLAY — ball to the defence", 1, 2.5f),
            EventKind.KempaAttempt => ($"KEMPA TRICK!", 2, 2f),
            EventKind.EmptyGoal => ($"7 v 6 — {team} pull the keeper", 1, 2.5f),
            EventKind.OffensiveFoul => ($"OFFENSIVE FOUL — {e.OtherPlayerName}", 1, 2f),
            EventKind.FreeThrow => ($"FREE THROW — {team}", 0, 1.4f),
            _ => (null, 0, 0f),
        };
    }

    /// <summary>
    /// The side panels. Rebuilt when somebody changes places — on, off, suspended, hurt — and
    /// otherwise only their energy bars move, once a second.
    /// </summary>
    public void RebuildRosters()
    {
        for (int side = 0; side < 2; side++)
        {
            var list = side == 0 ? HomeRoster : AwayRoster;
            var s = Match.Sides[side];
            var kit = side == 0 ? Look.Kits.Home : Look.Kits.Away;
            var keeperKit = side == 0 ? Look.Kits.HomeKeeper : Look.Kits.AwayKeeper;
            list.Clear();
            foreach (var a in s.Squad
                         .OrderBy(a => a.OnCourt ? 0 : a.State == AgentState.Bench ? 1 : 2)
                         .ThenBy(a => a.IsKeeperByTrade ? 0 : 1)
                         .ThenBy(a => a.Player.ShirtNumber))
            {
                var k = a.IsKeeperByTrade ? keeperKit : kit;
                var row = new SideRow
                {
                    Id = a.Id,
                    Number = a.Player.ShirtNumber,
                    Name = string.IsNullOrWhiteSpace(a.Player.LastName) ? a.Name : a.Player.LastName,
                    Shirt = k.Shirt,
                    NumberColour = k.Number,
                };
                UpdateRow(row, a);
                list.Add(row);
            }
        }
    }

    private void UpdateRosterEnergy()
    {
        foreach (var list in new[] { HomeRoster, AwayRoster })
            foreach (var row in list)
            {
                var a = Match.Agents.FirstOrDefault(x => x.Id == row.Id);
                if (a != null) UpdateRow(row, a);
            }
    }

    private void UpdateRow(SideRow row, Agent a)
    {
        bool waiting = Match.Sides[a.Side].Changes.Any(c => c.Entering == a);
        row.Role = a.Role == PhysicsMatch.ExtraRole ? "7th" : a.OnCourt ? a.Role : a.Player.Position;
        row.Energy = (int)a.Energy;
        row.OnCourt = a.OnCourt;
        row.Available = a.State == AgentState.Bench && a.SuspendedUntil <= 0 && !waiting;
        row.Status = a.State switch
        {
            AgentState.Suspended => $"2' {(int)MathF.Max(0f, a.SuspendedUntil - Match.GameTime)}s",
            AgentState.SentOff => "RED",
            AgentState.Injured => "INJ",
            AgentState.LeavingCourt => "OFF",
            AgentState.Bench when waiting => "IN",
            _ => "",
        };
        (row.Played, row.Rating) = LiveRating(a);
    }

    /// <summary>
    /// The mark the match report will give her, on the running score — the same
    /// <see cref="MatchRating"/> the result is recorded with, so it does not jump at full time.
    /// Nobody who has not been on the court has one yet.
    /// </summary>
    private (bool Played, double Rating) LiveRating(Agent a)
    {
        if (!Match.Stats.TryGetValue(a.Id, out var stat)) return (false, 0);
        int margin = Match.Score[a.Side] - Match.Score[1 - a.Side];
        // Whoever has faced shots is in goal, as the saved report reads it.
        string? position = stat.Saves > 0 || stat.GoalsAgainst > 0 ? "GK" : a.Player.Position;
        return (true, MatchRating.For(position, stat, margin));
    }

    /// <summary>"6 v 7 · 1:24" while a side is a player down, the time left on the longest suspension.</summary>
    private string ShortHanded(int side)
    {
        var s = Match.Sides[side];
        float left = 0;
        foreach (var a in s.Squad) if (a.SuspendedUntil > Match.GameTime) left = MathF.Max(left, a.SuspendedUntil - Match.GameTime);
        if (left <= 0) return "";
        return $"−{s.Squad.Count(a => a.SuspendedUntil > Match.GameTime)}  {(int)left / 60}:{(int)left % 60:00}";
    }

    public void RefreshSquadLists()
    {
        var side = Match.Sides[UserSide];
        CourtPlayers.Clear();
        BenchPlayers.Clear();
        foreach (var a in side.Squad.OrderBy(a => a.IsKeeper ? 0 : 1).ThenBy(a => a.Player.ShirtNumber))
        {
            if (a.State == AgentState.Injured) continue;
            var row = new BenchRow(a.Id, a.Name, a.OnCourt ? (a.Role == PhysicsMatch.ExtraRole ? "7th" : a.Role) : a.Player.Position,
                a.Player.ShirtNumber, (int)a.Energy, a.OnCourt, a.State == AgentState.Bench && a.SuspendedUntil <= 0
                && side.Changes.All(c => c.Entering != a));
            (a.OnCourt ? CourtPlayers : BenchPlayers).Add(row);
        }
    }

    // ── Controls ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

    [RelayCommand]
    private void SetSpeed(string speed)
    {
        if (int.TryParse(speed, out int x)) Speed = x;
        OnPropertyChanged(nameof(IsFastForward));
    }

    private readonly IInterstitialAds? _ads;
    private readonly IMatchPreferences? _preferences;
    private bool _startingSecondHalf;

    /// <summary>
    /// The second half, after the half-time interstitial — the same break the classic match
    /// screen shows it in, and the only one mid-match.
    /// </summary>
    [RelayCommand]
    private async Task ContinueSecondHalfAsync()
    {
        if (!Match.IsHalfTime || _startingSecondHalf) return;
        _startingSecondHalf = true;
        try
        {
            if (_ads != null) await _ads.ShowIfReadyAsync();
            // Seconds of real time went by behind the ad: the match may have been left.
            if (_finishing || !Match.IsHalfTime) return;
            Match.StartSecondHalf();
            Record();
            IsHalfTime = false;
            _lastFrame = _now();
        }
        finally
        {
            _startingSecondHalf = false;
        }
    }

    /// <summary>
    /// Whether the match stops to ask about seven-metres. Remembered between matches: nine of
    /// them a match is right at 1× and far too many at 32×.
    /// </summary>
    [ObservableProperty] private bool _asksForSevenMetres = true;

    partial void OnAsksForSevenMetresChanged(bool value)
    {
        Match.AskForSevenMetreTaker = value;
        if (_preferences != null) _preferences.AskForSevenMetres = value;
    }

    /// <summary>Whether the bench rotates tired players for the manager. Remembered between matches.</summary>
    [ObservableProperty] private bool _autoSubstitutions = true;

    partial void OnAutoSubstitutionsChanged(bool value)
    {
        Match.UserAutoSubstitutions = value;
        if (_preferences != null) _preferences.AutoSubstitutions = value;
    }

    [ObservableProperty] private bool _playForTime;
    partial void OnPlayForTimeChanged(bool value) => PushTactics();

    /// <summary>
    /// The time-out button. A team may only stop the game with the ball, and the button used
    /// to do nothing at all whenever the ball was in the air, with the other side, or being
    /// replayed — which at 16× and 32× was most of the time. Now the request waits for the ball
    /// as a green card does, and a second tap withdraws it. Either way the match drops to 1×,
    /// so the players can be seen going to the bench and coming back.
    /// </summary>
    [RelayCommand]
    private void CallTimeout()
    {
        if (IsFinished || IsHalfTime || IsSkipping) return;
        bool asked = Match.ToggleTimeoutRequest(UserSide);
        if (asked)
        {
            SlowForTimeout();
            // A moment being replayed is seconds behind the match; the time-out is now.
            if (_replaying) GoLive();
            DrainEvents(Match.Clock);
        }
        Sync();
    }

    /// <summary>Down to 1× for a time-out of the manager's own.</summary>
    private void SlowForTimeout()
    {
        if (Speed != 1) SetSpeed("1");
    }

    /// <summary>Out of a replayed moment and straight to the live match.</summary>
    private void GoLive()
    {
        _replaying = false;
        From = At(_newest - 1);
        To = At(_newest);
        Alpha = 0f;
        _shownTick = Math.Max(_shownTick, _newest);
        DrainEvents(Match.Clock);
    }

    /// <summary>Sends the chosen player to the zone and the chosen substitute on when she is off.</summary>
    public bool Substitute(int outgoingId, int incomingId)
    {
        var side = Match.Sides[UserSide];
        var off = side.Squad.FirstOrDefault(a => a.Id == outgoingId);
        var on = side.Squad.FirstOrDefault(a => a.Id == incomingId);
        if (off == null || on == null) return false;
        bool ok = Match.RequestSubstitution(UserSide, off, on);
        if (ok) { RefreshSquadLists(); RebuildRosters(); }
        return ok;
    }

    /// <summary>
    /// The seven-metre panel. For a throw to the managed side: everyone on court who could take
    /// it, the best takers first. Against it: the keeper in goal, then those on the bench, with
    /// what matters one on one and how her day has gone.
    /// </summary>
    private void OpenTakerChoice()
    {
        TakerChoices.Clear();
        IsChoosingKeeper = Match.AwaitingKeeper;
        var kit = UserSide == 0 ? Look.Kits.Home : Look.Kits.Away;
        var keeperKit = UserSide == 0 ? Look.Kits.HomeKeeper : Look.Kits.AwayKeeper;

        if (IsChoosingKeeper)
        {
            var inGoal = Match.Sides[UserSide].Keeper;
            foreach (var a in Match.SevenMetreKeepers(UserSide))
            {
                string day = Match.Stats.TryGetValue(a.Id, out var st) && st.Saves + st.GoalsAgainst > 0
                    ? $" · {st.Saves} saves from {st.Saves + st.GoalsAgainst} today" : "";
                TakerChoices.Add(new SevenMetreCandidate(a.Id, a.Name, a.Player.ShirtNumber, keeperKit.Shirt, keeperKit.Number,
                    a.Player.OneOnOnes, "1 V 1", $"Reflexes {a.Player.Reflexes}{day}",
                    a == inGoal ? "IN GOAL" : "", (int)a.Energy));
            }
            ChoiceKicker = "SEVEN METRES AGAINST";
            ChoiceTitle = "Who goes in goal?";
        }
        else
        {
            bool first = true;
            foreach (var a in Match.Sides[UserSide].Court.Where(a => !a.IsKeeper).OrderByDescending(a => a.Player.SevenMeterTaking))
            {
                var k = a.IsKeeperByTrade ? keeperKit : kit;
                TakerChoices.Add(new SevenMetreCandidate(a.Id, a.Name, a.Player.ShirtNumber, k.Shirt, k.Number,
                    a.Player.SevenMeterTaking, "7M", $"{a.Player.Position} · composure {a.Player.Composure}",
                    first ? "USUAL TAKER" : "", (int)a.Energy));
                first = false;
            }
            ChoiceKicker = "SEVEN METRES";
            ChoiceTitle = "Who takes it?";
        }
        IsChoosingTaker = true;
    }

    [ObservableProperty] private bool _isChoosingKeeper;
    [ObservableProperty] private string _choiceTitle = "Who takes it?";

    /// <summary>The small line above the question: whose seven-metre it is.</summary>
    [ObservableProperty] private string _choiceKicker = "SEVEN METRES";

    /// <summary>
    /// Names the taker, or the keeper for a throw against — 0 for the usual one: the club's
    /// taker, or the keeper already in goal — and lets the throw be taken.
    /// </summary>
    public void ChooseTaker(int playerId)
    {
        if (!IsChoosingTaker) return;
        if (Match.AwaitingKeeper)
        {
            Match.ChooseSevenMetreKeeper(playerId);
            RefreshSquadLists();
            RebuildRosters();
        }
        else
        {
            int id = playerId != 0 ? playerId : TakerChoices.FirstOrDefault()?.Id ?? 0;
            Match.ChooseSevenMetreTaker(id);
        }
        IsChoosingTaker = false;
        _lastFrame = _now();
    }

    partial void OnEmptyGoalChanged(bool value) => PushTactics();

    private double _lastCrowdCheck;

    /// <summary>
    /// Every few seconds, the crowd is asked to be playing if sound is on. Asking is free when it
    /// already is; when something has stopped it — the app losing focus for a moment, a loop that
    /// never started — it comes back on its own rather than staying silent under a lit-up SOUND
    /// button until the player toggles it.
    /// </summary>
    private void KeepCrowdGoing(double now)
    {
        if (_audio == null || now - _lastCrowdCheck < 3.0) return;
        _lastCrowdCheck = now;
        if (IsSoundOn && !IsFinished && !_finishing) _audio.StartAtmosphere();
    }

    /// <summary>
    /// The page is up and turned to landscape: the crowd is started afresh, the same stop and
    /// start the SOUND button does, which is what brought it back when it was silent at kick-off.
    /// </summary>
    public void OnShown()
    {
        if (_audio == null || !IsSoundOn || IsFinished) return;
        _audio.StopAtmosphere();
        _audio.StartAtmosphere();
    }

    /// <summary>The crowd goes quiet at full time rather than playing under the result.</summary>
    partial void OnIsFinishedChanged(bool value)
    {
        if (value) _audio?.StopAtmosphere();
    }

    [RelayCommand]
    private void ToggleSound()
    {
        if (_audio == null) return;
        IsSoundOn = !IsSoundOn;
        _audio.IsEnabled = IsSoundOn;
        if (IsSoundOn && !IsFinished) _audio.StartAtmosphere();
    }

    partial void OnTempoChanged(AttackTempo value) => PushTactics();
    partial void OnFocusChanged(AttackFocus value) => PushTactics();
    partial void OnDefenceSystemChanged(DefensiveSystem value) => PushTactics();
    partial void OnAggressionChanged(DefensiveAggression value) => PushTactics();

    private void PushTactics()
    {
        var current = Match.Sides[UserSide].Tactics;
        var t = current.Clone();
        t.Tempo = Tempo;
        t.Focus = Focus;
        t.System = DefenceSystem;
        t.Aggression = Aggression;
        t.UseEmptyGoal = EmptyGoal;
        t.PlayForTime = PlayForTime;
        Match.SetTactics(UserSide, t);
    }

    /// <summary>Plays the rest of the match without drawing it, off the UI thread.</summary>
    [RelayCommand]
    private async Task SkipToResultAsync()
    {
        if (IsSkipping || IsFinished) return;
        IsSkipping = true;
        if (IsChoosingTaker) ChooseTaker(0);
        Match.AskForSevenMetreTaker = false;
        _replaying = false;
        await Task.Run(() => Match.RunToEnd());
        Record();
        From = To = At(_newest);
        DrainEvents(Match.Clock);
        Sync();
        IsSkipping = false;
    }

    [RelayCommand]
    private void Finish()
    {
        if (!Match.IsFinished || _finishing) return;
        _finishing = true;
        Cleanup();
        _onFinished(Match);
    }

    public void Cleanup()
    {
        _audio?.StopAtmosphere();
        _ticker.Tick -= OnFrame;
        _ticker.Stop();
        _clock.Stop();
    }
}
