using System.Numerics;
using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// The attack: the player on the ball choosing what to do with it, and the other five or six
/// getting into the places she might want to find them.
/// </summary>
/// <remarks>
/// The ball carrier prices every option in the one currency — goals — and takes the best, with
/// a margin of error that shrinks as her Decisions improve. A shot is worth what the shot model
/// says; a pass is worth what the receiver could do with it, discounted by the chance it never
/// arrives; driving at the defence is worth the shot she would get if she got through; keeping
/// the ball is worth what an attack usually produces, which falls as the attack gets old and
/// the referee starts looking at his arm. The tactics sheet tilts those prices, it does not
/// replace them: "play through the wings" makes a wing worth a little more to pass to, and a
/// defence that has closed the wings still makes it a bad pass.
/// </remarks>
public sealed partial class PhysicsMatch
{
    private static readonly string[] AttackOrder = ["LW", "LB", "CB", "RB", "RW"];

    /// <summary>What keeping the ball is worth at this point of the attack, in goals.</summary>
    private float KeepValue(TeamSide s)
    {
        float age = Clock - _attackStartedAt;

        // Three stages. Straight after winning the ball, with the defence not yet back, anything
        // decent is worth taking. Against a set defence the first stretch is for getting into
        // shape and moving the ball — only a real chance is worth shooting. Then the attack is
        // ripe, and after that it starts to go stale as the referee's arm comes into play.
        float setup = s.Tactics.Tempo switch
        {
            AttackTempo.Counter => _t.SetupCounter,
            AttackTempo.Controlled => _t.SetupControlled,
            _ => _t.SetupBalanced,
        };
        float v;
        if (age < 6f && DefendersBack(s) < 3) v = _t.KeepValueBreak;
        else if (age < setup) v = _t.KeepValueSetup;
        else if (age < 36f) v = _t.KeepValueEarly;
        else if (age > 55f) v = _t.KeepValueLate;
        else v = _t.KeepValueEarly + (_t.KeepValueLate - _t.KeepValueEarly) * (age - 36f) / 19f;

        if (s.Tactics.Tempo == AttackTempo.Controlled) v += 0.03f;
        if (s.Tactics.PlayForTime && !PassiveWarned) v += 0.25f;
        // The arm is up: a shot, any shot, is better than handing it over.
        if (PassiveWarned) v = MathF.Min(v, 0.16f - 0.04f * _passesSinceWarning);
        return v;
    }

