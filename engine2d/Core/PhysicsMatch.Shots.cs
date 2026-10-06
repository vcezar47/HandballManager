using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>
/// Shooting, as geometry. A shot is a straight line from the hand to a point in the goal at a
/// speed; the keeper saves it if she can get a limb to where it passes her before it gets
/// there, and a defender blocks it if it goes through the space her arms fill.
/// </summary>
/// <remarks>
/// The same model answers two questions. <see cref="EstimateXg"/> is what a player
/// <em>thinks</em> a shot is worth — used to decide whether to shoot, and whether a teammate
/// is worth passing to. <see cref="TakeShot"/> is what actually happens, with the aim drawn
/// from how steady her hand is. Distance, angle, the keeper's position and the block all fall
/// out of the one calculation: a nine-metre shot gives the keeper longer to react, a wing has
/// less goal to aim at, a pivot at six metres leaves her almost none.
/// </remarks>
public sealed partial class PhysicsMatch
{
    private const float GoalFrameW = Court.PostHalf - 0.08f;
    private const float GoalFrameZ = Court.GoalHeight - 0.08f;

    private readonly struct ShotPlan
    {
        public required Vector2 Release { get; init; }
        public required float ReleaseZ { get; init; }
        public required float Speed { get; init; }
        public required float Sigma { get; init; }
        /// <summary>
        /// The aim error along the goal line. From a tight angle the goal is narrow as the
        /// shooter sees it, so the same wobble of the hand moves the ball further across it.
        /// </summary>
        public required float SigmaW { get; init; }
        public required float Flair { get; init; }
        public bool SevenMetre { get; init; }
        /// <summary>Reaction the keeper loses to bodies in her line of sight — fixed for the shot, whatever the aim.</summary>
        public float Screen { get; init; }
        /// <summary>
        /// A wing in flight: she hangs in the air watching the keeper commit to the near post
        /// before choosing, which costs the keeper reaction a set shooter never takes from her.
        /// </summary>
        public bool Wing { get; init; }
        /// <summary>Taken in the air off a lob: high, and the keeper caught moving the wrong way.</summary>
        public bool Kempa { get; init; }
        /// <summary>Where the keeper is assumed to be — her real position for a real shot.</summary>
        public Vector2? Keeper { get; init; }
    }

    private static readonly float[] AimW = [-1.2f, -0.6f, 0f, 0.6f, 1.2f];
    private static readonly float[] AimZ = [0.3f, 1.0f, 1.7f];
    private static readonly (float W, float Z)[] EstimateAims = [(-1.2f, 0.3f), (1.2f, 0.3f), (-1.2f, 1.7f), (1.2f, 1.7f), (0f, 0.3f)];

