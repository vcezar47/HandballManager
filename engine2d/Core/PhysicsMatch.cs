using System.Numerics;
using HandballManager.Models;
using HandballManager.Services;
using HandballManager.Helpers;

namespace HandballManager.Engine2D;

public sealed record MatchSetup(
    Team Home,
    Team Away,
    SquadSelection HomeSquad,
    SquadSelection AwaySquad,
    MatchTactics HomeTactics,
    MatchTactics AwayTactics,
    double HomeAdvantage = 1.0,
    bool IsKnockout = false,
    Tuning? Tuning = null,
    string VenueName = "",
    int Attendance = 0,
    int UserSide = -1);

public enum MatchPhase { Restart, Play, HalfTime, FullTime, Timeout }

public enum RestartKind { ThrowOff, FreeThrow, SevenMetre, GoalThrow, ThrowIn }

/// <summary>One club's half of the match: its players, its instructions, the end it attacks.</summary>
public sealed class TeamSide
{
    public int Index;
    public required Team Team;
    public required MatchTactics Tactics;
    public List<Agent> Squad = [];

    /// <summary>Everyone playing right now — kept as a list because the loop reads it thousands of times a match.</summary>
    public List<Agent> Court = [];

    /// <summary>The goal this side is attacking, and which way that is along the court.</summary>
    public float AttackGoalX;
    public float Dir;

    public Agent? Keeper;
    public int TimeoutsLeft = 3;

    /// <summary>
    /// The substitutions under way, any number at once: each outgoing player walks to the zone,
    /// and her replacement waits at the line until she is off.
    /// </summary>
    public List<SubChange> Changes = [];

    public bool IsChanging => Changes.Count > 0;

    public float LastSubCheck;

    public float OwnGoalX => Engine2D.Court.Length - AttackGoalX;

    public void RefreshCourt()
    {
        Court.Clear();
        Keeper = null;
        foreach (var a in Squad)
        {
            if (a.State != AgentState.OnCourt) continue;
            Court.Add(a);
            if (a.IsKeeper) Keeper = a;
        }
    }
}

/// <summary>One change through the zone: who is coming off, who is going on.</summary>
public sealed class SubChange(Agent leaving, Agent entering, bool keepsRoles = false)
{
    public Agent Leaving { get; } = leaving;
    public Agent Entering { get; } = entering;

    /// <summary>A keeper swap, whose roles were set when it started.</summary>
    public bool KeepsRoles { get; } = keepsRoles;
}

/// <summary>
/// A handball match played out on a court: fourteen agents deciding for themselves ten times a
/// second, a ball that goes where it is thrown, and a referee applying the rules to what
/// actually happens.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here rolls "does this attack score". A goal happens because a shooter found the
/// space to get the ball away, aimed past where the keeper was standing, and the keeper could
/// not get a hand across in the time the ball took to arrive. Tactics are shapes the defenders
/// hold and places the attack looks for the ball; attributes are how fast, how far and how
/// accurately each of them can do it.
/// </para>
/// <para>
/// Deterministic for a given seed, so a match can be replayed, and it runs headless as fast as
/// the CPU allows — which is how "skip to result" plays a managed club's match, and how the
/// calibration tests hold it to the quick sim's numbers.
/// </para>
/// </remarks>
public sealed partial class PhysicsMatch : IPlayedMatch
{
    public const float Dt = 0.1f;
    public const float HalfLength = 1800f;

    private readonly Random _rng;
    private readonly Tuning _t;

    public MatchSetup Setup { get; }
    public TeamSide[] Sides { get; } = new TeamSide[2];
    public List<Agent> Agents { get; } = [];
    public Ball Ball { get; } = new();

    /// <summary>Engine time: seconds simulated, never stopping. Every timer in the match runs on it.</summary>
    public float Clock { get; private set; }

    /// <summary>The match clock on the scoreboard: engine time less the stoppages for seven-metres.</summary>
    public float GameTime => Clock - _stopped;
    private float _stopped;
    public int Half { get; private set; } = 1;
    public MatchPhase Phase { get; private set; } = MatchPhase.Restart;
    public int[] Score { get; } = new int[2];

    /// <summary>The side with the right to the ball, or -1 while it is loose.</summary>
    public int Possession { get; private set; }

    public List<MatchEvent2D> Events { get; } = [];
    public Dictionary<int, MatchPlayerStat> Stats { get; } = [];

    public bool IsFinished => Phase == MatchPhase.FullTime;
    public bool IsHalfTime => Phase == MatchPhase.HalfTime;

    // ── Restart state ──────────────────────────────────────────────────────────
    public RestartKind Restart { get; private set; }
    private int _restartSide;
    private Vector2 _restartSpot;
    private Agent? _restartTaker;
    private float _restartReadyAt;

    // ── The current attack ─────────────────────────────────────────────────────
    private float _attackStartedAt;
    private float _passiveWarnAfter;
    public bool PassiveWarned { get; private set; }
    private int _passesSinceWarning;

    /// <summary>The last completed pass, for crediting assists.</summary>
    private Agent? _lastPasser, _lastReceiver;

    private int _firstThrowOffSide;

    /// <summary>The AI coach's own decision to pull the keeper, per side.</summary>
    private readonly bool[] _aiEmptyGoal = new bool[2];

    /// <summary>Everyone not sitting on a bench this tick: on court, walking off, serving time.</summary>
    private readonly List<Agent> _moving = new(20);