    private void ThinkCarrier(Agent c)
    {
        // In the air: she lets it go at the top of the jump.
        if (c.JumpUntil > 0f)
        {
            if (Clock >= c.JumpUntil)
            {
                TakeShot(c, sevenMetre: false);
                c.JumpUntil = 0f;
                c.Airborne = false;
                c.LandedUntil = Clock + 1.2f;
            }
            return;
        }
        if (Clock < c.NextDecisionAt) return;
        c.NextDecisionAt = Clock + c.DecisionInterval * (0.8f + 0.4f * Rand());

        var s = Sides[c.Side];
        var p = c.Player;
        var (u, w) = Frame(s, c.Pos);
        float keep = KeepValue(s);
        float temperature = 0.03f + 0.05f * (1f - c.Eff(p.Decisions) / 20f) + 0.03f * c.Fatigue;
        float flair = c.Eff(p.Flair) / 20f;
        var focus = s.Tactics.Focus;

        // ── Shoot ──
        float bestValue = float.MinValue;
        int bestKind = 0; // 0 keep, 1 shoot, 2 pass, 3 drive
        Agent? bestTarget = null;
        Vector2 driveTo = default;

        float mouth = Court.DistanceToGoalMouth(u, w);
        bool openNet = GoalIsOpen(s) && mouth < 38f;
        if (openNet)
        {
            // Nobody in the goal: from anywhere, by anyone — a keeper's throw the length of the court included.
            Consider(EstimateXg(c, c.Pos) + 0.1f, 1, null, default);
        }
        else if (!c.IsKeeper && mouth < 12.5f)
        {
            float xg = EstimateXg(c, c.Pos) * ShotAppetite(c) * Usage(c);
            if (mouth > 8.5f)
            {
                if (focus == AttackFocus.LongShots) xg += 0.07f;
                xg += 0.03f * flair;
            }
            if (focus == AttackFocus.Playmaker && c.Role == "CB") xg += 0.04f;
            if (focus == AttackFocus.Wings && (c.Role is "LW" or "RW")) xg += 0.04f;
            Consider(xg, 1, null, default);
        }

        // ── Pass ──
        float vision = 0.55f + 0.45f * c.Eff(p.Vision) / 20f;
        foreach (var t in s.Court)
        {
            if (t == c || t.IsKeeper) continue;
            if (Rand() > vision) continue; // she did not see her

            float success = PassSuccessEstimate(c, t);
            if (success < 0.2f) continue;
            var arrive = t.Pos + t.Vel * 0.4f;
            var (tu, tw) = Frame(s, arrive);
            float tMouth = Court.DistanceToGoalMouth(tu, tw);
            float length = Vector2.Distance(c.Pos, arrive);
            float flight = length / (_t.PassSpeedBase + _t.PassSpeedSpan * c.Eff(p.Passing) / 20f);
            float onward = tMouth < 12.5f
                ? EstimateXg(t, arrive, keeperAt: KeeperWhenShooting(t, arrive, flight + t.DecisionInterval)) * ReceiveDiscount(t) * Usage(t)
                : 0f;
            float value = MathF.Max(onward, keep * RoleKeepShare(t.Role, tMouth));

            value *= focus switch
            {
                AttackFocus.Wings when t.Role is "LW" or "RW" => 1.12f,
                AttackFocus.Pivot when t.Role == "Pivot" => 1.18f,
                AttackFocus.Playmaker when t.Role == "CB" => 1.10f,
                _ => 1f,
            };

            // Long balls to a runner are what a counter-attacking side lives on.
            bool breaking = Clock - _attackStartedAt < 6f && DefendersBack(s) < 4;
            if (s.Tactics.Tempo == AttackTempo.Counter && tu < 12f && breaking) value *= 1.1f;

            // Against a set defence the ball goes round the wall a pass at a time. A long throw
            // gives every defender time to read it and the receiver's marker time to arrive; a
            // cross-court ball skipping two team-mates is the exception, not the pattern.
            // A keeper's outlet is long by nature and exempt.
            if (!breaking && length > 9f && !c.IsKeeper) value *= MathF.Max(0.25f, 1f - (length - 9f) / 10f);

            float total = success * value - (1f - success) * 0.3f;
            Consider(total, 2, t, default);
        }

        // ── A wing's run-in: from the corner along the line to where she can take off ──
        if (c.Role is "LW" or "RW" && MathF.Abs(w) > 5f && u < 4f)
        {
            float side = MathF.Sign(w);
            var takeOff = World(s, 6.4f * MathF.Sin(0.55f), side * (Court.PostHalf + 6.4f * MathF.Cos(0.55f)));
            float space = LaneSpace(c, takeOff);
            float through = Sigmoid((space - 0.8f) / 0.35f);
            float xgThere = EstimateXg(c, takeOff) * ShotAppetite(c) * Usage(c);
            float value = through * xgThere * 0.95f + (1f - through) * keep * 0.85f;
            if (focus == AttackFocus.Wings) value += 0.04f;
            Consider(value, 3, null, takeOff);
        }

        // ── Drive at the defence ──
        if (!c.IsKeeper && u < 14f && u > 3f)
        {
            for (int k = -1; k <= 1; k++)
            {
                var toGoal = World(s, 0f, Math.Clamp(w * 0.5f, -1.5f, 1.5f)) - c.Pos;
                if (toGoal.LengthSquared() < 0.01f) continue;
                var dir = Rotate(Vector2.Normalize(toGoal), k * 0.45f);
                var point = c.Pos + dir * 2.4f;
                var (pu, pw) = Frame(s, point);
                (pu, pw) = Court.PushOutTo(pu, pw, Court.GoalAreaRadius + 0.2f);
                point = World(s, pu, pw);

                float space = LaneSpace(c, point);
                float through = Sigmoid((space - 0.9f) / 0.35f) * (0.6f + 0.4f * DuelEdge(c));
                float xgThere = Court.DistanceToGoalMouth(pu, pw) < 12.5f ? EstimateXg(c, point) * Usage(c) : 0f;
                // Stopped is not free: the attack restarts from nine metres with the clock running,
                // and a run into a set defender is how the ball gets stripped.
                float value = through * xgThere * 0.92f + (1f - through) * keep * 0.8f + 0.02f * flair;
                if (focus == AttackFocus.Playmaker && c.Role == "CB") value += 0.03f;
                Consider(value, 3, null, point);
            }
        }

        // ── The Kempa: a called play, so only now and then on the table at all ──
        if (Phase == MatchPhase.Play && DefendersBack(s) >= 4 && Rand() < _t.KempaCallChance)
        {
            var partner = KempaPartner(c);
            if (partner != null) Consider(KempaSuccess(c, partner) + 0.12f, 4, partner, default);
        }

        // ── Keep it ──
        // Three seconds is all the rules allow without bouncing it, and a back who stands on
        // the ball lets the defence settle. Holding is worth less the longer she has held.
        float held = Clock - c.HasBallSince;
        float holdShare = held < 1.2f ? 1f : MathF.Max(0.3f, 1f - (held - 1.2f) * 0.4f);

        // A keeper with the ball gets rid of it: the outlet is the start of the next attack.
        if (c.IsKeeper) holdShare = held < 1f ? 0.35f : 0.1f;

        // And never for long: after a couple of seconds she plays the best ball she has, whatever
        // it is worth — a keeper does not stand in her area waiting for the passive-play arm.
        float keepValue = c.IsKeeper && held > 2.5f ? -1f : keep * holdShare;
        Consider(keepValue, 0, null, default);

        // A keeper past her couple of seconds with nothing left on the table: the safest ball out.
        if (c.IsKeeper && held > 2.5f && bestKind == 0)
        {
            var outlet = s.Court.Where(t => t != c && !t.IsKeeper).OrderByDescending(t => PassSuccessEstimate(c, t)).FirstOrDefault();
            if (outlet != null) { bestKind = 2; bestTarget = outlet; }
        }
        if (c.IsKeeper && Clock - c.HasBallSince > LongestKeeperHold) LongestKeeperHold = Clock - c.HasBallSince;

        if (Trace != null)
            Trace.Add(new DecisionTrace(Clock, c.Role, Frame(s, c.Pos), keep, _options.ToArray(), bestKind, bestTarget?.Role));
        _options.Clear();

        switch (bestKind)
        {
            case 1:
                StartShot(c);
                break;
            case 2:
                StartPass(c, bestTarget!);
                break;
            case 4:
                StartKempa(c, bestTarget!);
                break;
            case 3:
                c.Intent = "drive";
                c.Target = driveTo;
                c.DesiredSpeed = c.MaxSpeed;
                break;
            default:
                c.Intent = "hold";
                break;
        }

        void Consider(float value, int kind, Agent? target, Vector2 point)
        {
            // Gumbel noise: a soft arg-max, sharper for the players who read the game well. The
            // noise belongs to her read of this possession, so it is the same every time she
            // looks at the same option — only a sliver of it is fresh.
            int key = Mix(c.ReadSeed, kind, target?.Id ?? 0, (int)(point.X * 2), (int)(point.Y * 2));
            float g = 0.85f * Gumbel(key) + 0.15f * -MathF.Log(-MathF.Log(MathF.Max(1e-6f, Rand())));
            float v = value + temperature * g;
            if (Trace != null) _options.Add((kind, target?.Role ?? "", value, v));
            if (v <= bestValue) return;
            bestValue = v;
            bestKind = kind;
            bestTarget = target;
            driveTo = point;
        }
    }

