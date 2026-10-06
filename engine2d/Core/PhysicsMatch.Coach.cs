using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.Engine2D;

/// <summary>
/// The other bench. Once a minute it reads the match the way a coach does — the score, the
/// clock, who is hurting it — and changes what it asks for. It never touches the side the
/// player manages.
/// </summary>
public sealed partial class PhysicsMatch
{
    private readonly float[] _lastCoachLook = new float[2];
    private readonly int[] _runAgainst = new int[2];
    private readonly int[] _timeoutHalf = { -1, -1 };

    /// <summary>Goals conceded per side, by who scored them: the pivot, the backs from distance, and so on.</summary>
    private readonly int[] _pivotGoalsAgainst = new int[2];
    private readonly int[] _longGoalsAgainst = new int[2];

    /// <summary>Goals each side has let in with its goal empty — how a bench learns 7 v 6 is costing it.</summary>
    private readonly int[] _emptyNetGoalsAgainst = new int[2];

    private bool IsCoached(int side) => Setup.UserSide != side;

    /// <summary>Called when a goal goes in, so the benches can see a run building.</summary>
    private void NoteGoal(int scoringSide, ShotRecord? shot)
    {
        int conceding = 1 - scoringSide;
        _runAgainst[conceding]++;
        _runAgainst[scoringSide] = 0;
        if (Sides[conceding].Keeper == null) _emptyNetGoalsAgainst[conceding]++;
        if (shot == null) return;
        if (shot.Role == "Pivot") _pivotGoalsAgainst[conceding]++;
        if (shot.Distance >= 8.5f) _longGoalsAgainst[conceding]++;
    }

    /// <summary>
    /// Once a minute, for both benches: a side chasing the game finds something extra in
    /// proportion to its Determination (up to three goals down), and in a tight finish — the
    /// last ten minutes, two goals or fewer in it — the manager's Timeout Talks tell. The same
    /// two rules as the classic engine, taken at half strength because they reach every attribute.
    /// </summary>
    private void UpdateSpirit()
    {
        bool tight = GameTime >= 2 * HalfLength - 600f && Math.Abs(Score[0] - Score[1]) <= 2;
        foreach (var s in Sides)
        {
            int deficit = Score[1 - s.Index] - Score[s.Index];
            var outfield = s.Court.Where(a => !a.IsKeeper).Select(a => a.Player).ToList();
            double shift = TeamIntangibles.ComebackShift(outfield, deficit)
                           + ManagerEffects.TimeoutShift(s.Team.Manager, tight);
            float lift = (float)(1.0 + 0.5 * shift);
            // Well ahead: the starters are rested and the side stops pressing, so a lead stops growing.
            int lead = -deficit;
            if (lead > _t.ComfortableLead) lift *= 1f - MathF.Min(_t.EasingOffMax, (lead - _t.ComfortableLead) * _t.EasingOffPerGoal);
            foreach (var a in s.Squad) a.Lift = lift;
        }
    }

    private void Coach(TeamSide s)
    {
        int i = s.Index;
        if (!IsCoached(i)) return;

        // A time-out to stop a run: the moment the side has the ball, three unanswered goals
        // down, one per half.
        if (_runAgainst[i] >= 3 && Possession == i && Phase == MatchPhase.Play && Ball.State == BallState.Held
            && _timeoutHalf[i] != Period && s.TimeoutsLeft > 0 && Ball.Holder?.Side == i)
        {
            if (RequestTimeout(i)) { _timeoutHalf[i] = Period; _runAgainst[i] = 0; }
        }

        if (Clock - _lastCoachLook[i] < 60f) return;
        _lastCoachLook[i] = Clock;

        var t = s.Tactics.Clone();
        var standing = MatchTactics.From(s.Team);
        // This bench pulls its keeper by its own judgement (_aiEmptyGoal below), never on a
        // standing order. A sheet that arrived with the goal empty — the classic engine's
        // full-time sheet once did — would otherwise have kept it empty for the whole match.
        t.UseEmptyGoal = false;
        int lead = Score[i] - Score[1 - i];
        float left = 2 * HalfLength - GameTime;

        // Chasing, late: run, press, and in the last minutes take the keeper out when attacking.
        if (left < 600f && lead < 0)
        {
            t.Tempo = AttackTempo.Counter;
            t.Aggression = DefensiveAggression.Aggressive;
        }
        // Sitting on a lead, late: slow it down.
        else if (left < 600f && lead >= 5)
        {
            t.Tempo = AttackTempo.Controlled;
        }
        else
        {
            t.Tempo = standing.Tempo;
            t.Aggression = standing.Aggression;
        }

        // The shape: a pivot running riot gets a tight six-nil; shooters from distance get
        // somebody out to meet them.
        if (_pivotGoalsAgainst[i] >= 4 && _pivotGoalsAgainst[i] > _longGoalsAgainst[i]) t.System = DefensiveSystem.SixZero;
        else if (_longGoalsAgainst[i] >= 5) t.System = DefensiveSystem.FiveOne;

        // Seven against six, the way benches really use it: to chase a close game in its last
        // minutes, or to make up the numbers while a player short in a tight second half. It
        // used to come out whenever a side was short-handed and not winning — ten goals down
        // included — and gave away more empty-net goals than it ever scored. A bench that has
        // conceded twice into its own empty goal puts its keeper back, short-handed or not,
        // and only the last two minutes of a one- or two-goal game can tempt it again.
        bool shortHanded = s.Squad.Count(a => a.State == AgentState.Suspended || (a.State == AgentState.SentOff && a.SuspendedUntil > GameTime)) > 0;
        bool chasing = left < 300f && lead is < 0 and >= -3;
        bool makingUpNumbers = shortHanded && Period >= 2 && left < 1200f && lead is >= -3 and <= 1;
        bool burned = _emptyNetGoalsAgainst[i] >= 2;
        bool lastGasp = left < 120f && lead is < 0 and >= -2;
        _aiEmptyGoal[i] = burned ? lastGasp : chasing || makingUpNumbers;

        s.Tactics = t;
    }
}
