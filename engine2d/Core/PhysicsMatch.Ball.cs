using System.Numerics;
using HandballManager.Models;
using HandballManager.Helpers;

namespace HandballManager.Engine2D;

/// <summary>
/// The ball: thrown, caught, fumbled, cut out, loose on the floor, out of play.
/// </summary>
public sealed partial class PhysicsMatch
{
    /// <summary>Starts a pass towards where the receiver will be when it gets there.</summary>
    private void StartPass(Agent from, Agent to)
    {
        var side = Sides[from.Side];

        // After the referee's arm goes up there are four passes left before a shot.
        if (PassiveWarned)
        {
            if (_passesSinceWarning >= 4)
            {
                Emit(EventKind.PassivePlay, from.Side, from);
                AwardFreeThrow(1 - from.Side, from.Pos, null);
                return;
            }
            _passesSinceWarning++;
        }

        if (_attack != null) _attack.Passes++;
        var p = from.Player;
        float speed = _t.PassSpeedBase + _t.PassSpeedSpan * from.Eff(p.Passing) / 20f;

        // Lead the receiver: throw to where she is running, not where she is.
        Vector2 target = to.Pos;
        for (int i = 0; i < 2; i++)
        {
            float tt = Vec.Dist(from.Pos, target) / speed;
            target = to.Pos + to.Vel * tt;
        }
        var (tu, tw) = Frame(side, target);
        (tu, tw) = Court.PushOutTo(tu, tw, Court.GoalAreaRadius + 0.05f);
        target = Court.Clamp(World(side, tu, tw), 0.3f);

        float d = Vec.Dist(from.Pos, target);
        float sigma = _t.PassErrorBase
                      + _t.PassErrorPerMetre * d * MathF.Max(0.3f, 1.3f - from.Eff(p.Passing) / 20f)
                      + 0.3f * Pressure(from, from.Pos)
                      + 0.1f * from.Fatigue;
        target += new Vector2(Gauss(), Gauss()) * sigma;
        d = Vec.Dist(from.Pos, target);

        Ball.State = BallState.Pass;
        Ball.Holder = null;
        Ball.LastTouch = from;
        Ball.Thrower = from;
        Ball.Receiver = to;
        Ball.From = from.Pos;
        Ball.To = target;
        Ball.FromHeight = Ball.ToHeight = 1.3f;
        Ball.Elapsed = 0f;
        Ball.Duration = MathF.Max(0.15f, d / speed);
        Ball.Pos = from.Pos;

        FindInterceptor(from, target, speed);
        from.Intent = "pass";
    }

    /// <summary>
    /// Who can get to the passing lane before the ball goes past. Worked out once, at release:
    /// each opponent is given the time the ball takes to reach the point nearest her, less how
    /// long she takes to read the pass, and either her legs cover the gap or they do not.
    /// </summary>
    private void FindInterceptor(Agent from, Vector2 target, float speed)
    {
        Ball.Interceptor = null;
        var d = target - Ball.From;
        float len2 = Vec.LenSq(d);
        if (len2 < 0.01f) return;
        float len = MathF.Sqrt(len2);

        float bestT = 2f;
        foreach (var def in Opp(Sides[from.Side]).Court)
        {
            if (def.StunnedUntil > Clock) continue;
            float t = Math.Clamp(Vec.Dot(def.Pos - Ball.From, d) / len2, 0f, 1f);
            if (t < 0.12f) continue;
            var point = Ball.From + d * t;
            float ballTime = t * len / speed;
            float read = _t.InterceptRead - 0.18f * def.Eff(def.Player.Anticipation) / 20f;
            float legs = def.MaxSpeed * _t.InterceptLegs * MathF.Max(0f, ballTime - read);
            float reach = def.IsKeeper ? _t.InterceptReach + 0.6f : _t.InterceptReach;
            float gap = Vec.Dist(def.Pos, point) - reach;
            if (gap > legs || t >= bestT) continue;

            bestT = t;
            float margin = legs - gap;
            float catchIt = Math.Clamp(0.10f + 0.25f * margin + 0.3f * def.Eff(def.Player.Receiving) / 20f, 0.1f, 0.8f);
            Ball.Interceptor = def;
            Ball.InterceptAt = t;
            Ball.InterceptorCatches = Rand() < catchIt;
        }
    }