    /// <summary>The longest a keeper has stood with the ball this match, seconds — for the tests.</summary>
    public float LongestKeeperHold { get; private set; }

    /// <summary>Every carrier decision with the options she weighed, when a calibration run asks for it.</summary>
    public List<DecisionTrace>? Trace { get; set; }
    private readonly List<(int Kind, string Target, float Value, float Noisy)> _options = [];

    /// <summary>
    /// How much of a team-mate's shot survives her having to catch it first. A back running on to
    /// the ball shoots in the same stride; a pivot has to turn and a wing to gather and take off.
    /// </summary>
    private static float ReceiveDiscount(Agent receiver) => receiver.Role switch
    {
        "LB" or "CB" or "RB" => 0.95f,
        "LW" or "RW" => 0.9f,
        _ => 0.85f,
    };

    /// <summary>
    /// She shoots: a take-off first — a wing diving in, a back jumping forward — and the throw
    /// at the end of it. Standing shots and open-net throws go straight away.
    /// </summary>
    private void StartShot(Agent c)
    {
        var plan = PlanShot(c, c.Pos, sevenMetre: false);
        float d = Vector2.Distance(plan.Release, c.Pos);
        if (d < 0.4f || GoalIsOpen(Sides[c.Side]))
        {
            TakeShot(c, sevenMetre: false);
            return;
        }
        c.Airborne = true;
        c.JumpFrom = c.Pos;
        c.JumpTo = plan.Release;
        c.JumpStart = Clock;
        c.JumpUntil = Clock + Math.Clamp(d / 6.5f, 0.2f, 0.55f);
        c.Intent = "shoot";
    }