    public PhysicsMatch(MatchSetup setup, int seed)
    {
        Setup = setup;
        _t = setup.Tuning ?? Tuning.Default;
        _rng = new Random(seed);

        Sides[0] = BuildSide(0, setup.Home, setup.HomeSquad, setup.HomeTactics, setup.HomeAdvantage);
        Sides[1] = BuildSide(1, setup.Away, setup.AwaySquad, setup.AwayTactics, 1.0);

        // Home attack the far goal first.
        SetEnds(homeAttacksX: Court.Length);

        // The level of this match, for the court players and for the keepers apart.
        var outfield = Agents.Where(a => !a.IsKeeperByTrade).ToList();
        var keepers = Agents.Where(a => a.IsKeeperByTrade).ToList();
        float outfieldLevel = outfield.Count > 0 ? (float)outfield.Average(a => a.Player.Overall100) / 5f : Agent.AttributeMid;
        float keeperLevel = keepers.Count > 0 ? (float)keepers.Average(a => a.Player.Overall100) / 5f : Agent.AttributeMid;
        foreach (var a in Agents)
        {
            a.Level = a.IsKeeperByTrade ? keeperLevel : outfieldLevel;
            a.LevelWeight = _t.LevelWeight;
            a.RefreshPhysics();
        }

        foreach (var s in Sides)
        {
            s.RefreshCourt();
            foreach (var a in s.Court) EnsureStat(a);
            // Everybody else starts in her seat, not walking to it.
            foreach (var a in s.Squad.Where(a => a.State == AgentState.Bench)) a.Pos = BenchPoint(s, BenchSlot(s, a));
        }

        _firstThrowOffSide = _rng.Next(2);
        PlaceForThrowOff(_firstThrowOffSide);
    }

    private TeamSide BuildSide(int index, Team team, SquadSelection squad, MatchTactics tactics, double homeAdvantage)
    {
        var side = new TeamSide { Index = index, Team = team, Tactics = tactics };

        // The crowd, the dressing room and the day's legs, as one multiplier per player — the
        // same factors the quick sim applies to a whole side, applied here to each body.
        float chemistry = (float)TeamIntangibles.ChemistryFactor(team.Chemistry);
        // The crowd lifts the whole side, but spread over every attribute of every player it
        // compounds, so the quick sim's home advantage is taken at seventy per cent strength here.
        float crowd = (float)Math.Clamp(1.0 + (homeAdvantage - 1.0) * 0.7, 0.9, 1.15);

        // Nobody plays to exactly their level every time. A side has its day or does not, a
        // keeper more than anyone — and without that the better team would win every match.
        float teamDay = 1f + _t.TeamDayForm * Gauss();

        // The side between the ears, and the man in charge of it — what the classic engine reads
        // from Teamwork, Leadership and Concentration, and from the manager's Motivation and
        // Discipline. Taken at half strength (the square root): on every attribute of every
        // player, attack and defence alike, a multiplier compounds as the crowd's does.
        var outfield = squad.StartingLineup.Where(kv => kv.Key != "GK" && kv.Value != null).Select(kv => kv.Value!).ToList();
        float mind = (float)Math.Sqrt(TeamIntangibles.Cohesion(outfield) * TeamIntangibles.PossessionSecurity(outfield)
                                      * ManagerEffects.AttackDrive(team.Manager) * ManagerEffects.DefensiveShape(team.Manager));

        foreach (var (role, player) in squad.StartingLineup)
        {
            if (player == null) continue;
            var a = new Agent(player, index, role) { State = AgentState.OnCourt };
            side.Squad.Add(a);
        }
        foreach (var player in squad.Substitutes)
        {
            if (side.Squad.Any(a => a.Id == player.Id)) continue;
            side.Squad.Add(new Agent(player, index, player.Position) { State = AgentState.Bench });
        }
        foreach (var a in side.Squad)
        {
            float day = 1f + (a.IsKeeper ? _t.KeeperDayForm : _t.PlayerDayForm) * Gauss();
            a.Spread = a.IsKeeperByTrade ? 1f : _t.AttributeSpread;
            a.Form = (float)(ConditionService.PerformanceFactor(a.Player) * MoraleService.PerformanceFactor(a.Player))
                     * chemistry * crowd * mind * teamDay * Math.Clamp(day, 0.75f, 1.25f);
            a.RefreshPhysics();
            Agents.Add(a);
        }
        return side;
    }

    private void SetEnds(float homeAttacksX)
    {
        Sides[0].AttackGoalX = homeAttacksX;
        Sides[0].Dir = homeAttacksX > Court.CentreX ? 1f : -1f;
        Sides[1].AttackGoalX = Court.Length - homeAttacksX;
        Sides[1].Dir = -Sides[0].Dir;
    }

    public TeamSide Opp(TeamSide s) => Sides[1 - s.Index];

    /// <summary>A world position in the attacking frame of <paramref name="attacking"/>.</summary>
    public static (float U, float W) Frame(TeamSide attacking, Vector2 p) => Court.ToFrame(attacking.AttackGoalX, attacking.Dir, p);

    public static Vector2 World(TeamSide attacking, float u, float w) => Court.ToWorld(attacking.AttackGoalX, attacking.Dir, u, w);

    // ── Running the match ──────────────────────────────────────────────────────

    /// <summary>Plays the whole match without stopping, half-time included.</summary>
    /// <summary>
    /// Whether the bench rotates tired players on the manager's side as it does on the other.
    /// Off, only the changes the manager makes happen — an injury or a keeper sent off is still
    /// covered, because a side cannot play on without the player.
    /// </summary>
    public bool UserAutoSubstitutions { get; set; } = true;

    public void RunToEnd()
    {
        while (!IsFinished)
        {
            if (IsHalfTime) StartSecondHalf();
            Tick();
        }
    }

    /// <summary>Advances the match by <paramref name="seconds"/> of game time, in fixed steps.</summary>
    public void Advance(float seconds)
    {
        int steps = Math.Max(1, (int)MathF.Round(seconds / Dt));
        for (int i = 0; i < steps && Phase is MatchPhase.Play or MatchPhase.Restart; i++) Tick();
    }

