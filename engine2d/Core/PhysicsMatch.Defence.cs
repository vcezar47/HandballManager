using System.Numerics;
using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// The defence: a shape held around the goal area, slid across with the ball, and one defender
/// at a time stepping out to meet whoever has it. And the keeper, cutting down the angle.
/// </summary>
/// <remarks>
/// The systems are literally shapes. A 6-0 is six bodies on the six-metre line, which is why it
/// gives the backs room to shoot over it; a 3-2-1 pushes three of them up to nine and ten metres
/// to take the ball off the backs, which is why the wings and the pivot find space behind; a
/// 5-1 sends one out onto the playmaker. The aggression setting is how far the defender dares to
/// step out, and so how often she ends up in a duel — and how many of those end in two minutes.
/// </remarks>
public sealed partial class PhysicsMatch
{
    /// <summary>The order the defenders line up in, from the attack's left to its right.</summary>
    private static readonly string[] DefenceOrder = ["RW", "RB", "CB", "Pivot", "LB", "LW"];

    private static readonly (float U, float W)[] SixZero = [(3.0f, 7.2f), (5.8f, 4.3f), (6.6f, 1.3f), (6.6f, -1.3f), (5.8f, -4.3f), (3.0f, -7.2f)];
    private static readonly (float U, float W)[] FiveOne = [(3.0f, 7.2f), (6.0f, 3.8f), (9.6f, 0.3f), (6.5f, -0.3f), (6.0f, -3.8f), (3.0f, -7.2f)];
    private static readonly (float U, float W)[] ThreeTwoOne = [(3.2f, 7.0f), (8.3f, 4.2f), (10.4f, 0.3f), (6.5f, -0.3f), (8.3f, -4.2f), (3.2f, -7.0f)];
    private static readonly (float U, float W)[] FiveZero = [(3.0f, 7.2f), (6.0f, 3.8f), (6.5f, 0f), (6.0f, -3.8f), (3.0f, -7.2f)];
    private static readonly (float U, float W)[] FourZero = [(4.5f, 5.5f), (6.5f, 1.8f), (6.5f, -1.8f), (4.5f, -5.5f)];
    private static readonly (float U, float W)[] ThreeZero = [(6.2f, 3.5f), (6.5f, 0f), (6.2f, -3.5f)];

    private readonly List<Agent> _defenders = new(7);
    private readonly int[] _slotOf = new int[8];

    /// <summary>The defending side's court players in line-up order.</summary>
    private List<Agent> OrderedDefenders(TeamSide s)
    {
        _defenders.Clear();
        foreach (var a in s.Court)
        {
            if (a.IsKeeper) continue;
            // Insertion sort: six players, every tick — no delegate, no allocation.
            int r = Rank(a.Role), i = _defenders.Count;
            _defenders.Add(a);
            while (i > 0 && Rank(_defenders[i - 1].Role) > r) { _defenders[i] = _defenders[i - 1]; i--; }
            _defenders[i] = a;
        }
        return _defenders;

        static int Rank(string role) { int i = Array.IndexOf(DefenceOrder, role); return i < 0 ? 2 : i; }
    }

    /// <summary>The man-marker in a man-marking defence: whoever marks best.</summary>
    private static Agent? Marker(List<Agent> defenders)
    {
        Agent? best = null;
        foreach (var d in defenders)
            if (best == null || d.Player.Marking > best.Player.Marking) best = d;
        return best;
    }

    /// <summary>The attacker a man-marking defence takes out of the game: their best.</summary>
    private static Agent? MarkedAttacker(TeamSide attacking)
    {
        Agent? best = null;
        foreach (var a in attacking.Court)
            if (!a.IsKeeper && (best == null || a.Player.Overall100 > best.Player.Overall100)) best = a;
        return best;
    }

    private (float U, float W)[] Shape(TeamSide s, int count, out int advanced)
    {
        advanced = -1;
        if (count >= 6)
        {
            switch (s.Tactics.System)
            {
                case DefensiveSystem.FiveOne: advanced = 2; return FiveOne;
                case DefensiveSystem.ThreeTwoOne: advanced = 2; return ThreeTwoOne;
                case DefensiveSystem.ManMarking: return FiveZero; // plus the marker
                default: return SixZero;
            }
        }
        return count switch { 5 => FiveZero, 4 => FourZero, _ => ThreeZero };
    }