    /// <summary>
    /// A fixed mix of five numbers. Not <c>HashCode.Combine</c>, which .NET seeds differently in
    /// every process on purpose — that made the same seed play a different match each run.
    /// </summary>
    private static int Mix(int a, int b, int c, int d, int e)
    {
        uint h = 2166136261u;
        foreach (int v in (ReadOnlySpan<int>)[a, b, c, d, e]) { h ^= (uint)v; h *= 16777619u; h ^= h >> 13; }
        return (int)h;
    }

    /// <summary>A Gumbel draw fixed by <paramref name="key"/>: the same key, the same noise.</summary>
    private static float Gumbel(int key)
    {
        uint h = (uint)key * 2654435761u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        float u = ((h & 0xFFFFFF) + 0.5f) / 16777216f;
        return -MathF.Log(-MathF.Log(u));
    }

    /// <summary>
    /// How keen she is to shoot rather than make the play: a playmaker with the passing and the
    /// vision looks for somebody else, a back with the arm and the finish looks for the goal.
    /// Spread over a squad this is what shares out a team's shots the way real ones are shared.
    /// </summary>
    private static float ShotAppetite(Agent c)
    {
        var p = c.Player;
        float lean = (2f * p.LongThrows + p.Finishing - p.Passing - 2f * p.Vision) / 6f;
        float appetite = 1f + 0.05f * lean;

        // The side backs are the long-range shooters, and they are there to shoot; the centre back
        // is there first to make the play for them.
        if (c.Role is "LB" or "RB") appetite += 0.06f;
        else if (c.Role == "CB") appetite -= 0.10f;
        return Math.Clamp(appetite, 0.8f, 1.3f);
    }

    /// <summary>Open-play shots each side has taken, for <see cref="Usage"/>.</summary>
    private readonly int[] _openShots = new int[2];

    /// <summary>
    /// How much of her pull on the ball a player keeps, given how much of her side's shooting she
    /// has already done: 1 up to a fair share, falling past it to <see cref="Tuning.UsageFloor"/>.
    /// </summary>
    /// <remarks>
    /// Read wherever a choice is valued by the chance a player would get — her own shot, her
    /// drive, a pass to her — so a side that has fed one player all night starts to look for
    /// somebody else, as a defence that has keyed on her makes it. A shot at an open net is
    /// not touched: nobody passes up an empty goal to share the ball round.
    /// </remarks>
    private float Usage(Agent a)
    {
        if (_t.UsageSlope <= 0f) return 1f;
        int mine = Stats.TryGetValue(a.Id, out var st) ? st.Shots - st.SevenMeterAttempts : 0;
        float share = (mine + _t.UsagePrior * _t.UsageFreeShare) / (_openShots[a.Side] + _t.UsagePrior);
        float excess = share - _t.UsageFreeShare;
        return excess <= 0f ? 1f : MathF.Max(_t.UsageFloor, 1f - _t.UsageSlope * excess);
    }

    /// <summary>How much of an attack's worth a player keeps in her hands in that part of the court.</summary>
    private static float RoleKeepShare(string role, float mouth)
        => mouth > 14f ? 0.9f : role switch
        {
            "LB" or "CB" or "RB" => 1.0f,
            "LW" or "RW" => 1.0f, // the ball goes out to the corners as part of the circulation
            "Pivot" => 0.95f,
            _ => 0.9f,
        };