    private void UpdateBall()
    {
        switch (Ball.State)
        {
            case BallState.Held:
                if (Ball.Holder == null || !Ball.Holder.OnCourt) { MakeLoose(Ball.Pos, Vector2.Zero); break; }
                Ball.Pos = Ball.Holder.Pos;
                Ball.Height = 1.1f;
                break;

            case BallState.Pass when Ball.IsKempa:
                Ball.Elapsed += Dt;
                Ball.Pos = Vec.Lerp(Ball.From, Ball.To, Ball.Progress);
                Ball.Height = Ball.FromHeight + (Ball.ToHeight - Ball.FromHeight) * Ball.Progress + 2.2f * DetMath.Sin(MathF.PI * Ball.Progress);
                if (Ball.Progress >= 1f) LandKempa();
                break;

            case BallState.Pass:
                Ball.Elapsed += Dt;
                Ball.Pos = Vec.Lerp(Ball.From, Ball.To, Ball.Progress);
                Ball.Height = 1.3f + 0.6f * DetMath.Sin(MathF.PI * Ball.Progress) * MathF.Min(1f, Ball.Duration);
                if (Ball.Interceptor != null && Ball.Progress >= Ball.InterceptAt) { ResolveInterception(); break; }
                if (Ball.Progress >= 1f) ResolveCatch();
                break;

            case BallState.Shot:
                Ball.Elapsed += Dt;
                Ball.Pos = Vec.Lerp(Ball.From, Ball.To, Ball.Progress);
                Ball.Height = Ball.FromHeight + (Ball.ToHeight - Ball.FromHeight) * Ball.Progress;
                if (Ball.Progress >= 1f) LandShot();
                break;

            case BallState.Loose:
                UpdateLooseBall();
                break;
        }
    }

    private void ResolveInterception()
    {
        var def = Ball.Interceptor!;
        var thrower = Ball.Thrower;
        Ball.Interceptor = null;
        if (Ball.InterceptorCatches && def.OnCourt)
        {
            Emit(EventKind.Interception, def.Side, def, thrower);
            string key = "interception to " + (Ball.Receiver?.Role ?? "?");
            TurnoverCauses[key] = TurnoverCauses.GetValueOrDefault(key) + 1;
            GiveBall(def, newAttack: true, lookDelay: 0.35f);
        }
        else
        {
            // A hand to it, not a catch: it spins off somewhere.
            float angle = Rand() * MathF.Tau;
            MakeLoose(Ball.Pos, new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * (3f + 3f * Rand()));
            Ball.LastTouch = def;
        }
    }

    private void ResolveCatch()
    {
        var r = Ball.Receiver;
        var thrower = Ball.Thrower;
        if (r != null && r.OnCourt)
        {
            float dist = Vec.Dist(r.Pos, Ball.To);

            // A defender on her at the moment it arrives gets a hand in: the pivot's whole
            // difficulty, and a wing's when her defender has stayed tight.
            var contester = Contester(r, Ball.To);
            if (contester != null && Rand() < ContestChance(contester, r, Ball.To))
            {
                float angle = Rand() * MathF.Tau;
                MakeLoose(Ball.To, new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * 2.5f);
                Ball.LastTouch = contester;
                TurnoverCauses["contested " + r.Role] = TurnoverCauses.GetValueOrDefault("contested " + r.Role) + 1;
                return;
            }

            if (dist < 1.2f)
            {
                float hands = r.Eff(r.Player.Receiving);
                float chance = Math.Clamp(0.988f + (hands - 12f) * 0.002f - 0.06f * MathF.Max(0f, dist - 0.7f)
                                          - 0.03f * Pressure(r, r.Pos), 0.6f, 0.998f);
                if (Rand() < chance)
                {
                    _lastPasser = thrower;
                    _lastReceiver = r;
                    // A pivot catches it with her back to goal and has to turn first.
                    GiveBall(r, newAttack: false, lookDelay: r.DecisionInterval * 0.6f + (r.Role == "Pivot" ? 0.3f : 0f));
                    r.GotBallBy = "pass";
                    return;
                }
            }
        }

        // Dropped, or thrown where nobody was.
        TurnoverCauses[r != null && r.OnCourt && Vec.Dist(r.Pos, Ball.To) < 1.2f ? "dropped" : "bad pass"] =
            TurnoverCauses.GetValueOrDefault(r != null && r.OnCourt && Vec.Dist(r.Pos, Ball.To) < 1.2f ? "dropped" : "bad pass") + 1;
        var dir = Ball.To - Ball.From;
        if (Vec.LenSq(dir) > 0.01f) dir = Vec.Norm(dir);
        MakeLoose(Ball.To, dir * 2.5f);
        if (r != null) Ball.LastTouch = r;
    }