    public void Tick()
    {
        if (Phase is MatchPhase.HalfTime or MatchPhase.FullTime) return;
        Steps++;

        foreach (var a in Agents) a.PrevPos = a.Pos;
        Ball.PrevPos = Ball.Pos;
        Ball.PrevHeight = Ball.Height;

        // A team time-out stops the clock. The players go to their bench; the match resumes
        // with a free throw where the ball was.
        if (Phase == MatchPhase.Timeout)
        {
            Huddle();
            return;
        }

        // A time-out asked for while the ball was not the team's to stop with is granted the
        // first moment it is — the green card waits on the timekeeper's table.
        for (int i = 0; i < 2; i++)
            if (_timeoutQueued[i] && RequestTimeout(i)) return;

        Clock += Dt;
        _tick++;
        // The referees stop the clock while a seven-metre is set up, as they do for a time-out
        // (which never gets this far). Everything that moves keeps to Clock; the scoreboard,
        // the halves and the minutes in the report keep to GameTime.
        if (Phase == MatchPhase.Restart && Restart == RestartKind.SevenMetre) _stopped += Dt;
        if (GameTime >= PeriodEnd) { EndPeriod(); return; }

        UpdateSuspensions();
        UpdateSubstitutions();

        _moving.Clear();
        foreach (var a in Agents)
        {
            if (a.State == AgentState.Bench) continue;
            a.RefreshPhysics();
            _moving.Add(a);
        }

        if (Phase == MatchPhase.Restart) UpdateRestart();
        else if (Ball.State == BallState.Held && Ball.Holder != null) ThinkCarrier(Ball.Holder);
        foreach (var s in Sides)
        {
            UpdateEmptyGoal(s);
            PlanKeeper(s);
            if (Possession == s.Index) PlanAttack(s); else PlanDefence(s);
        }
        PlanLooseBallChase();
        PlanOffCourt();

        MoveAgents();
        MoveReferees();
        UpdateBall();

        if (Phase == MatchPhase.Play)
        {
            UpdateContact();
            UpdatePassivePlay();
        }

        UpdateEnergy();
        RollInjuries();
        Coach(Sides[0]);
        Coach(Sides[1]);
        if (_tick % 300 == 0) UpdateSpirit();
        if (Clock - Sides[0].LastSubCheck > 10f) ManageBench(Sides[0]);
        if (Clock - Sides[1].LastSubCheck > 10f) ManageBench(Sides[1]);
    }

    // ── Periods ────────────────────────────────────────────────────────────────
    //
    // Two halves of thirty minutes. A knockout tie level after sixty goes to extra time — two
    // periods of five minutes, and if it is still level, two more — and then to seven-metre
    // throws, which is the IHF's order of things.

    /// <summary>1–2 regulation, 3–4 the first extra time, 5–6 the second.</summary>
    public int Period { get; private set; } = 1;
    public bool IsExtraTime => Period > 2;
    private float PeriodEnd => Period switch { 1 => 1800f, 2 => 3600f, 3 => 3900f, 4 => 4200f, 5 => 4500f, _ => 4800f };
    private bool Level => Score[0] == Score[1];

    private void EndPeriod()
    {
        Clock = PeriodEnd + _stopped;
        switch (Period)
        {
            case 1:
                Phase = MatchPhase.HalfTime;
                Emit(EventKind.HalfTime, -1, null);
                return;
            case 2 or 4 when Setup.IsKnockout && Level:
                Emit(EventKind.ExtraTime, -1, null);
                NextPeriod(Period + 1);
                return;
            case 3 or 5:
                NextPeriod(Period + 1);
                return;
            case 6 when Level:
                Shootout();
                EndMatch();
                return;
            default:
                EndMatch();
                return;
        }
    }

    private void NextPeriod(int period)
    {
        Period = period;
        SetEnds(homeAttacksX: Court.Length - Sides[0].AttackGoalX);
        foreach (var a in Agents) a.Energy = MathF.Min(100f, a.Energy + 3f);
        PlaceForThrowOff(period % 2 == 1 ? _firstThrowOffSide : 1 - _firstThrowOffSide);
    }

    public void StartSecondHalf()
    {
        if (Phase != MatchPhase.HalfTime) return;
        Half = 2;
        Period = 2;
        SetEnds(homeAttacksX: Court.Length - Sides[0].AttackGoalX);

        // Everybody gets a breather: half-time is fifteen minutes on a bench.
        foreach (var a in Agents) a.Energy = MathF.Min(100f, a.Energy + _t.HalfTimeRecovery);

        PlaceForThrowOff(1 - _firstThrowOffSide);
    }

    private void EndMatch()
    {
        Phase = MatchPhase.FullTime;
        Ball.State = BallState.Dead;
        Emit(EventKind.FullTime, -1, null);
    }

    // ── Seven-metre shootout ───────────────────────────────────────────────────

    public int[] ShootoutScore { get; } = new int[2];
    public bool WentToShootout { get; private set; }

    /// <summary>
    /// Five throws each, alternating, then sudden death — taken by the best takers still allowed
    /// to play, against the keeper in goal, through the same shot model as a penalty in the match.
    /// </summary>
    private void Shootout()
    {
        WentToShootout = true;
        var takers = Sides.Select(s => s.Squad
                .Where(a => a.State is AgentState.OnCourt or AgentState.Bench && !a.IsKeeper)
                .OrderByDescending(a => a.Player.SevenMeterTaking).ToList())
            .ToArray();
        if (takers[0].Count == 0 || takers[1].Count == 0) { ShootoutScore[0] = 1; return; }

        for (int round = 0; round < 40; round++)
        {
            for (int side = 0; side < 2; side++)
            {
                var s = Sides[side];
                var taker = takers[side][round % takers[side].Count];
                var keeper = Opp(s).Keeper;
                taker.Pos = World(s, Court.SevenMetreMark, 0f);
                if (keeper != null) keeper.Pos = World(s, 1.0f, 0f);
                var outcome = ResolveShot(taker, sevenMetre: true, out _, out _, out _);
                if (outcome == ShotOutcome.Goal) ShootoutScore[side]++;
                Emit(outcome == ShotOutcome.Goal ? EventKind.ShootoutGoal : EventKind.ShootoutMiss, side, taker, keeper);
            }

            int left = Math.Max(0, 4 - round);
            if (round >= 4 && ShootoutScore[0] != ShootoutScore[1]) return;
            if (round < 4 && Math.Abs(ShootoutScore[0] - ShootoutScore[1]) > left) return;
        }
        ShootoutScore[0]++;
    }

