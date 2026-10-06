namespace HandballManager.Engine2D;

/// <summary>
/// The handful of physical constants the engine is calibrated with. Everything else follows
/// from positions and attributes; these set how hard the physics is — how quickly a keeper
/// reacts, how far a pass wanders, how often a defender's stop becomes a punishment.
/// </summary>
/// <remarks>
/// An instance rather than static fields, so a calibration run can try a variant without
/// touching the matches running beside it. <see cref="Default"/> is what the game plays with.
/// </remarks>
public sealed class Tuning
{
    public static Tuning Default { get; } = new();

    // ── Keeper ─────────────────────────────────────────────────────────────────
    /// <summary>Reaction time of a keeper with Reflexes 0, in seconds; Reflexes 20 takes <see cref="KeeperReactionSpan"/> off it.</summary>
    public float KeeperReactionBase = 0.332f;
    public float KeeperReactionSpan = 0.26f;
    /// <summary>Reach from the body without moving, metres, before height and AerialReach.</summary>
    public float KeeperReachBase = 0.28f;
    /// <summary>How fast a keeper can get a limb across once she has reacted, m/s, before Agility.</summary>
    public float KeeperLimbSpeed = 1.32f;
    /// <summary>Reach an AerialReach of 20 adds over one of 0, metres.</summary>
    public float KeeperAerialSpan = 0.4f;
    public float KeeperLimbSpeedSpan = 1.8f;
    /// <summary>The softness of the edge of a keeper's reach, metres.</summary>
    public float SaveEdge = 0.20f;
    /// <summary>Reaction lost to each body right across the keeper's line of sight, seconds.</summary>
    public float ScreenDelay = 0.025f;
    /// <summary>The taker's dummy at a seven-metre: extra reaction time the keeper loses to it, seconds.</summary>
    public float SevenMetreFake = 0.02f;
    /// <summary>Reaction a keeper loses to a wing's hang time in the air, seconds, at full Flair.</summary>
    public float WingHangTime = 0.20f;
    /// <summary>Per decision of a back with a Kempa partner, the chance the play is even on.</summary>
    public float KempaCallChance = 0.0004f;
    /// <summary>Reaction the keeper loses to a Kempa, seconds: she was set for the lob, not the shot.</summary>
    public float KempaSurprise = 0.12f;

    // ── Shooting ───────────────────────────────────────────────────────────────
    public float AimErrorBase = 0.052f;
    public float AimErrorPerMetre = 0.012f;
    public float AimErrorSkill = 0.15f;
    public float AimErrorPressure = 0.11f;
    /// <summary>Extra aim error per metre beyond twelve, for shots at an empty goal from distance.</summary>
    public float LongThrowWander = 0.018f;
    /// <summary>A seven-metre's aim error as a share of an open-play shot's from the same spot.</summary>
    public float SevenMetreSteadiness = 0.8f;
    /// <summary>The narrowest angle the aim-error widening is taken to, as a sine (about 20°).</summary>
    public float MinShotAngleSine = 0.12f;
    /// <summary>The share of her running speed towards goal that goes into the ball.</summary>
    public float RunUpTransfer = 0.6f;
    public float ShotSpeedBase = 19f;
    public float ShotSpeedSpan = 8f;

    // ── Blocks ─────────────────────────────────────────────────────────────────
    public float BlockWidth = 0.85f;
    public float BlockChance = 0.5f;
    /// <summary>The share of blocks that go back over the defence's own end line, for a corner throw.</summary>
    public float BlockOverTheLine = 0.15f;

    // ── Saves ──────────────────────────────────────────────────────────────────
    /// <summary>Above this height (of 2 m) a parry goes over the bar, and a catch is harder.</summary>
    public float TipOverHeight = 1.55f;
    /// <summary>Wider than this from the middle of the goal (of 1.5 m), a parry is pushed aside.</summary>
    public float PushWideFrom = 0.9f;

    // ── Passing ────────────────────────────────────────────────────────────────
    public float PassSpeedBase = 10f;
    public float PassSpeedSpan = 6f;
    public float PassErrorBase = 0.10f;
    public float PassErrorPerMetre = 0.03f;
    /// <summary>How far a defender can reach for a ball going past, metres.</summary>
    public float InterceptReach = 0.5f;
    /// <summary>A defender right on the receiver as the ball arrives wins it this often, before attributes.</summary>
    public float ContestBase = 0.68f;
    /// <summary>How long a defender takes to read a pass with Anticipation 0, seconds.</summary>
    public float InterceptRead = 0.72f;
    /// <summary>The share of her top speed a defender can use lunging for a pass.</summary>
    public float InterceptLegs = 0.4f;