    /// <summary>
    /// The defender nearest where the ball is arriving, if she is close enough to fight for it.
    /// With <paramref name="lead"/> seconds still to go, she is given the ground she can make up
    /// in that time — which is how a passer should read it, and usually does not.
    /// </summary>
    private Agent? Contester(Agent receiver, Vector2 at, float lead = 0f)
    {
        Agent? best = null;
        float nearest = 1.3f;
        foreach (var d in Opp(Sides[receiver.Side]).Court)
        {
            if (d.IsKeeper || d.StunnedUntil > Clock) continue;
            // She only starts closing once she has read where it is going.
            float closing = MathF.Min(0.6f, MathF.Max(0f, lead - 0.35f));
            float dist = MathF.Max(0f, Vec.Dist(d.Pos, at) - d.MaxSpeed * 0.5f * closing);
            if (dist < nearest) { nearest = dist; best = d; }
        }
        _contestDistance = nearest;
        return best;
    }

    private float _contestDistance;

    /// <summary>How often a defender that close gets the ball off her: body position against body position.</summary>
    private float ContestChance(Agent defender, Agent receiver, Vector2 at)
    {
        float closeness = 1f - _contestDistance / 1.3f;
        float edge = (defender.Eff(defender.Player.Positioning) + defender.Eff(defender.Player.Anticipation) + defender.Eff(defender.Player.Strength)
                      - receiver.Eff(receiver.Player.Strength) - receiver.Eff(receiver.Player.Receiving) - receiver.Eff(receiver.Player.OffTheBall)) / 3f;
        return Math.Clamp(_t.ContestBase * closeness * (1f + edge / 10f), 0f, 0.7f);
    }

    private void GiveBall(Agent a, bool newAttack, float lookDelay)
    {
        foreach (var x in Agents) { if (x.JumpUntil > 0f) { x.JumpUntil = 0f; x.Airborne = false; } x.DiveUntil = 0f; }
        if (newAttack || Possession != a.Side)
        {
            // A rebound gathered by the defence is a save or a block doing its job, not a turnover.
            if (Possession != a.Side && !newAttack && !_looseAfterShot)
            {
                Emit(EventKind.Turnover, a.Side, a);
                TurnoverCauses["loose ball"] = TurnoverCauses.GetValueOrDefault("loose ball") + 1;
            }
            NewAttack(a.Side);
        }
        _looseAfterShot = false;
        Possession = a.Side;
        Ball.State = BallState.Held;
        Ball.Holder = a;
        Ball.LastTouch = a;
        Ball.Receiver = null;
        a.NextDecisionAt = Clock + lookDelay;
        a.HasBallSince = Clock;
        a.ReadSeed = _rng.Next();
    }

    private void MakeLoose(Vector2 at, Vector2 vel)
    {
        Ball.State = BallState.Loose;
        Ball.Holder = null;
        Ball.Receiver = null;
        Ball.Pos = at;
        Ball.Vel = vel;
        Ball.Height = 0.6f;
    }

    private void UpdateLooseBall()
    {
        Ball.Pos += Ball.Vel * Dt;
        Ball.Vel *= 0.92f;
        Ball.Height = MathF.Max(0.15f, Ball.Height - 0.1f);

        if (!Court.InsideCourt(Ball.Pos)) { OutOfBounds(); return; }

        // A ball at rest in a goal area belongs to that keeper.
        foreach (var s in Sides)
        {
            var (u, w) = Frame(Opp(s), Ball.Pos);
            if (!Court.InGoalArea(u, w)) continue;
            // Not her own deflection on its way out: she has to be able to get to it first.
            if (s.Keeper != null && Vec.Dist(s.Keeper.Pos, Ball.Pos) < 0.7f && Vec.Len(Ball.Vel) < 3f)
            {
                GiveBall(s.Keeper, newAttack: Possession != s.Index, lookDelay: 0.35f);
                return;
            }
            if (Vec.Len(Ball.Vel) < 0.6f) { AwardGoalThrow(s.Index); return; }
            return;
        }

        Agent? nearest = null;
        float best = 0.6f;
        foreach (var a in Agents)
        {
            if (a.State != AgentState.OnCourt || a.StunnedUntil > Clock) continue;
            float d = Vec.Dist(a.Pos, Ball.Pos);
            if (d < best) { best = d; nearest = a; }
        }
        if (nearest == null) return;
        if (Rand() < 0.5f + 0.4f * nearest.Eff(nearest.Player.Receiving) / 20f)
        {
            GiveBall(nearest, newAttack: false, lookDelay: 0.3f);
            nearest.GotBallBy = "loose";
        }
    }