    /// <summary>
    /// What the passer believes the chance of a pass arriving is: the interception test the
    /// ball will really face, run on her read of the defence rather than the defence itself.
    /// </summary>
    private float PassSuccessEstimate(Agent from, Agent to)
    {
        var p = from.Player;
        float speed = _t.PassSpeedBase + _t.PassSpeedSpan * from.Eff(p.Passing) / 20f;
        var target = to.Pos + to.Vel * (Vector2.Distance(from.Pos, to.Pos) / speed);
        var d = target - from.Pos;
        float len2 = d.LengthSquared();
        if (len2 < 0.01f) return 0.95f;
        float len = MathF.Sqrt(len2);

        // She reads the defenders' reach slightly wrong, more so the worse her Vision.
        float misread = 0.2f * (1f - from.Eff(p.Vision) / 20f);
        float risk = 0f;
        foreach (var def in Opp(Sides[from.Side]).Court)
        {
            float t = Math.Clamp(Vector2.Dot(def.Pos - from.Pos, d) / len2, 0f, 1f);
            if (t < 0.12f) continue;
            var point = from.Pos + d * t;
            float ballTime = t * len / speed;
            float read = _t.InterceptRead - 0.18f * def.Eff(def.Player.Anticipation) / 20f;
            float legs = def.MaxSpeed * _t.InterceptLegs * MathF.Max(0f, ballTime - read);
            float gap = Vector2.Distance(def.Pos, point) - _t.InterceptReach + misread * Gauss();
            float margin = legs - gap;
            // A defender who gets there does not always come away with it — but the pass is lost
            // either way more often than not, as the ball runs loose among her team-mates.
            if (margin > -0.3f) risk = MathF.Max(risk, Sigmoid(margin / 0.05f) * 0.7f);
        }

        float error = _t.PassErrorPerMetre * len * MathF.Max(0.3f, 1.3f - from.Eff(p.Passing) / 20f);
        float drop = 0.015f + 0.25f * MathF.Max(0f, error - 0.4f);
        var contester = Contester(to, target, lead: len / speed);
        float contest = contester == null ? 0f : ContestChance(contester, to, target);

        // Through the goal area is through the keeper's territory: she can leave her line and
        // take anything that floats across it.
        float overArea = CrossesGoalArea(from, from.Pos, target) ? 0.55f : 0f;
        return (1f - risk) * (1f - drop) * (1f - contest) * (1f - overArea);
    }

    /// <summary>Whether a ball thrown from <paramref name="a"/> to <paramref name="b"/> passes over the goal area being attacked.</summary>
    private bool CrossesGoalArea(Agent passer, Vector2 a, Vector2 b)
    {
        var s = Sides[passer.Side];
        for (int i = 1; i < 8; i++)
        {
            var (u, w) = Frame(s, Vector2.Lerp(a, b, i / 8f));
            if (Court.InGoalArea(u, w)) return true;
        }
        return false;
    }

    /// <summary>The narrowest gap a defender leaves in the lane from her to that point.</summary>
    private float LaneSpace(Agent c, Vector2 point)
    {
        var d = point - c.Pos;
        float len2 = d.LengthSquared();
        float space = 4f;
        foreach (var def in Opp(Sides[c.Side]).Court)
        {
            if (def.IsKeeper || def.StunnedUntil > Clock) continue;
            float t = Math.Clamp(Vector2.Dot(def.Pos - c.Pos, d) / MathF.Max(len2, 0.01f), 0f, 1.3f);
            // They will not stand still while she runs at them: in the half-second the drive
            // takes, a set defender closes most of a metre of it.
            float dist = Vector2.Distance(def.Pos, c.Pos + d * t) - (def.Vel.Length() < 2.5f ? _t.LaneClosing : 0f);
            space = MathF.Min(space, dist);
        }
        return space;
    }

    /// <summary>0–1: how she would fare one against one with the defenders around her.</summary>
    private float DuelEdge(Agent c)
    {
        float att = AttackerDuelPower(c);
        float def = 0f; int n = 0;
        foreach (var d in Opp(Sides[c.Side]).Court)
        {
            if (d.IsKeeper || Vector2.Distance(d.Pos, c.Pos) > 3f) continue;
            def += DefenderDuelPower(d); n++;
        }
        if (n == 0) return 1f;
        return Sigmoid((att - def / n) / 2.5f);
    }

    private static Vector2 Rotate(Vector2 v, float a)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    // ── Off the ball ───────────────────────────────────────────────────────────

