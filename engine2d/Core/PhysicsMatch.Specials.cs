using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>
/// The rarer things: the seven-metre taker the manager names, and the Kempa trick.
/// </summary>
public sealed partial class PhysicsMatch
{
    // ── Naming the seven-metre taker ───────────────────────────────────────────

    /// <summary>When set, a seven-metre to the managed side waits for <see cref="ChooseSevenMetreTaker"/>.</summary>
    public bool AskForSevenMetreTaker { get; set; }

    /// <summary>A seven-metre to the managed side is waiting for somebody to be named.</summary>
    public bool AwaitingTaker { get; private set; }

    /// <summary>Names the player to take the throw the match is waiting on.</summary>
    public void ChooseSevenMetreTaker(int playerId)
    {
        if (!AwaitingTaker) return;
        var s = Sides[_restartSide];
        var taker = s.Court.FirstOrDefault(a => a.Id == playerId && !a.IsKeeper);
        if (taker != null)
        {
            if (Ball.Holder == _restartTaker) Ball.Holder = taker;
            _restartTaker = taker;
        }
        AwaitingTaker = false;
        _restartReadyAt = Clock + 2f;
    }

    // ── Naming the keeper to face a seven-metre ────────────────────────────────
    //
    // A coach can bring on a penalty specialist for one throw. The change goes through the zone
    // like any other, and the referees wait until she is in her goal before they whistle.

    /// <summary>The match is stopped for the manager: a taker or a keeper to name.</summary>
    public bool AwaitingChoice => AwaitingTaker || AwaitingKeeper;

    /// <summary>A seven-metre against the managed side is waiting for its keeper to be named.</summary>
    public bool AwaitingKeeper { get; private set; }

    /// <summary>The keeper coming on to face the throw, until she is in her goal.</summary>
    private Agent? _sevenMetreKeeper;

    /// <summary>
    /// Names the keeper to face the throw the match is waiting on: the one in goal (or 0) keeps
    /// her place, a keeper from the bench replaces her.
    /// </summary>
    public void ChooseSevenMetreKeeper(int playerId)
    {
        if (!AwaitingKeeper) return;
        AwaitingKeeper = false;
        _restartReadyAt = Clock + 2f;

        var s = Opp(Sides[_restartSide]);
        var chosen = s.Squad.FirstOrDefault(a => a.Id == playerId);
        if (chosen == null || chosen == s.Keeper || !chosen.IsKeeperByTrade || chosen.State != AgentState.Bench) return;
        // Whoever is in goal now — or nobody, with the keeper pulled for a seventh attacker —
        // the one named is who faces the throw. The change is made as soon as it can be
        // (SevenMetreKeeperOnHerWay), and the throw waits for her.
        _sevenMetreKeeper = chosen;
        if (s.Keeper != null && !s.IsChanging) RequestSubstitution(s.Index, s.Keeper, chosen);
    }

    /// <summary>The keepers the managed side could put in goal for this throw: the one there first.</summary>
    public IEnumerable<Agent> SevenMetreKeepers(int side)
    {
        var s = Sides[side];
        if (s.Keeper != null) yield return s.Keeper;
        foreach (var a in s.Squad)
            if (a.IsKeeperByTrade && a.State == AgentState.Bench && a.SuspendedUntil <= 0 && s.Changes.All(c => c.Entering != a))
                yield return a;
    }

    /// <summary>
    /// The goal the throw is at has a keeper ready in it: on her spot for the throw, standing,
    /// and for a moment. "Somewhere near her line" was not enough — a keeper coming on from the
    /// bench arrives from the side of the court and crossed into that box three metres out and
    /// still running, and the throw went in past her.
    /// </summary>
    private bool GoalManned(Agent? k)
    {
        bool set = k is { State: AgentState.OnCourt, IsKeeper: true }
                   && Vec.Dist(k.Pos, KeeperSpot(Opp(Sides[_restartSide]), _restartSpot, 1.0f)) < 0.8f
                   && Vec.Len(k.Vel) < 1.2f;
        if (!set) { _keeperSetSince = -1f; return false; }
        if (_keeperSetSince < 0f) _keeperSetSince = Clock;
        return Clock - _keeperSetSince >= 1.0f;
    }

    private float _keeperSetSince = -1f;