    private void OutOfBounds()
    {
        var p = Ball.Pos;
        var last = Ball.LastTouch;
        int touchedBy = last?.Side ?? Possession;

        if (p.X < 0 || p.X > Court.Length)
        {
            // Over an end line. The side defending that goal throws it out — unless one of its
            // court players put it there, which gives the attack a throw from the corner.
            float endX = p.X < 0 ? 0f : Court.Length;
            var defending = Sides.First(s => MathF.Abs(s.OwnGoalX - endX) < 1f);
            if (last != null && last.Side == defending.Index && !last.IsKeeper)
            {
                var corner = new Vector2(endX == 0 ? 0.1f : Court.Length - 0.1f, p.Y < Court.CentreY ? 0.1f : Court.Width - 0.1f);
                AwardThrowIn(1 - defending.Index, corner);
            }
            else AwardGoalThrow(defending.Index);
            return;
        }

        var spot = new Vector2(Math.Clamp(p.X, 0.1f, Court.Length - 0.1f), p.Y < 0 ? 0.05f : Court.Width - 0.05f);
        AwardThrowIn(1 - touchedBy, spot);
    }

    private void AwardThrowIn(int side, Vector2 spot)
    {
        var taker = Sides[side].Court.Where(a => !a.IsKeeper)
            .OrderBy(a => Vec.DistSq(a.Pos, spot)).FirstOrDefault();
        if (taker == null) return;
        BeginRestart(RestartKind.ThrowIn, side, spot, taker, 1.0f, newAttack: Possession != side);
    }

    /// <summary>The shot arrives. What happened was settled at release; this is where it counts.</summary>
    private void LandShot()
    {
        var shooter = Ball.Thrower!;
        int a = shooter.Side, d = 1 - a;
        var keeper = Ball.ShotKeeper;
        bool seven = Ball.IsSevenMetre;
        Ball.IsSevenMetre = false;
        // The dive is over where it ended: a parry comes off her there, and she gets up from it.
        if (keeper != null) { keeper.DiveUntil = 0f; keeper.Vel = Vector2.Zero; }

        // A shot on goal wipes the referee's passive-play warning.
        PassiveWarned = false;
        _passesSinceWarning = 0;

        switch (Ball.PendingShot)
        {
            case ShotOutcome.Goal:
            {
                Score[a]++;
                var st = EnsureStat(shooter);
                st.Goals++;
                if (seven) st.SevenMeterGoals++;
                // An assist is the pass she scored straight off: received from a team-mate and
                // shot within the window, as a stat sheet would credit it.
                if (!seven && _lastReceiver == shooter && _lastPasser != null && _lastPasser.Side == a
                    && LastShot is { GotBallBy: "pass" } shot && shot.HeldFor <= _t.AssistWindow)
                    EnsureStat(_lastPasser).Assists++;
                if (keeper != null) EnsureStat(keeper).GoalsAgainst++;
                Emit(seven ? EventKind.SevenMetreGoal : EventKind.Goal, a, shooter, seven ? keeper : _lastPasser);
                NoteGoal(a, LastShot);

                BeginThrowOffAfterGoal(d, Ball.To);
                break;
            }

            case ShotOutcome.SavedHeld:
                if (keeper != null) EnsureStat(keeper).Saves++;
                Emit(seven ? EventKind.SevenMetreSaved : EventKind.Save, d, keeper, shooter);
                if (keeper != null && keeper.OnCourt) GiveBall(keeper, newAttack: true, lookDelay: 0.25f + 0.25f * Rand());
                else AwardGoalThrow(d);
                break;

            case ShotOutcome.Saved:
                if (keeper != null) EnsureStat(keeper).Saves++;
                Emit(seven ? EventKind.SevenMetreSaved : EventKind.Save, d, keeper, shooter);
                if (keeper == null) AwardGoalThrow(d);
                else Parry(keeper, Sides[a], LastShot?.GoalW ?? 0f, LastShot?.GoalZ ?? 1f);
                break;

            case ShotOutcome.Missed:
                Emit(seven ? EventKind.SevenMetreMissed : EventKind.Miss, a, shooter);
                AwardGoalThrow(d);
                break;

            case ShotOutcome.Post:
                Emit(seven ? EventKind.SevenMetreMissed : EventKind.Post, a, shooter);
                ReboundFrom(Ball.To, Sides[a], 4f, 7f, shooter);
                break;

            case ShotOutcome.Blocked:
                Emit(EventKind.Block, d, Ball.ShotBlocker, shooter);
                if (Ball.ShotBlocker != null && Rand() < _t.BlockOverTheLine) DeflectOverOwnLine(Ball.ShotBlocker, Sides[a]);
                else ReboundFrom(Ball.ShotBlocker?.Pos ?? Ball.Pos, Sides[a], 2f, 5f, Ball.ShotBlocker);
                break;
        }
    }