    // ── What the season needs back ─────────────────────────────────────────────

    MatchRating.Scale IPlayedMatch.RatingScale => MatchRating.Scale.Physics2D;
    Team IPlayedMatch.HomeTeam => Sides[0].Team;
    Team IPlayedMatch.AwayTeam => Sides[1].Team;
    int IPlayedMatch.HomeScore => Score[0];
    int IPlayedMatch.AwayScore => Score[1];
    int IPlayedMatch.HomeShootoutScore => ShootoutScore[0];
    int IPlayedMatch.AwayShootoutScore => ShootoutScore[1];
    string IPlayedMatch.VenueName => Setup.VenueName;
    int IPlayedMatch.Attendance => Setup.Attendance;
    IReadOnlyDictionary<int, MatchPlayerStat> IPlayedMatch.Stats => Stats;
    IReadOnlyList<Injury> IPlayedMatch.MatchInjuries => MatchInjuries;

    /// <summary>Injuries picked up during the match, filed by the season when the result is recorded.</summary>
    public List<Injury> MatchInjuries { get; } = [];

    IReadOnlyDictionary<int, double> IPlayedMatch.CourtMinutes
        => Agents.Where(a => a.CourtSeconds > 0).ToDictionary(a => a.Id, a => (double)a.CourtSeconds / 60.0);

    IEnumerable<LiveMatchEvent> IPlayedMatch.EventLog
    {
        get
        {
            // The same seed every time it is read, so the feed and the stored report agree.
            var rng = new Random(Events.Count);
            foreach (var e in Events)
            {
                var live = Commentary.ToLiveEvent(this, e, rng);
                if (live != null) yield return live;
            }
            if (WentToShootout)
                yield return new LiveMatchEvent { Minute = 70, EventType = "Shootout", PlayerName = "Penalty Shootout" };
        }
    }

    // ── Restarts ───────────────────────────────────────────────────────────────

    private void PlaceForThrowOff(int side)
    {
        // Line both teams up the way they would walk out: the throwing side in its own half,
        // the other in its defensive shape.
        foreach (var s in Sides)
        {
            foreach (var a in s.Court)
            {
                var (u, w) = AttackSlot(s, a, forThrowOff: true);
                if (s.Index != side) (u, w) = DefenceHome(s, a);
                a.Pos = s.Index == side ? World(s, u, w) : World(Opp(s), u, w);
                a.Vel = Vector2.Zero;
            }
        }
        var taker = Sides[side].Court.FirstOrDefault(a => a.Role == "CB")
                    ?? Sides[side].Court.FirstOrDefault(a => !a.IsKeeper)
                    ?? Sides[side].Court.First();
        taker.Pos = new Vector2(Court.CentreX, Court.CentreY);
        BeginRestart(RestartKind.ThrowOff, side, new Vector2(Court.CentreX, Court.CentreY), taker, 1.5f, newAttack: true);
    }

    private void BeginRestart(RestartKind kind, int side, Vector2 spot, Agent taker, float delay, bool newAttack)
    {
        Phase = MatchPhase.Restart;
        Restart = kind;
        _throwOffStage = ThrowOffStage.Ready;
        FastThrowOff = false;

        // Throw-offs at the start of a period and seven-metres wait for everyone; free throws,
        // throw-ins and goal throws are taken as soon as they are ready.
        _restartNeedsSet = kind is RestartKind.ThrowOff or RestartKind.SevenMetre;
        _restartSide = side;
        _restartSpot = Court.Clamp(spot, 0.2f);
        _restartTaker = taker;
        _restartReadyAt = Clock + delay;

        foreach (var x in Agents) { if (x.JumpUntil > 0f) { x.JumpUntil = 0f; x.Airborne = false; } x.DiveUntil = 0f; }
        Ball.State = BallState.Dead;
        Ball.Holder = taker;
        Ball.Pos = _restartSpot;
        Ball.Height = 1.1f;
        Ball.Interceptor = null;
        Ball.Receiver = null;

        if (newAttack || Possession != side) NewAttack(side);
        Possession = side;
    }

    /// <summary>Every possession of the match: who, how long, how many passes, how it ended.</summary>
    public List<AttackRecord> Attacks { get; } = [];
    private AttackRecord? _attack;

    private void NewAttack(int side)
    {
        if (_attack != null) _attack.End = Clock;
        _attack = new AttackRecord { Side = side, Start = Clock, StartedBy = Phase == MatchPhase.Restart ? Restart.ToString() : "Play" };
        Attacks.Add(_attack);
        Possession = side;
        _attackStartedAt = Clock;
        PassiveWarned = false;
        _passesSinceWarning = 0;
        _passiveWarnAfter = _t.PassiveWarningAfter + (float)(_rng.NextDouble() * 16 - 8)
                            - (Sides[side].Tactics.Tempo == AttackTempo.Controlled ? 6f : 0f);
        _lastPasser = _lastReceiver = null;
    }

    // ── The throw-off after a goal ─────────────────────────────────────────────
    //
    // The rule is simple — the side that conceded restarts from the centre — but it is not
    // instant. The keeper fetches the ball out of the net and throws it out to the centre, and
    // then the side chooses: take it at once, while the team that scored is still celebrating
    // its way back, or walk it up, get set and make changes. Tempo, the score and how
    // disorganised the other lot are decide which.

    private enum ThrowOffStage { Ready, Retrieving, BallToCentre }
    private ThrowOffStage _throwOffStage;
    private Agent? _retriever;
    private float _stageEndsAt;

    /// <summary>Whether this throw-off is being taken quickly — for the renderer and the feed.</summary>
    public bool FastThrowOff { get; private set; }