    /// <summary>
    /// The 3:3 an attack sets up in, before the ball bends it: metres out from the goal line and
    /// metres across, the attack's left positive. Public for the tactics board, which draws it.
    /// </summary>
    public static (float U, float W) AttackHome(string role) => role switch
    {
        "LW" => (1.2f, 8.6f),
        "RW" => (1.2f, -8.6f),
        "LB" => (9.9f, 4.6f),
        "RB" => (9.9f, -4.6f),
        "CB" => (10.6f, 0f),
        "Pivot" => (6.4f, 0f),
        _ => (10f, 0f),
    };

    /// <summary>
    /// Where a player of the side in possession wants to be, in its attacking frame. The 3:3
    /// shape every club plays from, bent by where the ball is, whether the attack is still
    /// breaking and what the restart rules allow.
    /// </summary>
    private (float U, float W) AttackSlot(TeamSide s, Agent a, bool forThrowOff = false)
    {
        var ball = Frame(s, Ball.Pos);
        (float u, float w) = a.Role == "GK" ? (Court.Length - 3.5f, 0f) : AttackHome(a.Role);
        if (a.Role == ExtraRole)
        {
            // The seventh plays as a second pivot, on the other side of the first.
            float pivotW = s.Court.FirstOrDefault(x => x.Role == "Pivot") is { } piv ? Frame(s, piv.Pos).W : 0f;
            (u, w) = (6.5f, pivotW > 0 ? -2.4f : 2.4f);
        }

        if (forThrowOff)
        {
            // In their own half for the throw-off.
            return a.IsKeeper ? (Court.Length - 2f, 0f) : (MathF.Max(20.5f, u + 12f), w * 0.8f);
        }
        if (a.IsKeeper) return (u, w);

        switch (a.Role)
        {
            case "LB" or "CB" or "RB":
                // Coiled when the ball is next door, ready to run on to it; back at ten when not.
                var holder = Ball.Holder;
                bool neighbour = holder != null && holder.Side == s.Index && holder != a
                                 && MathF.Abs(Array.IndexOf(AttackOrder, holder.Role) - Array.IndexOf(AttackOrder, a.Role)) == 1;
                u = neighbour ? 9.1f : 10.1f;
                break;
            case "LW" or "RW":
                // Drifting in from the far corner when the ball is on the other side.
                float sideSign = MathF.Sign(w);
                if (ball.W * sideSign < -2f) { u = 2.6f; w = sideSign * 7.8f; }
                break;
            case "Pivot":
                if (_gapTick != _tick || _gapSide != s.Index) { _gapTick = _tick; _gapSide = s.Index; _gapW = PivotGap(s, ball.W); }
                w = _gapW;
                break;
        }

        // Getting open: a back or pivot who might get the ball next does not stand behind a
        // defender. She looks at a few spots around her own and takes the one the ball can
        // reach — how far she looks, and how well she reads it, is her OffTheBall.
        var carrier = Ball.Holder;
        if (Phase == MatchPhase.Play && carrier != null && carrier.Side == s.Index && carrier != a
            && a.Role is "LB" or "CB" or "RB" or "Pivot")
            (u, w) = GetOpen(s, a, carrier, u, w);

        // The outlet: with the ball deep in her own half — the keeper's, or a defender's who has
        // just won it — the nearest back comes short for it rather than everybody running away
        // from the passer.
        if (Phase == MatchPhase.Play && carrier != null && carrier.Side == s.Index && carrier != a && a == OutletFor(s, carrier))
        {
            var (hu, hw) = Frame(s, carrier.Pos);
            float lean = OpenSide(s, carrier) * (3.5f + 1.5f * a.Eff(a.Player.OffTheBall) / 20f);
            return (MathF.Max(hu - 7f, 10f), Math.Clamp(hw + lean, -8.5f, 8.5f));
        }

        // Breaking: straight after winning the ball the quick ones go, if the defence is not back.
        float age = Clock - _attackStartedAt;
        if (Phase == MatchPhase.Play && age < 6f && DefendersBack(s) < 4)
        {
            bool runner = a.Role is "LW" or "RW"
                          || (s.Tactics.Tempo == AttackTempo.Counter && a.Role is "Pivot" or "LB" or "RB");
            if (runner) { u = 4.8f; w = MathF.Sign(w == 0 ? 1 : w) * 3.2f; }
        }

        // The rules of the restart.
        if (Phase == MatchPhase.Restart)
        {
            if (Restart is RestartKind.FreeThrow)
                (u, w) = Court.PushOutTo(u, w, Court.FreeThrowRadius + 0.2f);
            else if (Restart is RestartKind.SevenMetre)
                (u, w) = Court.PushOutTo(u, w, Court.FreeThrowRadius + 0.3f);
            else if (Restart is RestartKind.ThrowOff)
                u = MathF.Max(u, Court.CentreX + 0.5f); // in their own half until the whistle
        }

        return Court.PushOutTo(u, w, Court.GoalAreaRadius + 0.1f);
    }