    /// <summary>
    /// Whether the throw has to wait for a keeper: the one the manager named is still on her
    /// way, or — named or not — nobody is in the goal yet, which after losing the ball playing
    /// seven against six is often the case. A throw into an empty net because the keeper was
    /// still running back is not something referees allow.
    /// </summary>
    private bool SevenMetreKeeperOnHerWay()
    {
        var defending = Opp(Sides[_restartSide]);
        // Nobody who could go in goal is fit: the throw cannot be held for ever.
        if (Clock > _restartReadyAt + 25f) { _sevenMetreKeeper = null; return false; }

        var k = _sevenMetreKeeper;
        if (k == null) return !GoalManned(defending.Keeper);

        // Named while nobody could be changed: make the change now.
        if (k.State == AgentState.Bench && !defending.IsChanging && defending.Keeper is { } current && current != k)
            RequestSubstitution(defending.Index, current, k);
        bool there = GoalManned(k);
        // She may never arrive — hurt, sent off: then whoever is in goal faces it.
        if (there || k.State is not (AgentState.OnCourt or AgentState.Bench))
        {
            _sevenMetreKeeper = null;
            return !GoalManned(defending.Keeper);
        }
        return true;
    }

    // ── The Kempa trick ────────────────────────────────────────────────────────
    //
    // A back lobs the ball into the goal area; a wing comes in off her line, takes off, catches it
    // in the air and finishes before she lands. It needs a passer who can place it, a wing with
    // the spring and the nerve, and a keeper who has not read it. Rare — a called play, not a
    // decision a player makes on every touch — and a gamble: mistimed, it is the keeper's ball.

    private Agent? _kempaWing;
    private float _kempaChance;

    /// <summary>The wing on the carrier's side who could go up for it, or null.</summary>
    private Agent? KempaPartner(Agent c)
    {
        var s = Sides[c.Side];
        if (c.Role is not ("LB" or "CB" or "RB") || c.Eff(c.Player.Passing) < 12f) return null;
        var (_, cw) = Frame(s, c.Pos);
        foreach (var a in s.Court)
        {
            if (a.Role is not ("LW" or "RW") || a.Eff(a.Player.Flair) < 13f) continue;
            var (_, aw) = Frame(s, a.Pos);
            if (c.Role != "CB" && MathF.Sign(aw) != MathF.Sign(cw)) continue;
            return a;
        }
        return null;
    }

    /// <summary>The chance the lob, the jump and the catch all come off.</summary>
    private float KempaSuccess(Agent passer, Agent wing)
    {
        var keeper = Opp(Sides[passer.Side]).Keeper;
        float k = keeper == null ? 0f : keeper.Eff(keeper.Player.Anticipation) / 20f;
        return Math.Clamp(0.25f + 0.25f * passer.Eff(passer.Player.Passing) / 20f
                          + 0.25f * (wing.Eff(wing.Player.Flair) + wing.Eff(wing.Player.JumpingReach) + wing.Eff(wing.Player.Receiving)) / 60f
                          - 0.2f * k, 0.15f, 0.8f);
    }

    /// <summary>Where the ball is lobbed to: inside the area, off the wing's side of the goal.</summary>
    private Vector2 KempaPoint(Agent wing)
    {
        var s = Sides[wing.Side];
        var (_, w) = Frame(s, wing.Pos);
        return World(s, 4.3f, MathF.Sign(w) * 2.3f);
    }

    private void StartKempa(Agent passer, Agent wing)
    {
        var to = KempaPoint(wing);
        _kempaWing = wing;
        _kempaChance = KempaSuccess(passer, wing);
        wing.Airborne = true;
        Emit(EventKind.KempaAttempt, passer.Side, passer, wing);

        Ball.State = BallState.Pass;
        Ball.Holder = null;
        Ball.LastTouch = passer;
        Ball.Thrower = passer;
        Ball.Receiver = wing;
        Ball.From = passer.Pos;
        Ball.To = to;
        Ball.FromHeight = 1.8f;
        Ball.ToHeight = 2.6f;
        Ball.Elapsed = 0f;
        Ball.Duration = MathF.Max(0.6f, Vec.Dist(passer.Pos, to) / 10f);
        Ball.Interceptor = null;
        Ball.IsKempa = true;
        passer.Intent = "pass";
    }

    /// <summary>The lob arrives: she is up there with it, or the keeper has it.</summary>
    private void LandKempa()
    {
        var wing = _kempaWing;
        Ball.IsKempa = false;
        _kempaWing = null;
        if (wing == null || !wing.OnCourt) { AwardGoalThrow(1 - (wing?.Side ?? Possession)); return; }

        bool caught = Rand() < _kempaChance;
        if (!caught)
        {
            wing.Airborne = false;
            Emit(EventKind.KempaFailed, wing.Side, wing);
            AwardGoalThrow(1 - wing.Side);
            return;
        }

        // In the air, over the area, ball in hand: shoot before landing.
        wing.Pos = Ball.To;
        _kempaShot = true;
        TakeShot(wing, sevenMetre: false);
        _kempaShot = false;
        wing.Airborne = false;
        wing.LandedUntil = Clock + 1.2f;
    }

    private bool _kempaShot;
}