    private void BeginThrowOffAfterGoal(int side, Vector2 ballInNet)
    {
        var s = Sides[side];
        var taker = s.Court.FirstOrDefault(x => x.Role == "CB") ?? s.Court.FirstOrDefault(x => !x.IsKeeper);
        if (taker == null) { AwardGoalThrow(side); return; }

        var centre = new Vector2(Court.CentreX, Court.CentreY);
        BeginRestart(RestartKind.ThrowOff, side, centre, taker, 0f, newAttack: true);

        _retriever = s.Keeper;
        Ball.Holder = null;
        Ball.Pos = Court.Clamp(ballInNet, 0.1f);
        Ball.Height = 0.3f;
        _throwOffStage = _retriever != null ? ThrowOffStage.Retrieving : ThrowOffStage.BallToCentre;

        // A side going quickly has the ball out of the net in an instant; one that is not, in its own time.
        _fastIntent = FastThrowOffIntent(s);
        _stageEndsAt = Clock + (_fastIntent ? 0.5f + 0.3f * Rand() : 1.2f + 0.8f * Rand());
        Ball.From = Ball.Pos;
    }

    /// <summary>Takes the throw-off quickly or not: a side that likes to run, or is chasing, goes at once.</summary>
    /// <summary>
    /// Whether the side means to take it quickly, decided as the ball goes in: how it likes to
    /// play, the score and the clock, and whether its legs need a change first.
    /// </summary>
    private bool FastThrowOffIntent(TeamSide s)
    {
        var opp = Opp(s);
        float want = s.Tactics.Tempo switch
        {
            AttackTempo.Counter => _t.FastThrowOffCounter,
            AttackTempo.Controlled => _t.FastThrowOffControlled,
            _ => _t.FastThrowOffBalanced,
        };
        int lead = Score[s.Index] - Score[opp.Index];
        bool late = GameTime > 2 * HalfLength - 600f;
        if (late && lead < 0) want += 0.35f;
        if (late && lead >= 4) want = 0.05f;
        if (s.Tactics.PlayForTime) want = 0f;
        bool tired = s.Court.Any(a => !a.IsKeeper && a.Energy < _t.RotateBelow - 4f)
                     && s.Squad.Any(b => b.State == AgentState.Bench && !b.IsKeeper && b.Energy > _t.FreshAbove);
        if (tired) want *= 0.6f;
        return Rand() < want;
    }

    /// <summary>The scoring side's players already back in their own defensive half and set.</summary>
    private int OpponentsBack(TeamSide throwing)
    {
        int n = 0;
        foreach (var d in Opp(throwing).Court)
        {
            if (d.IsKeeper) continue;
            var (u, _) = Frame(throwing, d.Pos);
            if (u < 11f) n++;
        }
        return n;
    }

    private bool _fastIntent;

    /// <summary>How the throw-offs after goals were taken this match.</summary>
    public int FastThrowOffs { get; private set; }
    public int SlowThrowOffs { get; private set; }

    /// <summary>
    /// The keeper with the ball in the net, then the ball on its way out to the centre. Returns
    /// true while that is still going on.
    /// </summary>
    private bool UpdateThrowOffLeadIn(Agent taker)
    {
        switch (_throwOffStage)
        {
            case ThrowOffStage.Retrieving:
                if (_retriever != null && _retriever.OnCourt)
                {
                    _retriever.Target = Ball.Pos;
                    _retriever.DesiredSpeed = 3f;
                }
                if (Clock < _stageEndsAt) return true;
                _throwOffStage = ThrowOffStage.BallToCentre;
                Ball.From = _retriever?.Pos ?? Ball.Pos;
                Ball.Elapsed = 0f;
                Ball.Duration = MathF.Max(0.4f, Vec.Dist(Ball.From, taker.Pos) / 15f);
                return true;

            case ThrowOffStage.BallToCentre:
                // The throw out: no one contests it, it is dead until the whistle.
                Ball.Elapsed += Dt;
                float t = Ball.Progress;
                Ball.Pos = Vec.Lerp(Ball.From, taker.Pos, t);
                Ball.Height = 1.2f + 1.5f * DetMath.Sin(MathF.PI * t);
                if (t < 1f) return true;

                _throwOffStage = ThrowOffStage.Ready;
                Ball.Holder = taker;

                // Now the call: fast if the other lot are still coming back, or if this side
                // always goes; slow if they are set, and the side walks it up and makes its changes.
                var thrower = Sides[_restartSide];
                int back = OpponentsBack(thrower);
                FastThrowOff = _fastIntent && (back < 3 || thrower.Tactics.Tempo == AttackTempo.Counter && back < 4);
                if (FastThrowOff) FastThrowOffs++; else SlowThrowOffs++;
                _restartNeedsSet = !FastThrowOff;
                _restartReadyAt = Clock + (FastThrowOff ? 0.1f : 2.5f + 2f * Rand());
                if (!FastThrowOff) ManageBench(thrower, stoppage: true);
                return false;
        }
        return false;
    }

    private void UpdateRestart()
    {
        var taker = _restartTaker;
        if (taker == null || !taker.OnCourt)
        {
            // She was sent off or substituted while the throw was being set up.
            var side = Sides[_restartSide];
            taker = side.Court.Where(a => !a.IsKeeper || Restart == RestartKind.GoalThrow)
                .OrderBy(a => Vec.DistSq(a.Pos, _restartSpot)).FirstOrDefault();
            if (taker == null) return;
            _restartTaker = taker;
            Ball.Holder = taker;
        }

        taker.Target = _restartSpot;
        taker.DesiredSpeed = taker.MaxSpeed;

        if (Restart == RestartKind.ThrowOff && _throwOffStage != ThrowOffStage.Ready)
        {
            UpdateThrowOffLeadIn(taker);
            return;
        }
        Ball.Pos = Ball.Holder == taker && Restart == RestartKind.ThrowOff ? taker.Pos : _restartSpot;

        bool inPlace = Vec.Dist(taker.Pos, _restartSpot) < (Restart == RestartKind.ThrowOff && FastThrowOff ? 2f : 0.6f);
        if (AwaitingTaker || AwaitingKeeper) return;

        // Nothing may hold a restart for ever. If the taker cannot get to her spot — it is
        // somewhere she may not stand, or something keeps sending her elsewhere — she is put on
        // it: a stalled restart froze the whole match while the clock ran on.
        if (!inPlace && Clock > _restartReadyAt + 6f)
        {
            taker.Pos = _restartSpot;
            taker.Vel = Vector2.Zero;
            inPlace = true;
        }
        if (!inPlace || Clock < _restartReadyAt) return;
        if (Restart == RestartKind.SevenMetre && SevenMetreKeeperOnHerWay()) return;

        // The referees do not whistle with a keeper still walking back from the bench: both
        // goals manned and everybody near their place, or long enough that they have to go.
        if (_restartNeedsSet && !EveryoneSet(taker) && Clock < _restartReadyAt + 8f) return;

        taker.Pos = _restartSpot;
        if (Restart == RestartKind.SevenMetre)
        {
            TakeSevenMetre(taker);
            return;
        }

        Phase = MatchPhase.Play;
        Ball.State = BallState.Held;
        Ball.Holder = taker;
        Ball.LastTouch = taker;
        taker.NextDecisionAt = Clock + 0.1f;
        taker.HasBallSince = Clock;
        taker.ReadSeed = _rng.Next();
        taker.GotBallBy = "restart";
    }