    private int _outletTick = -1;
    private Agent? _outlet;

    /// <summary>Who comes short for the ball when it is deep in her own half: the nearest non-wing court player.</summary>
    private Agent? OutletFor(TeamSide s, Agent carrier)
    {
        if (_outletTick == _tick) return _outlet;
        _outletTick = _tick;
        _outlet = null;
        var (hu, _) = Frame(s, carrier.Pos);
        if (hu < 24f) return null;
        float best = float.MaxValue;
        foreach (var t in s.Court)
        {
            if (t == carrier || t.IsKeeper || t.Role is "LW" or "RW") continue;
            float d = Vector2.DistanceSquared(t.Pos, carrier.Pos);
            if (d < best) { best = d; _outlet = t; }
        }
        return _outlet;
    }

    /// <summary>+1 or −1: the side of the court, from the carrier, with fewer opponents on it.</summary>
    private float OpenSide(TeamSide s, Agent carrier)
    {
        var (_, cw) = Frame(s, carrier.Pos);
        int left = 0, right = 0;
        foreach (var d in Opp(s).Court)
        {
            if (d.IsKeeper) continue;
            var (_, dw) = Frame(s, d.Pos);
            if (dw > cw) left++; else right++;
        }
        return left <= right ? 1f : -1f;
    }

    private static readonly (float Du, float Dw)[] OpenOffsets = [(0f, 0f), (1f, 0f), (0f, 1f), (0f, -1f), (0.8f, 0.8f), (0.8f, -0.8f), (-0.6f, 0f)];

    private (float U, float W) GetOpen(TeamSide s, Agent a, Agent carrier, float u, float w)
    {
        float range = 0.5f + 1.0f * a.Eff(a.Player.OffTheBall) / 20f;
        if (Clock < a.OpenUntil) return Court.PushOutTo(u + a.OpenDu * range, w + a.OpenDw * range, Court.GoalAreaRadius + 0.1f);
        float bestScore = float.MinValue, bu = u, bw = w, bestDu = 0f, bestDw = 0f, heldScore = float.MinValue;
        var from = carrier.Pos;
        foreach (var (du, dw) in OpenOffsets)
        {
            float cu = u + du * range, cw = w + dw * range;
            if (a.Role == "Pivot") cu = MathF.Min(cu, 7.2f); // the pivot's work is on the line
            var (pu, pw) = Court.PushOutTo(cu, cw, Court.GoalAreaRadius + 0.1f);
            var spot = World(s, pu, pw);
            var d = spot - from;
            float len2 = MathF.Max(d.LengthSquared(), 0.01f);
            float clear = 2f;
            foreach (var def in Opp(s).Court)
            {
                if (def.IsKeeper) continue;
                float t = Math.Clamp(Vector2.Dot(def.Pos - from, d) / len2, 0.12f, 1f);
                clear = MathF.Min(clear, Vector2.Distance(def.Pos, from + d * t));
            }
            float score = clear - 0.25f * MathF.Sqrt(du * du + dw * dw);
            if (du == a.OpenDu && dw == a.OpenDw) heldScore = score;
            if (score > bestScore) { bestScore = score; bu = pu; bw = pw; bestDu = du; bestDw = dw; }
        }

        // Only for a clearly better spot: nobody darts about for a few centimetres of lane.
        if (heldScore > float.MinValue && bestScore < heldScore + 0.3f) { bestDu = a.OpenDu; bestDw = a.OpenDw; }
        a.OpenDu = bestDu;
        a.OpenDw = bestDw;
        a.OpenUntil = Clock + 0.8f + 0.4f * Rand();
        return Court.PushOutTo(u + bestDu * range, w + bestDw * range, Court.GoalAreaRadius + 0.1f);
    }