    /// <summary>Where the ball leaves her hand: a back jumps forward, a wing dives in to open the angle.</summary>
    private ShotPlan PlanShot(Agent s, Vector2 at, bool sevenMetre, Vector2? keeperAt = null)
    {
        var side = Sides[s.Side];
        var p = s.Player;
        var (u, w) = Frame(side, at);

        Vector2 release;
        float z;
        bool wingShot = false;
        if (sevenMetre)
        {
            release = World(side, Court.SevenMetreMark, 0f);
            z = 1.85f + (s.HeightM - 1.75f) * 0.8f;
        }
        else
        {
            float mouthW = Math.Clamp(w, -Court.PostHalf, Court.PostHalf);
            var toMouth = new Vector2(-u, mouthW - w);
            float dist = toMouth.Length();
            bool wing = MathF.Abs(w) > 4.5f && u < 6f;
            wingShot = wing;

            // A wing does not dive at the near post — that closes the angle to nothing. She takes
            // off into the area towards the middle of it, which is what opens the goal up.
            bool close = dist < 7f;
            if (wing)
            {
                // A wing runs two strides in along the six-metre line and takes off diagonally
                // for the middle of the goal, releasing about five metres out at 45–60 degrees —
                // the whole trick of the position. A better jumper gets deeper and wider.
                // Her defender, if she has stayed tight, pushes the take-off out and the angle shut.
                float jr = s.Eff(p.JumpingReach) / 20f;
                float squeeze = 1f - 0.4f * Pressure(s, at);
                float angle = (30f + 20f * jr) * squeeze * MathF.PI / 180f;
                float reach = 5.6f - 0.8f * jr;
                float idealU = reach * MathF.Sin(angle);
                float idealW = MathF.Sign(w) * (Court.PostHalf + reach * MathF.Cos(angle));

                // She can only get there from where she is: a stride and a flight, not a teleport.
                // Stuck deep in the corner, the angle stays as bad as the corner is.
                float gu = idealU - u, gw = idealW - w;
                float gap = MathF.Sqrt(gu * gu + gw * gw);
                float flight = 2.0f + 1.4f * jr;
                float k = gap <= flight || gap < 0.01f ? 1f : flight / gap;
                u += gu * k;
                w += gw * k;
            }
            else
            {
                float jump = close ? 0.7f : 0.5f + 0.6f * s.Eff(p.JumpingReach) / 20f;
                jump = MathF.Min(jump, MathF.Max(0f, dist - 3.2f));
                if (dist > 0.01f) { u += toMouth.X / dist * jump; w += toMouth.Y / dist * jump; }
            }
            release = World(side, u, w);
            if (_kempaShot || s.JumpUntil > 0f) { release = at; } // already in the air: she releases where she is
            if (_kempaShot) wingShot = false;
            z = _kempaShot ? 2.5f : wing || close
                ? 1.55f + 0.25f * s.Eff(p.JumpingReach) / 20f
                : 1.95f + 0.45f * s.Eff(p.JumpingReach) / 20f + (s.HeightM - 1.75f) * 0.9f;
        }

        float d = Vector2.Distance(release, World(side, 0f, 0f));
        float skill = sevenMetre
            ? 0.6f * s.Eff(p.SevenMeterTaking) + 0.4f * s.Eff(p.Composure)
            : d > 8f ? 0.5f * s.Eff(p.Finishing) + 0.5f * s.Eff(p.LongThrows)
            : s.Eff(p.Finishing);

        float pressure = sevenMetre ? 0f : Pressure(s, at);
        float sigma = _t.AimErrorBase + _t.AimErrorPerMetre * d
                      + _t.AimErrorSkill * MathF.Max(0f, 1f - skill / 20f)
                      + _t.AimErrorPressure * pressure * MathF.Max(0.2f, 1.2f - s.Eff(p.Composure) / 20f)
                      + 0.08f * s.Fatigue;

        // A throw the length of the court wanders far more than one from the nine metres.
        if (d > 12f) sigma += _t.LongThrowWander * (d - 12f);

        // Standing still, nobody near, all the time in the world: a penalty is placed, not flung.
        if (sevenMetre) sigma *= _t.SevenMetreSteadiness;

        float power = 0.55f * s.Eff(p.LongThrows) + 0.45f * s.Eff(p.Strength);
        float speed = _t.ShotSpeedBase + _t.ShotSpeedSpan * power / 20f - 2f * s.Fatigue;

        // Running on to it: her own speed towards goal goes into the ball, and a run-up gets her
        // higher off the floor — the difference between a set shot and a real back's jump shot.
        if (!sevenMetre)
        {
            var toGoalDir = World(side, 0f, 0f) - at;
            float forward = toGoalDir.LengthSquared() > 0.01f ? MathF.Max(0f, Vector2.Dot(s.Vel, Vector2.Normalize(toGoalDir))) : 0f;
            speed += _t.RunUpTransfer * forward;
            z += 0.05f * MathF.Min(forward, 4f);
        }

        var toGoal = World(side, 0f, 0f) - release;
        float sinAngle = toGoal.LengthSquared() > 0.01f ? MathF.Abs(Frame(side, release).U) / toGoal.Length() : 1f;
        float sigmaW = sigma / MathF.Max(_t.MinShotAngleSine, sinAngle);

        var keeper = Opp(side).Keeper;
        var keeperPos = keeperAt ?? keeper?.Pos;
        float screen = keeperPos == null || sevenMetre ? 0f : Screening(release, keeperPos.Value, s);
        return new ShotPlan { Release = release, ReleaseZ = z, Speed = speed, Sigma = sigma, SigmaW = sigmaW, Flair = s.Eff(p.Flair),
                              SevenMetre = sevenMetre, Screen = screen, Keeper = keeperPos, Wing = wingShot && !_kempaShot, Kempa = _kempaShot };
    }

    /// <summary>How closely she is marked: 0 with nobody within a metre and a half, 1 with a defender on her.</summary>
    private float Pressure(Agent s, Vector2 at)
    {
        float nearest = float.MaxValue;
        foreach (var d in Opp(Sides[s.Side]).Court)
        {
            if (d.IsKeeper) continue;
            nearest = MathF.Min(nearest, Vector2.Distance(d.Pos, at));
        }
        return Math.Clamp((1.6f - nearest) / 1.2f, 0f, 1f);
    }