    /// <summary>This restart waits until both keepers are back and everybody is near their place.</summary>
    private bool _restartNeedsSet;

    private bool EveryoneSet(Agent taker)
    {
        foreach (var s in Sides)
        {
            foreach (var a in s.Court)
            {
                if (a == taker) continue;
                if (a.IsKeeper)
                {
                    var (u, w) = Frame(Opp(s), a.Pos);
                    if (u > 4f || MathF.Abs(w) > 3.5f) return false;
                }
                else if (Vec.Dist(a.Pos, a.Target) > 3f) return false;
            }
        }
        return true;
    }

    /// <summary>A free throw from where the foul was, pushed back to the nine metres if it was inside.</summary>
    private void AwardFreeThrow(int side, Vector2 where, Agent? taker)
    {
        var s = Sides[side];
        var (u, w) = Frame(s, where);
        (u, w) = Court.PushOutTo(u, w, Court.FreeThrowRadius);
        var spot = World(s, u, w);
        taker ??= s.Court.Where(a => !a.IsKeeper).OrderBy(a => Vec.DistSq(a.Pos, spot)).FirstOrDefault();
        if (taker == null) return;
        BeginRestart(RestartKind.FreeThrow, side, spot, taker, 0.8f + (float)_rng.NextDouble() * 1.2f, newAttack: false);
    }

    private void AwardGoalThrow(int side)
    {
        var s = Sides[side];
        var keeper = s.Keeper;
        var spot = World(Opp(s), 2.5f, 0f);
        if (keeper == null)
        {
            // The keeper is off for a seventh attacker: a court player takes it, and she may not
            // stand in the goal area — so from just outside it. Handing her a spot inside the
            // area left her at the line for the rest of the match, with the clock running.
            keeper = s.Court.OrderBy(a => Vec.DistSq(a.Pos, World(Opp(s), 0f, 0f))).FirstOrDefault();
            spot = World(Opp(s), Court.GoalAreaRadius + 0.3f, 0f);
        }
        if (keeper == null) return;
        BeginRestart(RestartKind.GoalThrow, side, spot, keeper, 0.6f, newAttack: true);
    }

    // ── Instructions from the bench ────────────────────────────────────────────

    /// <summary>New instructions for one side, from the next decision on.</summary>
    public void SetTactics(int side, MatchTactics tactics)
    {
        var s = Sides[side];
        s.Tactics = tactics;
    }

    private float _timeoutElapsed, _huddledAt;
    private int _timeoutSide = -1;
    private readonly bool[] _timeoutQueued = new bool[2];

    /// <summary>The side whose time-out is being taken, or -1.</summary>
    public int TimeoutSide => _timeoutSide;

    /// <summary>Whether a side has asked for a time-out it has not been given yet.</summary>
    public bool IsTimeoutQueued(int side) => _timeoutQueued[side];

    /// <summary>
    /// The manager's green card: a time-out now if the ball is the team's, or else the moment
    /// it is. Asking again withdraws it. Returns whether one is now taken or waiting.
    /// </summary>
    public bool ToggleTimeoutRequest(int side)
    {
        if (_timeoutQueued[side]) { _timeoutQueued[side] = false; return false; }
        if (Sides[side].TimeoutsLeft <= 0 || IsFinished || _timeoutSide == side) return false;
        if (RequestTimeout(side)) return true;
        _timeoutQueued[side] = true;
        return true;
    }

    /// <summary>
    /// A team time-out: allowed only with the ball, three a match. The clock stops, the players
    /// walk to their bench, and the match restarts with a free throw where it was.
    /// </summary>
    public bool RequestTimeout(int side)
    {
        var s = Sides[side];
        if (s.TimeoutsLeft <= 0 || Possession != side || Phase != MatchPhase.Play || Ball.State != BallState.Held) return false;
        s.TimeoutsLeft--;
        _timeoutQueued[side] = false;
        // A breather for the side that called it, as in the classic engine.
        foreach (var a in s.Court) a.Energy = MathF.Min(100f, a.Energy + _t.TimeoutRecovery);
        _timeoutSide = side;
        _timeoutSpot = Ball.Pos;
        _timeoutTaker = Ball.Holder;
        _timeoutElapsed = 0f;
        _huddledAt = -1f;
        Phase = MatchPhase.Timeout;
        Ball.State = BallState.Dead;
        Emit(EventKind.Timeout, side, null);
        return true;
    }

    /// <summary>
    /// How long the players stand in their huddle once everyone has reached it, in simulated
    /// seconds. The minute of a real time-out is dead time — nothing moves the clock — so it
    /// is shortened to the jog to the bench, a moment together, and the way back.
    /// </summary>
    public const float TimeoutHuddle = 3f;

    /// <summary>The longest the jog to the bench may take before the huddle starts regardless.</summary>
    private const float TimeoutGatherLimit = 12f;

    /// <summary>Radius of the ring each side's players stand in, in metres.</summary>
    private const float HuddleRadius = 1.5f;