    /// <summary>The seam between the two line defenders nearest the ball, which is where a pivot wants to stand.</summary>
    private float PivotGap(TeamSide s, float ballW)
    {
        float target = ballW * 0.5f;
        float bestW = 0, bestD = float.MaxValue;
        var defenders = Opp(s).Court;
        for (int i = 0; i < defenders.Count; i++)
        {
            var di = defenders[i];
            if (di.IsKeeper) continue;
            var (ui, wi) = Frame(s, di.Pos);
            if (ui > 8f) continue;
            for (int j = i + 1; j < defenders.Count; j++)
            {
                var dj = defenders[j];
                if (dj.IsKeeper) continue;
                var (uj, wj) = Frame(s, dj.Pos);
                if (uj > 8f || MathF.Abs(wi - wj) > 4.5f) continue;
                float mid = (wi + wj) / 2f;
                float d = MathF.Abs(mid - target);
                if (d < bestD) { bestD = d; bestW = mid; }
            }
        }
        return Math.Clamp(bestW, -4f, 4f);
    }

    private int _backTick = -1, _backSide = -1, _backCount;
    private int _gapTick = -1, _gapSide = -1;
    private float _gapW;
    private int _tick;

    /// <summary>How many of the opponents are already between the ball and their goal.</summary>
    private int DefendersBack(TeamSide attacking)
    {
        if (_backTick == _tick && _backSide == attacking.Index) return _backCount;
        _backTick = _tick; _backSide = attacking.Index;
        return _backCount = CountDefendersBack(attacking);
    }

    private int CountDefendersBack(TeamSide attacking)
    {
        var (bu, _) = Frame(attacking, Ball.Pos);
        int n = 0;
        foreach (var d in Opp(attacking).Court)
        {
            if (d.IsKeeper) continue;
            var (u, _) = Frame(attacking, d.Pos);
            if (u < bu - 1f && u < 12f) n++;
        }
        return n;
    }

    /// <summary>
    /// How hard a side is playing right now, 0.55–1: the tempo it has been told to play at,
    /// flat out on the break, sharper as the attack gets old or the referee's arm goes up.
    /// Jogging, shuffling and getting into position all run at this; sprints are sprints.
    /// </summary>
    public float Intensity(TeamSide s)
    {
        if (Possession != s.Index)
        {
            return s.Tactics.Aggression switch
            {
                DefensiveAggression.Aggressive => 0.85f,
                DefensiveAggression.Contain => 0.65f,
                _ => 0.75f,
            };
        }
        float age = Clock - _attackStartedAt;
        if (age < 6f && DefendersBack(s) < 4) return 1f;
        float i = s.Tactics.Tempo switch
        {
            AttackTempo.Counter => 0.8f,
            AttackTempo.Controlled => 0.58f,
            _ => 0.68f,
        };
        if (age > 25f) i += 0.1f;
        if (PassiveWarned) i += 0.15f;
        return MathF.Min(1f, i);
    }

    private void PlanAttack(TeamSide s)
    {
        float intensity = Intensity(s);
        foreach (var a in s.Court)
        {
            if (a.IsKeeper) continue;

            if (Ball.State == BallState.Pass && Ball.Receiver == a)
            {
                a.Target = Ball.To;
                a.DesiredSpeed = a.MaxSpeed;
                continue;
            }

            if (a == Ball.Holder && Phase == MatchPhase.Play)
            {
                if (a.Intent == "drive") continue;
                // Holding: settle into her spot, bouncing it, looking.
                var (su, sw) = AttackSlot(s, a);
                a.Target = World(s, su, sw);
                a.DesiredSpeed = 1.2f + 1.6f * intensity;
                continue;
            }
            if (a == Ball.Holder) continue; // taking a restart: UpdateRestart moves her

            var (u, w) = AttackSlot(s, a);
            a.Target = Court.Clamp(World(s, u, w));
            float far = Vector2.Distance(a.Pos, a.Target);
            a.DesiredSpeed = far > 6f ? a.MaxSpeed * (0.75f + 0.25f * intensity)
                           : far > 1.5f ? 1.8f + 2.4f * intensity
                           : 0.9f + 1.2f * intensity;
            a.Intent = "";
        }
    }
}

/// <summary>One decision on the ball: where she was, what each option was worth, what she chose (0 keep, 1 shoot, 2 pass, 3 drive).</summary>
public sealed record DecisionTrace(float Time, string Role, (float U, float W) At, float Keep,
    (int Kind, string Target, float Value, float Noisy)[] Options, int Chosen, string? ChosenTarget);