    /// <summary>A defender's place in the shape with nobody on the ball — used to line up for a throw-off.</summary>
    private (float U, float W) DefenceHome(TeamSide s, Agent a)
    {
        var list = OrderedDefenders(s);
        int i = list.IndexOf(a);
        var shape = Shape(s, list.Count, out _);
        if (i < 0) return (6.5f, 0f);
        return shape[Math.Min(i, shape.Length - 1)];
    }

    private float StepOutLimit(TeamSide s, bool advancedSlot)
    {
        float limit = StepOutDistance(s.Tactics.Aggression);
        return advancedSlot ? limit + 1.8f : limit;
    }

    /// <summary>How far from goal a defender will step out to meet the ball, by aggression.</summary>
    public static float StepOutDistance(DefensiveAggression aggression) => aggression switch
    {
        DefensiveAggression.Contain => 8.0f,
        DefensiveAggression.Aggressive => 10.6f,
        _ => 9.3f,
    };

    /// <summary>
    /// A full defence's shape for a system, from the attack's left to its right — the shape the
    /// engine plays, which the tactics board draws so the picture and the match cannot differ.
    /// Man-marking is the five-man wall; the marker is wherever their best player goes.
    /// </summary>
    public static IReadOnlyList<(float U, float W)> DefenceFormation(DefensiveSystem system) => system switch
    {
        DefensiveSystem.FiveOne => FiveOne,
        DefensiveSystem.ThreeTwoOne => ThreeTwoOne,
        DefensiveSystem.ManMarking => FiveZero,
        _ => SixZero
    };