    /// <summary>
    /// One step of a time-out. Each side jogs to the touchline in front of its own bench and
    /// stands in a ring there — a huddle rather than seven players on one spot — and once
    /// everyone has arrived it holds for <see cref="TimeoutHuddle"/> seconds. The clock does
    /// not move, so nothing here touches the result; it is only what the court shows.
    /// </summary>
    private void Huddle()
    {
        _timeoutElapsed += Dt;
        bool gathered = true;
        foreach (var s in Sides)
        {
            var group = _moving.Where(a => a.Side == s.Index).ToList();
            float outward = s.OwnGoalX < Court.CentreX ? -1f : 1f;
            var centre = new Vector2(Court.CentreX + outward * 7f, 2.3f);
            for (int k = 0; k < group.Count; k++)
            {
                float angle = MathF.PI * 2f * k / Math.Max(1, group.Count);
                var a = group[k];
                a.Target = centre + new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * HuddleRadius;
                a.DesiredSpeed = 4.2f;
                if (Vec.Dist(a.Pos, a.Target) > 0.7f) gathered = false;
            }
        }
        MoveAgents();

        if (gathered && _huddledAt < 0f) _huddledAt = _timeoutElapsed;
        bool held = _huddledAt >= 0f && _timeoutElapsed - _huddledAt >= TimeoutHuddle;
        if (held || _timeoutElapsed >= TimeoutGatherLimit + TimeoutHuddle) EndTimeout();
    }

    private Vector2 _timeoutSpot;
    private Agent? _timeoutTaker;

    private void EndTimeout()
    {
        Phase = MatchPhase.Play;
        AwardFreeThrow(_timeoutSide, _timeoutSpot, _timeoutTaker != null && _timeoutTaker.OnCourt ? _timeoutTaker : null);
        _restartNeedsSet = true;
        _timeoutSide = -1;
    }

    // ── Clock-driven rules: suspensions ────────────────────────────────────────

    private void Suspend(Agent offender)
    {
        offender.Suspensions++;
        var stat = EnsureStat(offender);
        stat.TwoMinutes++;

        if (offender.Suspensions >= 3)
        {
            stat.RedCards = 1;
            Emit(EventKind.RedCard, offender.Side, offender);
            offender.State = AgentState.SentOff;
        }
        else
        {
            Emit(EventKind.TwoMinutes, offender.Side, offender);
            offender.State = AgentState.Suspended;
        }

        // Two minutes a player short, whether or not she can ever come back.
        offender.SuspendedUntil = GameTime + 120f; // two minutes on the match clock
        _lastCoachLook[offender.Side] = -999f; // the bench reacts to being a player down straight away
        if (Ball.Holder == offender) Ball.Holder = null;
        var side = Sides[offender.Side];
        side.Changes.RemoveAll(c => c.Leaving == offender);
        side.RefreshCourt();
    }

    private void UpdateSuspensions()
    {
        foreach (var s in Sides)
        {
            foreach (var a in s.Squad)
            {
                if (a.SuspendedUntil <= 0 || GameTime < a.SuspendedUntil) continue;
                a.SuspendedUntil = 0;

                var returning = a;
                if (a.State == AgentState.SentOff)
                {
                    // The side is back to full strength, with somebody else.
                    returning = s.Squad.Where(b => b.State == AgentState.Bench && !b.IsKeeper && s.Changes.All(c => c.Entering != b))
                        .OrderByDescending(b => b.Role == a.Role).ThenByDescending(b => b.Energy)
                        .FirstOrDefault()!;
                    if (returning == null) continue;
                    returning.Role = a.Role;
                }
                returning.State = AgentState.OnCourt;
                returning.Pos = SubPoint(s);
                EnsureStat(returning);
                Emit(EventKind.SuspensionOver, s.Index, returning);
                s.RefreshCourt();
            }
        }
    }

    // ── Substitutions, through the zone ────────────────────────────────────────

    /// <summary>Where a side's players leave and join the court: its half of the substitution zone.</summary>
    public Vector2 SubPoint(TeamSide s)
        => new(Court.CentreX + (s.OwnGoalX < Court.CentreX ? -2.2f : 2.2f), 0.05f);

    /// <summary>The bench: off the court, behind that side's zone.</summary>
    public Vector2 BenchPoint(TeamSide s, int slot)
        => new(Court.CentreX + (s.OwnGoalX < Court.CentreX ? -3.2f - slot * 1.0f : 3.2f + slot * 1.0f), -1.3f);

    /// <summary>
    /// Each player's own seat, by her place in the squad, so that one of them getting up does
    /// not shuffle the rest along the bench.
    /// </summary>
    private static int BenchSlot(TeamSide s, Agent a) => s.Squad.IndexOf(a);

    /// <summary>Where a replacement stands at the line, waiting for the player she replaces.</summary>
    private Vector2 WaitingSpot(TeamSide s, int queue)
    {
        var p = SubPoint(s);
        float outward = s.OwnGoalX < Court.CentreX ? -1f : 1f;
        return new Vector2(p.X + outward * (0.6f + 0.8f * queue), -0.45f);
    }

    /// <summary>
    /// Sends <paramref name="outgoing"/> to the zone; <paramref name="incoming"/> comes on when she
    /// is off. The one call for the AI, the player's manager and injuries alike. Any number of
    /// changes can be under way at once, as long as nobody is in two of them.
    /// </summary>
    public bool RequestSubstitution(int side, Agent outgoing, Agent incoming)
    {
        var s = Sides[side];
        if (outgoing.State != AgentState.OnCourt || incoming.State != AgentState.Bench) return false;
        if (outgoing.IsKeeper != incoming.IsKeeper) return false;
        if (s.Changes.Any(c => c.Entering == incoming)) return false;
        // She passes it on before she goes; until then she plays on.
        if (Ball.Holder == outgoing && Phase == MatchPhase.Play) return false;

        s.Changes.Add(new SubChange(outgoing, incoming));
        outgoing.State = AgentState.LeavingCourt;
        s.RefreshCourt();
        return true;
    }