    /// <summary>
    /// What a shot from <paramref name="at"/> is worth to her — the best of her options, blocks,
    /// misses and the keeper all priced in. A decision aid: nothing random, nothing changes.
    /// </summary>
    private float EstimateXg(Agent s, Vector2 at, bool sevenMetre = false, Vector2? keeperAt = null)
    {
        var plan = PlanShot(s, at, sevenMetre, keeperAt);
        float best = 0f;
        // The four corners and the bottom middle: where the best option almost always is. A
        // coarser grid than the real shot uses, because this runs for every teammate at every
        // decision.
        foreach (var (w, z) in EstimateAims)
            best = MathF.Max(best, AimValue(s, plan, w, z, sevenMetre));
        return best;
    }

    private float AimValue(Agent s, in ShotPlan plan, float aimW, float aimZ, bool sevenMetre)
    {
        float sig = plan.Sigma, sigW = plan.SigmaW;
        float onW = Phi((GoalFrameW - aimW) / sigW) - Phi((-GoalFrameW - aimW) / sigW);
        float onZ = Phi((GoalFrameZ - aimZ) / sig);
        float soft = MathF.Sqrt(_t.SaveEdge * _t.SaveEdge + 0.125f * (sig * sig + sigW * sigW));
        float save = SaveChance(s, plan, aimW, aimZ, soft);
        float block = sevenMetre ? 0f : BlockChance(s, plan, aimW, aimZ, out _);
        return onW * onZ * (1f - block) * (1f - save);
    }

    /// <summary>Whether the keeper gets to a ball aimed at (<paramref name="w"/>, <paramref name="z"/>) in the goal.</summary>
    private float SaveChance(Agent s, in ShotPlan plan, float w, float z, float edge)
    {
        var side = Sides[s.Side];
        var k = Opp(side).Keeper;
        if (k == null) return 0f;

        var kPos = plan.Keeper ?? k.Pos;
        var target = World(side, 0f, w);
        var d = target - plan.Release;
        float len2 = d.LengthSquared();
        if (len2 < 0.01f) return 0f;
        float t = Math.Clamp(Vector2.Dot(kPos - plan.Release, d) / len2, 0f, 1f);
        var closest = plan.Release + d * t;
        float lateral = Vector2.Distance(kPos, closest);
        float zAt = plan.ReleaseZ + t * (z - plan.ReleaseZ);
        float centre = 0.85f + (k.HeightM - 1.75f) * 0.5f;
        float dz = 0.8f * (zAt - centre);
        float reachNeeded = MathF.Sqrt(lateral * lateral + dz * dz);

        var kp = k.Player;
        float reaction = _t.KeeperReactionBase - _t.KeeperReactionSpan * k.Eff(kp.Reflexes) / 20f
                         + plan.Screen
                         + 0.04f * plan.Flair / 20f
                         + (plan.SevenMetre ? _t.SevenMetreFake : 0f)
                         + (plan.Wing ? _t.WingHangTime * (0.6f + 0.4f * plan.Flair / 20f) : 0f)
                         + (plan.Kempa ? _t.KempaSurprise : 0f)
                         + (k.Vel.Length() > 1.5f ? 0.05f : 0f);
        float arrive = t * MathF.Sqrt(len2) / plan.Speed;
        float reach = _t.KeeperReachBase + 0.35f * (k.HeightM - 1.60f) + _t.KeeperAerialSpan * k.Eff(kp.AerialReach) / 20f;
        float limb = _t.KeeperLimbSpeed + _t.KeeperLimbSpeedSpan * k.Eff(kp.Agility) / 20f;
        float cover = reach + limb * MathF.Max(0f, arrive - reaction);

        return Sigmoid((cover - reachNeeded) / edge);
    }

    /// <summary>
    /// Bodies between the shooter and the keeper. She cannot save what she did not see leave
    /// the hand, and a shot through a crowded nine metres is picked up late — which is what
    /// makes the long-range shot work at all against a keeper with that much time.
    /// </summary>
    private float Screening(Vector2 release, Vector2 keeper, Agent shooter)
    {
        var d = keeper - release;
        float len2 = d.LengthSquared();
        if (len2 < 4f) return 0f;
        float hidden = 0f;
        foreach (var a in Agents)
        {
            if (a == shooter || !a.OnCourt || a.IsKeeper) continue;
            float t = Vector2.Dot(a.Pos - release, d) / len2;
            if (t < 0.1f || t > 0.9f) continue;
            float off = Vector2.Distance(a.Pos, release + d * t);
            if (off < 0.8f) hidden += _t.ScreenDelay * (1f - off / 0.8f);
        }
        return MathF.Min(hidden, _t.ScreenDelay * 2.5f);
    }