    private void PlanDefence(TeamSide s)
    {
        var attacking = Opp(s);
        var list = OrderedDefenders(s);
        if (list.Count == 0) return;
        float intensity = Intensity(s);

        var (bu, bw) = Frame(attacking, Ball.Pos);

        // Who the defence is reacting to: the player on the ball, or where a pass is going once
        // they have read it.
        Vector2 focus = Ball.Pos;
        if (Ball.State == BallState.Pass && Ball.Elapsed > 0.2f) focus = Ball.To;
        var (fu, fw) = Frame(attacking, focus);
        bool focusThreat = Ball.State is BallState.Held or BallState.Pass && Possession == attacking.Index
                           && Court.DistanceToGoalMouth(fu, fw) < 13f;

        Agent? marker = null, marked = null;
        bool manMarking = s.Tactics.System == DefensiveSystem.ManMarking && list.Count >= 6;
        if (manMarking)
        {
            marker = Marker(list);
            marked = MarkedAttacker(attacking);
        }

        var shape = Shape(s, manMarking ? list.Count - 1 : list.Count, out int advanced);

        // Slots, slid towards the ball.
        Span<float> su = stackalloc float[7];
        Span<float> sw = stackalloc float[7];
        int n = 0;
        var slotOf = _slotOf;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == marker) { slotOf[i] = -1; continue; }
            var (u, w) = shape[Math.Min(n, shape.Length - 1)];
            bool adv = n == advanced || u > 7.5f;
            w += (adv ? 0.45f : 0.28f) * (bw - w);
            if (adv && bu < 13f) u = Math.Clamp(bu - 1.4f, 8f, 11f);
            w = Math.Clamp(w, -8.5f, 8.5f);
            su[n] = u; sw[n] = w;
            slotOf[i] = n++;
        }

        // The one who steps out: whoever's slot is nearest the ball.
        int presser = -1;
        if (focusThreat && Phase == MatchPhase.Play)
        {
            float best = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                if (slotOf[i] < 0 || list[i].StunnedUntil > Clock) continue;
                var slotPos = World(attacking, su[slotOf[i]], sw[slotOf[i]]);
                float d = Vec.Dist(slotPos, focus);
                if (d < best) { best = d; presser = i; }
            }
        }

        // When a line defender steps out, her neighbours slide in behind her: the wall closes the
        // hole she left rather than leaving the shooter a window through the middle of it.
        if (presser >= 0 && slotOf[presser] >= 0 && su[slotOf[presser]] < 7.5f)
        {
            float gapW = sw[slotOf[presser]];
            for (int i = 0; i < list.Count; i++)
            {
                if (i == presser || slotOf[i] < 0 || su[slotOf[i]] > 7.5f) continue;
                float away = MathF.Abs(sw[slotOf[i]] - gapW);
                if (away < 4f) sw[slotOf[i]] += (gapW - sw[slotOf[i]]) * _t.WallClosing * (1f - away / 4f);
            }
        }

        // Help: when the ball comes inside nine metres, the nearest line defender beside the
        // presser slides across to close the gap she would run into. That is what makes a
        // breakthrough hard — beating one defender only brings you face to face with the next.
        int helper = -1;
        if (presser >= 0 && Court.DistanceToGoalMouth(fu, fw) < 9.5f)
        {
            float best = 4.5f;
            for (int i = 0; i < list.Count; i++)
            {
                if (i == presser || slotOf[i] < 0 || su[slotOf[i]] > 7.5f || list[i].StunnedUntil > Clock) continue;
                float d = Vec.Dist(list[i].Pos, focus);
                if (d < best) { best = d; helper = i; }
            }
        }

        // The attacking pivot, for the defender behind whom she is hiding.
        Agent? pivot = null;
        foreach (var a in attacking.Court) if (a.Role == "Pivot") pivot = a;

        int pivotMarker = pivot == null ? -1 : PivotMarker(list, slotOf, su, sw, attacking, pivot, presser, helper);

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            Vector2 target;
            float speed;

            if (Ball.Interceptor == d && Ball.State == BallState.Pass)
            {
                target = Ball.From + (Ball.To - Ball.From) * Ball.InterceptAt;
                speed = d.MaxSpeed;
            }
            else if (d == marker && marked != null)
            {
                // A metre from her, on the goal side, wherever she goes.
                target = GoalSide(attacking, marked.Pos, 1.0f);
                speed = d.MaxSpeed;
            }
            else if (i == presser)
            {
                bool adv = slotOf[i] == advanced || su[slotOf[i]] > 7.5f;
                target = GoalSide(attacking, focus, 0.9f);
                var (tu, tw) = Frame(attacking, target);
                float mouth = Court.DistanceToGoalMouth(tu, tw);
                float limit = StepOutLimit(s, adv);
                if (mouth > limit)
                {
                    // Not that far: wait for her at the limit of the step.
                    float k = limit / mouth;
                    target = World(attacking, tu * k, tw * k + (1 - k) * 0f);
                }
                speed = d.MaxSpeed;
            }
            else if (i == helper)
            {
                // Along the line, into the lane — never out of it, or the gap behind opens.
                var lane = GoalSide(attacking, focus, 1.6f);
                var (hu, hw) = Frame(attacking, lane);
                (hu, hw) = Court.PushOutTo(hu, Math.Clamp(hw, sw[slotOf[i]] - 2.5f, sw[slotOf[i]] + 2.5f), Court.GoalAreaRadius + 0.3f);
                target = World(attacking, MathF.Min(hu, 7.2f), hw);
                speed = d.MaxSpeed;
            }
            else
            {
                float u = su[slotOf[i]], w = sw[slotOf[i]];
                target = World(attacking, u, w);

                // The defender the pivot is leaning on keeps a body on her.
                if (pivot != null && i == pivotMarker)
                    target = GoalSide(attacking, pivot.Pos, 0.45f);

                float far = Vec.Dist(d.Pos, target);
                speed = far > 5f ? d.MaxSpeed : 2.2f + 2.4f * intensity;
            }

            var (gu, gw) = Frame(attacking, target);
            (gu, gw) = Court.PushOutTo(gu, gw, Court.GoalAreaRadius + 0.05f);

            // Restart distances: three metres from a free throw, behind nine for a seven-metre.
            if (Phase == MatchPhase.Restart)
            {
                if (Restart == RestartKind.SevenMetre) (gu, gw) = Court.PushOutTo(gu, gw, Court.FreeThrowRadius + 0.3f);
                var spot = Frame(attacking, _restartSpot);
                float du = gu - spot.U, dw = gw - spot.W;
                float dd = MathF.Sqrt(du * du + dw * dw);
                if (dd < 3f && Restart != RestartKind.ThrowOff)
                {
                    if (dd < 0.01f) { du = -1f; dw = 0f; dd = 1f; }
                    gu = spot.U + du / dd * 3f; gw = spot.W + dw / dd * 3f;
                    (gu, gw) = Court.PushOutTo(gu, gw, Court.GoalAreaRadius + 0.05f);
                }
            }

            d.Target = Court.Clamp(World(attacking, gu, gw));
            d.DesiredSpeed = speed;
            d.Intent = i == presser ? "press" : "";
        }
    }

    /// <summary>The line defender whose slot is nearest the pivot — the one who has to live with her.</summary>
    private int PivotMarker(List<Agent> list, int[] slotOf, Span<float> su, Span<float> sw, TeamSide attacking, Agent pivot, int presser, int helper)
    {
        int best = -1;
        float bestD = 2.8f;
        for (int i = 0; i < list.Count; i++)
        {
            if (i == presser || i == helper || slotOf[i] < 0 || su[slotOf[i]] > 7.5f) continue;
            float d = Vec.Dist(World(attacking, su[slotOf[i]], sw[slotOf[i]]), pivot.Pos);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>A point <paramref name="gap"/> metres from <paramref name="p"/>, between her and the goal.</summary>
    private static Vector2 GoalSide(TeamSide attacking, Vector2 p, float gap)
    {
        var goal = World(attacking, 0f, 0f);
        var d = goal - p;
        if (Vec.LenSq(d) < 0.01f) return p;
        return p + Vec.Norm(d) * gap;
    }

    // ── The keeper ─────────────────────────────────────────────────────────────

    private void PlanKeeper(TeamSide s)
    {
        var k = s.Keeper;
        if (k == null || Ball.Holder == k) return;
        if (k == _retriever && Phase == MatchPhase.Restart && Restart == RestartKind.ThrowOff && _throwOffStage == ThrowOffStage.Retrieving) return;
        var attacking = Opp(s);

        if (Possession == s.Index && Ball.State != BallState.Loose)
        {
            // Her side has it: a few metres off her line, ready for the ball back.
            k.Target = World(attacking, 3f, 0f);
            k.DesiredSpeed = 3f;
            return;
        }

        // Committed: once the shot is away she cannot start again.
        if (Ball.State == BallState.Shot) { k.Target = k.Pos; k.DesiredSpeed = 0f; return; }

        // A loose ball in her own area is hers to collect.
        if (Ball.State == BallState.Loose)
        {
            var (lu, lw) = Frame(attacking, Ball.Pos);
            if (Court.InGoalArea(lu, lw)) { k.Target = Ball.Pos; k.DesiredSpeed = k.MaxSpeed; return; }
        }

        // She moves when she has read it, not the instant it happens.
        if (Clock < k.NextDecisionAt) return;
        k.NextDecisionAt = Clock + 0.30f - 0.15f * k.Eff(k.Player.Anticipation) / 20f;

        // How far she comes off the bisector's foot, towards the ball.
        float depth;
        var holder = Ball.Holder;
        if (Phase == MatchPhase.Restart && Restart == RestartKind.SevenMetre) depth = 1.0f;
        else if (holder != null && holder.Side != s.Index && IsClearChance(holder))
            depth = 1.3f + 0.7f * k.Eff(k.Player.OneOnOnes) / 20f;
        else depth = KeeperDepth(k);

        // Where she thinks the middle of the angle is: a good keeper is rarely a stride off it.
        if (Rand() < 0.2f) k.Wobble = Gauss() * 0.35f * (1f - k.Eff(k.Player.Positioning) / 20f);

        k.Target = KeeperSpot(s, Ball.Pos, depth, k.Wobble);
        k.DesiredSpeed = 4f;
        // Coming on for a seven-metre, or back into an empty goal for one: she runs.
        if (Phase == MatchPhase.Restart && Restart == RestartKind.SevenMetre && Vec.Dist(k.Pos, k.Target) > 2f)
            k.DesiredSpeed = k.MaxSpeed;
    }

    private static float KeeperDepth(Agent k) => 0.8f + 0.3f * k.Eff(k.Player.Positioning) / 20f;

    /// <summary>
    /// Where a keeper of <paramref name="defending"/> stands for a ball at <paramref name="ball"/>:
    /// on the bisector of the angle the posts make with it, <paramref name="depth"/> metres off
    /// its foot. From a wing that line runs almost along the goal, so the same step takes her to
    /// the near post — which is exactly where a keeper stands against a wing.
    /// </summary>
    private Vector2 KeeperSpot(TeamSide defending, Vector2 ball, float depth, float wobble = 0f)
    {
        var attacking = Opp(defending);
        var (bu, bw) = Frame(attacking, ball);
        bu = MathF.Max(bu, 0.3f);

        // From a tight angle a keeper does not come off her line: she stands on it, against the
        // near post, and leaves the far corner as the shooter's gamble.
        if (MathF.Abs(bw) > 3.5f && bu < MathF.Abs(bw) * 0.9f)
        {
            float post = MathF.Sign(bw) * (Court.PostHalf - 0.35f);
            return World(attacking, 0.25f, post + wobble * 0.5f);
        }
        float d1 = MathF.Sqrt(bu * bu + (bw - Court.PostHalf) * (bw - Court.PostHalf));
        float d2 = MathF.Sqrt(bu * bu + (bw + Court.PostHalf) * (bw + Court.PostHalf));
        float xw = Court.PostHalf - Court.GoalWidth * d1 / (d1 + d2);
        var toBall = new Vector2(bu, bw - xw);
        float len = Vec.Len(toBall);
        if (len > 0.01f) toBall /= len;
        float ku = toBall.X * depth, kw = xw + toBall.Y * depth + wobble;
        return World(attacking, MathF.Max(0.2f, ku), Math.Clamp(kw, -2.5f, 2.5f));
    }

    /// <summary>
    /// Where the keeper will be by the time a player receiving at <paramref name="at"/> could
    /// shoot: heading for her spot for that ball, as far as her feet get her in the time.
    /// Passers read the keeper this way — not where she is standing now, which from the far
    /// wing always looks like an open goal.
    /// </summary>
    private Vector2? KeeperWhenShooting(Agent shooter, Vector2 at, float seconds)
    {
        var defending = Opp(Sides[shooter.Side]);
        var k = defending.Keeper;
        if (k == null) return null;
        var spot = KeeperSpot(defending, at, KeeperDepth(k));
        var gap = spot - k.Pos;
        float reach = k.MaxSpeed * 0.8f * seconds;
        float d = Vec.Len(gap);
        return d <= reach || d < 0.01f ? spot : k.Pos + gap / d * reach;
    }

    // ── Loose balls and the bench ──────────────────────────────────────────────

    private void PlanLooseBallChase()
    {
        if (Ball.State != BallState.Loose) return;
        var aim = Ball.Pos + Ball.Vel * 0.3f;
        foreach (var s in Sides)
        {
            // The nearest two go for it; everybody else holds their shape.
            Agent? first = null, second = null;
            float f = float.MaxValue, sc = float.MaxValue;
            foreach (var a in s.Court)
            {
                if (a.IsKeeper) continue;
                float d = Vec.DistSq(a.Pos, aim);
                if (d < f) { second = first; sc = f; first = a; f = d; }
                else if (d < sc) { second = a; sc = d; }
            }
            foreach (var a in new[] { first, second })
            {
                if (a == null) continue;
                a.Target = KeepOutOfAreas(Court.Clamp(aim), Court.GoalAreaRadius);
                a.DesiredSpeed = a.MaxSpeed;
            }
        }
    }

    private void PlanOffCourt()
    {
        foreach (var s in Sides)
        {
            foreach (var a in s.Squad)
            {
                a.OnTouchline = false;
                switch (a.State)
                {
                    case AgentState.Bench when a.IsKeeperByTrade && HasExtraAttacker(s.Index):
                        a.Pos = SubPoint(s) + new Vector2(0f, -0.6f);
                        a.Vel = Vector2.Zero;
                        a.OnTouchline = true;
                        break;
                    case AgentState.Bench:
                        WalkOffCourt(s, a);
                        break;
                    case AgentState.Injured when a.StunnedUntil > Clock:
                        a.Target = a.Pos;
                        break;
                    case AgentState.Injured:
                        a.Target = BenchPoint(s, BenchSlot(s, a));
                        a.DesiredSpeed = 1.5f;
                        break;
                    case AgentState.Suspended:
                    case AgentState.SentOff:
                        a.Target = BenchPoint(s, BenchSlot(s, a));
                        a.DesiredSpeed = 3f;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// The bench is not part of the physics — nobody there can touch the ball — so its walks
    /// are stepped here: a replacement out to the line to wait for the player she replaces, and
    /// the player who came off back to her seat.
    /// </summary>
    private void WalkOffCourt(TeamSide s, Agent a)
    {
        int queue = s.Changes.FindIndex(c => c.Entering == a);
        var target = queue >= 0 ? WaitingSpot(s, queue) : BenchPoint(s, BenchSlot(s, a));
        float speed = queue >= 0 ? 2.4f : 1.6f;
        float d = Vec.Dist(a.Pos, target);

        // The ends change at half-time and the benches with them: nobody parades across the court.
        bool otherEnd = (a.Pos.X - Court.CentreX) * (target.X - Court.CentreX) < 0f;
        if (queue < 0 && (d > 8f || otherEnd)) { a.Pos = target; d = 0f; }
        else if (d > 0.01f) a.Pos += (target - a.Pos) * MathF.Min(1f, speed * Dt / d);
        a.Vel = Vector2.Zero;
        a.OnTouchline = queue >= 0 || d > 0.25f;
    }
}