    private void UpdateSubstitutions()
    {
        foreach (var s in Sides)
        {
            if (!s.IsChanging) continue;
            var point = SubPoint(s);
            for (int i = 0; i < s.Changes.Count; i++)
            {
                var change = s.Changes[i];
                var leaving = change.Leaving;
                leaving.Target = point;
                leaving.DesiredSpeed = leaving.MaxSpeed * 0.9f;
                if (Vec.Dist(leaving.Pos, point) > 0.8f) continue;

                s.Changes.RemoveAt(i--);
                var incoming = change.Entering;
                if (incoming.State != AgentState.Bench)
                {
                    // Her replacement went on some other way meanwhile: she stays on.
                    leaving.State = AgentState.OnCourt;
                    s.RefreshCourt();
                    continue;
                }
                leaving.State = AgentState.Bench;
                incoming.State = AgentState.OnCourt;
                if (!change.KeepsRoles) incoming.Role = leaving.Role;
                if (leaving.Role == ExtraRole) leaving.Role = leaving.Player.Position;
                // On from the line where she was waiting; straight from the bench if she had not got there yet.
                incoming.Pos = Vec.Dist(incoming.Pos, point) < 3f ? incoming.Pos : point;
                incoming.Vel = Vector2.Zero;
                bool swap = change.KeepsRoles || incoming.Role == ExtraRole || incoming.IsKeeperByTrade && incoming.Role == "GK" && !leaving.IsKeeperByTrade;
                s.RefreshCourt();
                EnsureStat(incoming);
                if (incoming.Role == ExtraRole) Emit(EventKind.EmptyGoal, s.Index, incoming, leaving);
                else if (!swap) Emit(EventKind.Substitution, s.Index, incoming, leaving);
            }
        }
    }

    /// <summary>
    /// The bench's own judgement: a tired player comes off for a fresher one in the same
    /// position, at a moment the side has the ball, so it is not a man short at the back.
    /// </summary>
    private void ManageBench(TeamSide s, bool stoppage = false)
    {
        s.LastSubCheck = Clock;
        // The manager's side, with the rotation switched off: every change is theirs to make.
        if (s.Index == Setup.UserSide && !UserAutoSubstitutions) return;
        // Two at a time at most: a bench changing half its court at once leaves it short.
        if (s.Changes.Count >= 2 || (Possession != s.Index && !stoppage)) return;

        Agent? worst = null;
        foreach (var a in s.Court)
        {
            if (a.IsKeeper || a == Ball.Holder) continue;
            if (a.Energy < _t.RotateBelow && (worst == null || a.Energy < worst.Energy)) worst = a;
        }
        if (worst == null) return;

        var fresh = s.Squad.Where(b => b.State == AgentState.Bench && !b.IsKeeper && b.Energy > _t.FreshAbove
                                       && b.SuspendedUntil <= 0 && s.Changes.All(c => c.Entering != b))
            .OrderByDescending(b => b.Player.Position == worst.Role)
            .ThenByDescending(b => b.Player.Overall100 * (0.7 + 0.3 * b.Energy / 100))
            .FirstOrDefault();
        if (fresh == null) return;

        // Only if the change is worth making: a much weaker deputy stays on the bench.
        double now = worst.Player.Overall100 * (0.7 + 0.3 * worst.Energy / 100);
        // A coach rotates rather than waiting for legs to go: a fresh deputy a little weaker on
        // paper is worth it, a much weaker one is not.
        double then = fresh.Player.Overall100 * (0.7 + 0.3 * fresh.Energy / 100)
                      * (fresh.Player.Position == worst.Role ? 1.0 : 0.9);
        // Somebody running on empty comes off for almost anyone.
        double tolerance = worst.Energy < 45f ? _t.RotateTolerance * 0.8 : _t.RotateTolerance;
        if (then < now * tolerance) return;

        RequestSubstitution(s.Index, worst, fresh);
    }

    // ── Energy ─────────────────────────────────────────────────────────────────

    private void UpdateEnergy()
    {
        foreach (var a in Agents)
        {
            if (a.OnCourt)
            {
                float speed2 = Vec.LenSq(a.Vel);
                // Stamina 5 drains at 1.25×, 12 at 0.9×, 20 at half: a real difference over an hour.
                float stamina = 1.5f - a.Player.Stamina / 20f;
                float drain = (_t.DrainIdle + _t.DrainPerSpeedSquared * speed2) * stamina;
                if (a.IsKeeper) drain *= 0.5f;
                a.Energy = MathF.Max(5f, a.Energy - drain * Dt);
                a.CourtSeconds += Dt;
            }
            else if (a.State is AgentState.Bench or AgentState.Suspended)
            {
                a.Energy = MathF.Min(100f, a.Energy + _t.BenchRecovery * Dt * 10f);
            }
        }
    }

    // ── Bookkeeping ────────────────────────────────────────────────────────────

    private MatchPlayerStat EnsureStat(Agent a)
    {
        if (!Stats.TryGetValue(a.Id, out var st))
        {
            st = new MatchPlayerStat { PlayerId = a.Id, PlayerName = a.Name, TeamId = Sides[a.Side].Team.Id };
            Stats[a.Id] = st;
        }
        return st;
    }

    private void Emit(EventKind kind, int side, Agent? who, Agent? other = null)
        => Events.Add(new MatchEvent2D(Clock, kind, side, who?.Id ?? 0, who?.Name ?? "",
            other?.Id ?? 0, other?.Name ?? "", Ball.Pos, GameTime));

    private float Rand() => (float)_rng.NextDouble();

    private float Gauss()
    {
        double u1 = 1.0 - _rng.NextDouble(), u2 = _rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * DetMath.Log(u1)) * DetMath.Cos(2 * Math.PI * u2));
    }

    private static float Sigmoid(float x) => 1f / (1f + DetMath.Exp(-x));
}

public sealed class AttackRecord
{
    public int Side;
    public float Start, End;
    public string StartedBy = "";
    public int Passes, Shots;
    public float FirstShotAt = -1;
    public float Duration => End - Start;
}