    /// <summary>The chance a defender's block is in the ball's path — the arms up, the ball not over them.</summary>
    private float BlockChance(Agent s, in ShotPlan plan, float w, float z, out Agent? blocker)
    {
        blocker = null;
        var side = Sides[s.Side];
        var target = World(side, 0f, w);
        var d = target - plan.Release;
        float len = d.Length();
        if (len < 3f) return 0f;

        // A shot from the line is gone before an arm can come across; blocks are a nine-metre
        // thing, and fade in between.
        float closeness = Math.Clamp((len - 6f) / 2.5f, 0.15f, 1f);

        float miss = 1f, best = 0f;
        foreach (var def in Opp(side).Court)
        {
            if (def.IsKeeper || def.StunnedUntil > Clock) continue;
            float t = Vector2.Dot(def.Pos - plan.Release, d) / (len * len);
            float along = t * len;
            if (along < 0.3f || along > len - 2.5f) continue;
            float lateral = Vector2.Distance(def.Pos, plan.Release + d * t);
            if (lateral >= _t.BlockWidth) continue;

            float zAt = plan.ReleaseZ + t * (z - plan.ReleaseZ);
            float armsUp = def.HeightM * 1.27f + 0.15f + 0.35f * def.Eff(def.Player.JumpingReach) / 20f;
            if (zAt > armsUp) continue;

            float set = def.Vel.Length() > 3f ? 0.5f : 1f;
            float read = 0.7f + 0.3f * def.Eff(def.Player.Anticipation) / 20f;
            float bp = _t.BlockChance * set * read * closeness * MathF.Sqrt(1f - lateral / _t.BlockWidth);
            miss *= 1f - bp;
            if (bp > best) { best = bp; blocker = def; }
        }
        return 1f - miss;
    }

    /// <summary>She shoots: pick a spot, throw it, and let the geometry decide.</summary>
    private void TakeShot(Agent s, bool sevenMetre)
    {
        var keeper = Opp(Sides[s.Side]).Keeper;
        var stat = EnsureStat(s);
        stat.Shots++;
        if (_attack != null) { _attack.Shots++; if (_attack.FirstShotAt < 0) _attack.FirstShotAt = Clock - _attack.Start; }
        if (sevenMetre) stat.SevenMeterAttempts++;

        var outcome = ResolveShot(s, sevenMetre, out var plan, out var aim, out var blocker);
        LaunchShot(s, keeper, plan, outcome, aim.X, aim.Y, aim.Z, blocker, sevenMetre);
    }

    /// <summary>Where she aims, where it really goes, and what that comes to. (X, Y) is the point in the goal; Z her read of it.</summary>
    private ShotOutcome ResolveShot(Agent s, bool sevenMetre, out ShotPlan plan, out Vector3 aim, out Agent? blocker)
    {
        var side = Sides[s.Side];
        var keeper = Opp(side).Keeper;
        plan = PlanShot(s, s.Pos, sevenMetre);

        // Choosing the corner. Her read of the keeper is only as good as her decisions.
        float noise = 0.05f * (1.2f - s.Eff(s.Player.Decisions) / 20f);
        float bestScore = float.MinValue, aimW = 0, aimZ = 1;
        foreach (var w in AimW)
            foreach (var z in AimZ)
            {
                float v = AimValue(s, plan, w, z, sevenMetre) + noise * Gauss();
                if (v > bestScore) { bestScore = v; aimW = w; aimZ = z; }
            }

        // Throwing it: the hand is never quite where the eye was.
        float tw = aimW + plan.SigmaW * Gauss();
        float tz = MathF.Max(0.05f, aimZ + plan.Sigma * Gauss());

        ShotOutcome outcome;
        blocker = null;
        float outW = MathF.Abs(tw) - Court.PostHalf, outZ = tz - Court.GoalHeight;
        if (outW > 0.1f || outZ > 0.1f) outcome = ShotOutcome.Missed;
        else if (outW > -0.04f || outZ > -0.04f) outcome = Rand() < 0.5f ? ShotOutcome.Post : ShotOutcome.Missed;
        else
        {
            float block = sevenMetre ? 0f : BlockChance(s, plan, tw, tz, out blocker);
            if (Rand() < block) outcome = ShotOutcome.Blocked;
            else if (keeper != null && Rand() < SaveChance(s, plan, tw, tz, _t.SaveEdge))
            {
                // Straight at her she can catch it; into a corner or up high she can only get a
                // hand to it. Whether she then holds it is her Handling.
                float wide = MathF.Min(1f, MathF.Abs(tw) / Court.PostHalf);
                float reach = (1.15f - 0.6f * wide) * (tz > _t.TipOverHeight ? 0.55f : 1f);
                float hold = (0.45f + 0.35f * keeper.Eff(keeper.Player.Handling) / 20f) * reach;
                outcome = Rand() < hold ? ShotOutcome.SavedHeld : ShotOutcome.Saved;
            }
            else outcome = ShotOutcome.Goal;
        }

        aim = new Vector3(tw, tz, bestScore);
        return outcome;
    }