    /// <summary>
    /// Where a parried shot goes, from where it was going. Up high she tips it over the bar —
    /// out behind the goal, a goal throw. Low or wide she pushes it aside, and it flies off
    /// towards the line beside the goal (a goal throw) or, now and then, all the way to the side
    /// line (a throw-in to the attack, since she touched it last). Otherwise it comes back off
    /// her into play. The ball is sent there and the rules of the court decide the rest.
    /// </summary>
    private void Parry(Agent keeper, TeamSide attackers, float w, float z)
    {
        float outX = attackers.Dir > 0 ? 1f : -1f; // towards and past the end line she guards
        float sideways = MathF.Sign(w == 0 ? (Rand() - 0.5f) : w);
        Vector2 dir;
        float speed;
        if (z > _t.TipOverHeight && Rand() < 0.8f)
        {
            dir = Vec.Norm(new Vector2(outX, sideways * (0.1f + 0.4f * Rand())));
            speed = 5f + 3f * Rand();
            MakeLoose(keeper.Pos, dir * speed);
            Ball.Height = 2.4f;
        }
        else if (MathF.Abs(w) > _t.PushWideFrom)
        {
            // Swept aside: out over the line beside the goal, or across the court towards the
            // side line — how far is how hard she got to it.
            dir = Vec.Norm(new Vector2(outX * (Rand() * 1.0f - 0.45f), sideways));
            speed = 6f + 6f * Rand();
            MakeLoose(keeper.Pos + dir * 0.6f, dir * speed);
        }
        else
        {
            // Off her body, back out into the court where anyone can get to it.
            ReboundFrom(keeper.Pos + new Vector2(-outX * 0.8f, 0f), attackers, 4f, 8f, keeper);
            return;
        }
        Ball.LastTouch = keeper;
        _looseAfterShot = true;
    }

    /// <summary>
    /// A block that takes the ball back over the defence's own end line, beside the goal: a
    /// throw from the corner to the attack, as the rule gives when a court player put it there.
    /// </summary>
    private void DeflectOverOwnLine(Agent blocker, TeamSide attackers)
    {
        float lineX = attackers.AttackGoalX;
        float sideW = (Rand() < 0.5f ? -1f : 1f) * (Court.PostHalf + 1f + 4f * Rand());
        var target = new Vector2(lineX + (attackers.Dir > 0 ? 0.5f : -0.5f), Court.CentreY + sideW);
        var dir = target - blocker.Pos;
        if (Vec.LenSq(dir) < 0.01f) dir = new Vector2(attackers.Dir, 0f);
        float dist = Vec.Len(dir);
        // Fast enough to reach the line through the floor's drag, which takes about 1.25 m per m/s.
        MakeLoose(blocker.Pos, Vec.Norm(dir) * (dist / 1.1f + 1f));
        Ball.LastTouch = blocker;
        _looseAfterShot = true;
    }

    /// <summary>The ball comes back off a keeper, a post or a block, out into the court.</summary>
    private void ReboundFrom(Vector2 at, TeamSide attackers, float minSpeed, float maxSpeed, Agent? touched)
    {
        // Back towards the attackers' half, in a spread.
        float away = attackers.Dir < 0 ? 0f : MathF.PI;
        float angle = away + (Rand() - 0.5f) * 2.2f;
        float speed = minSpeed + (maxSpeed - minSpeed) * Rand();
        var start = at + new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * 0.5f;
        MakeLoose(Court.Clamp(start, 0.2f), new Vector2(DetMath.Cos(angle), DetMath.Sin(angle)) * speed);
        Ball.LastTouch = touched;
        _looseAfterShot = true;
    }

    private bool _looseAfterShot;

    private void TakeSevenMetre(Agent taker)
    {
        Phase = MatchPhase.Play;
        TakeShot(taker, sevenMetre: true);
    }
}