    // ── Contact ────────────────────────────────────────────────────────────────
    /// <summary>Seconds between duel resolutions while a defender is on the ball carrier.</summary>
    public float DuelInterval = 0.3f;
    /// <summary>Chance, per duel, that the defender stops the attack for a free throw.</summary>
    public float StopChance = 0.40f;
    /// <summary>Chance, per duel, that the attacker gets past.</summary>
    public float BreakChance = 0.18f;
    public float StealChance = 0.02f;
    public float OffensiveFoulChance = 0.02f;
    /// <summary>Of the stops, the share that earn two minutes, at neutral aggression.</summary>
    public float SuspensionShare = 0.0375f;
    /// <summary>
    /// How the defensive instruction moves two-minute suspensions on its own, on top of how
    /// often a defender gets in a foul. Without it an aggressive defence ended attacks earlier,
    /// had fewer duels, and so collected no more suspensions than a contained one.
    /// </summary>
    public float SuspensionContain = 0.8f, SuspensionAggressive = 1.1f;
    /// <summary>A player on one suspension, then on two, picks her fouls: most reds were a third to the pivot's marker.</summary>
    public float CarefulAfterOne = 0.75f, CarefulAfterTwo = 0.25f;

    // ── Pace ───────────────────────────────────────────────────────────────────
    /// <summary>How often a side means to take the throw-off quickly after conceding, by tempo.</summary>
    public float FastThrowOffCounter = 0.45f, FastThrowOffBalanced = 0.35f, FastThrowOffControlled = 0.2f;
    /// <summary>Seconds against a set defence before an attack is ripe for any decent shot, by tempo.</summary>
    public float SetupCounter = 18f, SetupBalanced = 21f, SetupControlled = 23f;

    // ── Mismatches ─────────────────────────────────────────────────────────────
    /// <summary>
    /// The share of a court player's distance from a typical attribute that tells on the court.
    /// Keepers keep all of theirs: keepers are what decide matches between equals.
    /// </summary>
    public float AttributeSpread = 0.7f;
    /// <summary>
    /// How much the level of the whole match shows, as against the gap between the players in
    /// it. At 1 two weak sides played a weak match — keepers who could not stop anything and
    /// seventy goals in Slovenia against forty-nine in Sweden, where real leagues all sit
    /// between 55 and 59. The gap between the two sides, and between the two keepers, is kept.
    /// </summary>
    public float LevelWeight = 0.3f;
    /// <summary>A lead past this and the side ahead eases off, as the quick sim's does (ScoringModel).</summary>
    public int ComfortableLead = 8;
    /// <summary>What each goal of lead past comfortable takes off the leader's level, up to <see cref="EasingOffMax"/>.</summary>
    public float EasingOffPerGoal = 0.02f;
    public float EasingOffMax = 0.12f;
    /// <summary>Of the stops on a clear chance, the share given as a seven-metre throw.</summary>
    public float SevenMetreShare = 1.0f;

    /// <summary>How much of a gap a set defender closes while an attacker drives at it, metres.</summary>
    public float LaneClosing = 0.6f;
    /// <summary>How far the neighbours slide into the hole a stepping-out defender leaves, as a share of the gap.</summary>
    public float WallClosing = 0.45f;

    // ── Attack ─────────────────────────────────────────────────────────────────
    /// <summary>What keeping the ball is worth, in goals, early in an attack.</summary>
    /// <summary>Straight after winning the ball, before the defence is back.</summary>
    public float KeepValueBreak = 0.55f;
    /// <summary>The first seconds against a set defence: only a real chance is worth a shot.</summary>
    public float KeepValueSetup = 0.86f;
    public float KeepValueEarly = 0.68f;
    public float KeepValueLate = 0.30f;
    public float PassiveWarningAfter = 48f;

    // ── The day ────────────────────────────────────────────────────────────────
    /// <summary>Spread of a player's form on the day, as a share of her level.</summary>
    public float PlayerDayForm = 0.04f;
    /// <summary>A keeper's: she has her day, or she does not.</summary>
    public float KeeperDayForm = 0.05f;
    /// <summary>The whole side's.</summary>
    public float TeamDayForm = 0.03f;

    // ── Rotation ───────────────────────────────────────────────────────────────
    /// <summary>A court player is considered for a rest below this much energy.</summary>
    public float RotateBelow = 64f;
    /// <summary>A fresh deputy is sent on if her day's value is at least this share of the tired starter's.</summary>
    public float RotateTolerance = 0.88f;
    /// <summary>Only a player with at least this much back in her legs counts as fresh.</summary>
    public float FreshAbove = 80f;

    // ── Fatigue ────────────────────────────────────────────────────────────────
    //
    // A starter left on for a whole half ends it in the fifties, and a change on the bench gets
    // back about three points a minute, the classic engine's rate. The first version drained at
    // about half this and refilled at twice it, so every bar sat between 75 and 100 all match
    // and rotation was invisible.
    public float DrainIdle = 0.007f;
    public float DrainPerSpeedSquared = 0.0021f;
    /// <summary>Per tick, times ten: 0.005 is three points a minute.</summary>
    public float BenchRecovery = 0.005f;
    /// <summary>Fifteen minutes in the dressing room.</summary>
    public float HalfTimeRecovery = 12f;
    /// <summary>
    /// A time-out's minute on the bench, for the side that called it. The classic engine's +10
    /// is two minutes of its drain; here that is three points — ten was eight minutes' worth,
    /// and cut rotation by a third.
    /// </summary>
    public float TimeoutRecovery = 3f;
}