    private void LaunchShot(Agent s, Agent? keeper, in ShotPlan plan, ShotOutcome outcome, float tw, float tz, float bestScore, Agent? blocker, bool sevenMetre)
    {
        var side = Sides[s.Side];

        // The flight, for the eye. The result is known; it lands when the ball gets there.
        Vector2 to = outcome switch
        {
            ShotOutcome.Blocked => blocker!.Pos,
            ShotOutcome.Saved or ShotOutcome.SavedHeld => keeper!.Pos,
            ShotOutcome.Missed => World(side, -1.2f, tw * 1.4f),
            ShotOutcome.Post => World(side, 0f, MathF.Sign(tw) * Court.PostHalf),
            _ => World(side, -0.3f, tw),
        };

        Ball.State = BallState.Shot;
        Ball.Holder = null;
        Ball.LastTouch = s;
        Ball.Thrower = s;
        Ball.From = plan.Release;
        Ball.To = to;
        Ball.FromHeight = plan.ReleaseZ;
        Ball.ToHeight = outcome == ShotOutcome.Blocked ? plan.ReleaseZ : tz;
        Ball.Elapsed = 0f;
        Ball.Duration = MathF.Max(0.12f, Vector2.Distance(plan.Release, to) / plan.Speed);
        Ball.PendingShot = outcome;
        Ball.ShotKeeper = keeper;
        Ball.ShotBlocker = blocker;
        Ball.IsSevenMetre = sevenMetre;
        Ball.Pos = plan.Release;
        var (ru, rw) = Frame(side, plan.Release);
        LastShot = new ShotRecord(Clock, s.Side, s.Id, plan.Release, tw, tz, outcome, sevenMetre)
        {
            Distance = Court.DistanceToGoalMouth(ru, rw),
            Role = s.Role,
            Xg = MathF.Max(0f, bestScore),
            KeeperFrame = keeper == null ? default : new Vector2(Frame(side, keeper.Pos).U, Frame(side, keeper.Pos).W),
            Pressure = sevenMetre ? 0f : Pressure(s, s.Pos),
            Sigma = plan.Sigma,
            Kempa = plan.Kempa,
            AttackAge = Clock - _attackStartedAt,
            AfterDrive = s.Intent == "drive",
            HeldFor = Clock - s.HasBallSince,
            GotBallBy = s.GotBallBy,
        };
        Shots.Add(LastShot);
    }

    /// <summary>Every shot of the match, for the shot map.</summary>
    public List<ShotRecord> Shots { get; } = [];
    public ShotRecord? LastShot { get; private set; }

    // Standard normal CDF, Abramowitz–Stegun 7.1.26 via erf.
    private static float Phi(float x)
    {
        float t = 1f / (1f + 0.3275911f * MathF.Abs(x) / MathF.Sqrt(2f));
        float y = 1f - ((((1.061405429f * t - 1.453152027f) * t + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f)
                  * t * MathF.Exp(-x * x / 2f);
        return x >= 0 ? 0.5f * (1f + y) : 0.5f * (1f - y);
    }
}

/// <summary>A shot as the shot map needs it: where from, where it went, what came of it.</summary>
public sealed record ShotRecord(float Time, int Side, int PlayerId, Vector2 From, float GoalW, float GoalZ, ShotOutcome Outcome, bool SevenMetre)
{
    /// <summary>Metres from the goal mouth at release.</summary>
    public float Distance { get; init; }
    public string Role { get; init; } = "";
    /// <summary>What she thought it was worth when she let it go.</summary>
    public float Xg { get; init; }
    /// <summary>The keeper, in the shooter's attacking frame (u out from the line, w across).</summary>
    public Vector2 KeeperFrame { get; init; }
    public float Pressure { get; init; }
    public float Sigma { get; init; }
    public bool Kempa { get; init; }
    /// <summary>Seconds into the attack, whether she was driving, and how long she had had the ball.</summary>
    public float AttackAge { get; init; }
    public bool AfterDrive { get; init; }
    public float HeldFor { get; init; }
    public string GotBallBy { get; init; } = "";
}
