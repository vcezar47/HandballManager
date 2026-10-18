using System.Numerics;
using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// Bodies: how they move, how they collide, and what the referee makes of it.
/// </summary>
/// <remarks>
/// Most contact in handball is legal and ends in a free throw — the defender stops the attack,
/// the whistle goes, the attack starts again from where it was. Duels here are decided on the
/// attributes a real one turns on: footwork, balance and strength with the ball against
/// tackling, reading and strength without it. Running at a defender at full speed is how you
/// get past her, and also how she ends up hauling you down; stopping a player who was through
/// on goal is a seven-metre throw, and the rougher the stop the likelier it is two minutes.
/// </remarks>
public sealed partial class PhysicsMatch
{
    /// <summary>Why the ball changed hands without a shot, for calibration.</summary>
    public Dictionary<string, int> TurnoverCauses { get; } = [];

    private float _contactClock;
    private Agent? _brokeThrough;
    private float _brokeThroughAt;
    private float _passiveWarnedAt;

    // ── Movement ───────────────────────────────────────────────────────────────

    private void MoveAgents()
    {
        foreach (var a in _moving)
        {
            if (a.JumpUntil > 0f)
            {
                float t = Math.Clamp((Clock - a.JumpStart) / MathF.Max(0.05f, a.JumpUntil - a.JumpStart), 0f, 1f);
                var next = Vector2.Lerp(a.JumpFrom, a.JumpTo, t);
                a.Vel = (next - a.Pos) / Dt;
                a.Pos = next;
                continue;
            }
            if (a.DiveUntil > 0f)
            {
                float t = Math.Clamp((Clock - a.DiveStart) / MathF.Max(0.05f, a.DiveUntil - a.DiveStart), 0f, 1f);
                // Fast off the mark and slowing into the stretch, as a dive is.
                var next = Vector2.Lerp(a.DiveFrom, a.DiveTo, 1f - (1f - t) * (1f - t));
                a.Vel = (next - a.Pos) / Dt;
                a.Pos = next;
                continue;
            }
            if (a.StunnedUntil > Clock)
            {
                a.Vel *= 0.5f;
                a.Pos += a.Vel * Dt;
                continue;
            }

            var toTarget = a.Target - a.Pos;
            float dist = toTarget.Length();
            Vector2 desired = Vector2.Zero;
            if (dist > 0.05f)
            {
                // Ease into the spot rather than overshooting it.
                float speed = MathF.Min(MathF.Min(a.DesiredSpeed, a.MaxSpeed), MathF.Sqrt(2f * a.Turning * dist) * 0.9f);
                desired = toTarget / dist * speed;
            }

            var dv = desired - a.Vel;
            bool braking = Vector2.Dot(desired, a.Vel) < a.Vel.LengthSquared() * 0.5f;
            float maxDv = (braking ? a.Turning : a.Acceleration) * Dt;
            float dvLen = dv.Length();
            if (dvLen > maxDv) dv *= maxDv / dvLen;
            a.Vel += dv;
            a.Pos += a.Vel * Dt;
        }

        // Nobody stands in anybody else — except a defender on the ball, which is the point.
        for (int i = 0; i < _moving.Count; i++)
        {
            var a = _moving[i];
            if (!a.OnCourt) continue;
            for (int j = i + 1; j < _moving.Count; j++)
            {
                var b = _moving[j];
                if (!b.OnCourt) continue;
                var d = b.Pos - a.Pos;
                float len2 = d.LengthSquared();
                const float min = 0.55f;
                if (len2 >= min * min || len2 < 1e-6f) continue;
                float len = MathF.Sqrt(len2);
                var push = d / len * ((min - len) * 0.5f);
                a.Pos -= push;
                b.Pos += push;
            }
        }

        foreach (var a in _moving)
        {
            if (a.State != AgentState.OnCourt) continue;
            a.Pos = Court.Clamp(a.Pos, 0.15f);
            if (!a.IsKeeper && !a.Airborne && Clock >= a.LandedUntil) a.Pos = KeepOutOfAreas(a.Pos, Court.GoalAreaRadius);
        }
    }

    /// <summary>Moves a point out of both goal areas — the court player's line she may not cross.</summary>
    private Vector2 KeepOutOfAreas(Vector2 p, float radius)
    {
        foreach (var s in Sides)
        {
            var (u, w) = Frame(s, p);
            if (u < radius + 0.5f && Court.DistanceToGoalMouth(u, w) < radius)
            {
                (u, w) = Court.PushOutTo(u, w, radius);
                p = World(s, u, w);
            }
        }
        return p;
    }

    // ── Duels ──────────────────────────────────────────────────────────────────

    private static float AttackerDuelPower(Agent c)
    {
        var p = c.Player;
        return 0.30f * c.Eff(p.Dribbling) + 0.20f * c.Eff(p.Agility) + 0.15f * c.Eff(p.Balance)
             + 0.15f * c.Eff(p.Strength) + 0.10f * c.Eff(p.Pace) + 0.10f * c.Eff(p.Flair);
    }

    private static float DefenderDuelPower(Agent d)
    {
        var p = d.Player;
        return 0.30f * d.Eff(p.Tackling) + 0.15f * d.Eff(p.Marking) + 0.20f * d.Eff(p.Strength)
             + 0.15f * d.Eff(p.Positioning) + 0.10f * d.Eff(p.Anticipation) + 0.10f * d.Eff(p.Balance);
    }

    private void UpdateContact()
    {
        var c = Ball.Holder;
        if (Ball.State != BallState.Held || c == null || c.IsKeeper || c.JumpUntil > 0f) { _contactClock = 0; return; }

        Agent? d = null;
        float nearest = 1.0f;
        foreach (var o in Opp(Sides[c.Side]).Court)
        {
            if (o.IsKeeper || o.StunnedUntil > Clock) continue;
            float dist = Vector2.Distance(o.Pos, c.Pos);
            if (dist < nearest) { nearest = dist; d = o; }
        }
        if (d == null) { _contactClock = 0; return; }

        _contactClock += Dt;
        if (_contactClock < _t.DuelInterval) return;
        _contactClock = 0;

        var attack = Sides[c.Side];
        var defence = Sides[d.Side];
        var toGoal = World(attack, 0f, 0f) - c.Pos;
        float momentum = toGoal.LengthSquared() > 0.01f
            ? Math.Clamp(Vector2.Dot(c.Vel, Vector2.Normalize(toGoal)) / 5f, 0f, 1f)
            : 0f;

        // A back standing on the ball with a defender in her face is pressure, not a duel. It is
        // going at her — the drive, or running on to the ball — that asks the question.
        // A pivot is in a fight from the moment she catches it: her marker is already on her.
        bool attacking = c.Intent == "drive" || momentum > 0.3f || c.Role == "Pivot";
        if (!attacking)
        {
            if (Rand() < _t.StealChance * 0.1f * (d.Eff(d.Player.Anticipation) / 12f))
            {
                TurnoverCauses["stripped standing"] = TurnoverCauses.GetValueOrDefault("stripped standing") + 1;
                Emit(EventKind.Turnover, d.Side, d, c);
                GiveBall(d, newAttack: true, lookDelay: 0.3f);
            }
            return;
        }

        float edge = Sigmoid((AttackerDuelPower(c) - DefenderDuelPower(d)) / 2.5f);
        float aggression = defence.Tactics.Aggression switch
        {
            DefensiveAggression.Contain => 0.8f,
            DefensiveAggression.Aggressive => 1.25f,
            _ => 1f,
        };

        float pSteal = _t.StealChance * (1.5f - edge) * aggression * (d.Eff(d.Player.Anticipation) / 12f);
        float pOffensive = _t.OffensiveFoulChance * (0.5f + momentum);
        float pBreak = _t.BreakChance * (0.4f + 1.2f * edge) * (0.25f + momentum);
        float pStop = _t.StopChance * (1.4f - 0.8f * edge) * aggression;

        float r = Rand();
        if ((r -= pSteal) < 0)
        {
            TurnoverCauses["stripped driving"] = TurnoverCauses.GetValueOrDefault("stripped driving") + 1;
            Emit(EventKind.Turnover, d.Side, d, c);
            GiveBall(d, newAttack: true, lookDelay: 0.3f);
            return;
        }
        if ((r -= pOffensive) < 0)
        {
            TurnoverCauses["offensive foul"] = TurnoverCauses.GetValueOrDefault("offensive foul") + 1;
            Emit(EventKind.OffensiveFoul, d.Side, d, c);
            AwardFreeThrow(d.Side, c.Pos, null);
            return;
        }
        if ((r -= pBreak) < 0)
        {
            // Past her. The defender is left on the floor for a moment; the attacker is through.
            d.StunnedUntil = Clock + 0.5f + 0.4f * edge;
            _brokeThrough = c;
            _brokeThroughAt = Clock;
            if (toGoal.LengthSquared() > 0.01f)
            {
                var (u, w) = Frame(attack, c.Pos + Vector2.Normalize(toGoal) * 2.2f);
                (u, w) = Court.PushOutTo(u, w, Court.GoalAreaRadius + 0.2f);
                c.Target = World(attack, u, w);
                c.DesiredSpeed = c.MaxSpeed;
                c.Intent = "drive";
            }
            c.NextDecisionAt = Clock + 0.15f;
            return;
        }
        if ((r -= pStop) < 0)
        {
            // A clear chance is one she has made: past a defender a moment ago, or already
            // running in on goal with nobody left in front.
            bool justBroke = _brokeThrough == c && Clock - _brokeThroughAt < 1.5f;
            bool clear = (justBroke || (momentum > 0.5f && c.Intent == "drive")) && IsClearChance(c, exclude: d);
            var discipline = (defence.Team.Manager?.Discipline ?? 10) / 20f;
            float instruction = defence.Tactics.Aggression switch
            {
                DefensiveAggression.Contain => _t.SuspensionContain,
                DefensiveAggression.Aggressive => _t.SuspensionAggressive,
                _ => 1f,
            };
            float careful = d.Suspensions switch { 0 => 1f, 1 => _t.CarefulAfterOne, _ => _t.CarefulAfterTwo };
            float pSuspension = _t.SuspensionShare * instruction * careful * (0.5f + d.Eff(d.Player.Aggression) / 20f)
                                * (1.3f - 0.6f * discipline) * (1f + momentum) * (clear ? 2.5f : 1f);

            if (clear && Rand() < _t.SevenMetreShare)
            {
                if (Rand() < pSuspension) Suspend(d);
                AwardSevenMetre(c.Side);
                return;
            }

            Emit(EventKind.FreeThrow, c.Side, c, d);
            if (Rand() < pSuspension) Suspend(d);
            AwardFreeThrow(c.Side, c.Pos, c);
        }
    }

    /// <summary>
    /// Through on goal: close enough to shoot, going forward, and no defender left between her
    /// and the keeper. The seven-metre rule's "clear chance", as a court position.
    /// </summary>
    private bool IsClearChance(Agent c, Agent? exclude = null)
    {
        var attack = Sides[c.Side];
        var (u, w) = Frame(attack, c.Pos);
        if (Court.DistanceToGoalMouth(u, w) > 8.0f) return false;

        var goal = World(attack, 0f, 0f);
        var d = goal - c.Pos;
        float len2 = d.LengthSquared();
        foreach (var o in Opp(attack).Court)
        {
            if (o.IsKeeper || o == exclude || o.StunnedUntil > Clock) continue;
            float t = Vector2.Dot(o.Pos - c.Pos, d) / MathF.Max(len2, 0.01f);
            if (t <= 0f || t >= 1f) continue;
            if (Vector2.Distance(o.Pos, c.Pos + d * t) < 1.1f) return false;
        }
        return true;
    }

    private void AwardSevenMetre(int side)
    {
        var s = Sides[side];

        // The club's own penalty taker, whoever that is on court.
        Agent? taker = null;
        foreach (var a in s.Court)
            if (!a.IsKeeper && (taker == null || a.Player.SevenMeterTaking > taker.Player.SevenMeterTaking))
                taker = a;
        if (taker == null) return;

        Emit(EventKind.SevenMetreAwarded, side, taker);
        BeginRestart(RestartKind.SevenMetre, side, World(s, Court.SevenMetreMark, 0f), taker,
            2.5f + Rand(), newAttack: false);
        AwaitingTaker = AskForSevenMetreTaker && side == Setup.UserSide;
        // Against the managed side: which keeper faces it — worth asking only with one on the bench.
        AwaitingKeeper = AskForSevenMetreTaker && side == 1 - Setup.UserSide && Setup.UserSide >= 0
                         && SevenMetreKeepers(Setup.UserSide).Skip(1).Any();
    }

    // ── Passive play ───────────────────────────────────────────────────────────

    private void UpdatePassivePlay()
    {
        if (Possession < 0 || Ball.State is BallState.Shot or BallState.Loose) return;

        if (!PassiveWarned)
        {
            if (Clock - _attackStartedAt < _passiveWarnAfter) return;
            PassiveWarned = true;
            _passiveWarnedAt = Clock;
            _passesSinceWarning = 0;
            Emit(EventKind.PassiveWarning, Possession, Ball.Holder);
            return;
        }

        // The arm has been up too long.
        if (Clock - _passiveWarnedAt > 20f && Ball.State == BallState.Held)
        {
            Emit(EventKind.PassivePlay, Possession, Ball.Holder);
            AwardFreeThrow(1 - Possession, Ball.Pos, null);
        }
    }
}
