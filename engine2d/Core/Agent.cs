using System.Numerics;
using HandballManager.Models;

namespace HandballManager.Engine2D;

public enum AgentState
{
    Bench,
    OnCourt,
    /// <summary>Heading for the substitution zone; still on court until she crosses the side line.</summary>
    LeavingCourt,
    /// <summary>Serving two minutes on the bench.</summary>
    Suspended,
    /// <summary>Disqualified — a third suspension. Takes no further part.</summary>
    SentOff,
    /// <summary>Hurt: down on the court, then helped off. Takes no further part.</summary>
    Injured,
}

/// <summary>
/// One player as the simulation sees her: a body with a position, a velocity and a tank of
/// energy, and the physical limits her attributes give her.
/// </summary>
/// <remarks>
/// Every attribute is read through <see cref="Eff"/>, which folds in the day's form (condition,
/// morale, chemistry, the crowd) and how tired she is right now. A tired player is not just
/// slower; she reads the game worse and her shot wanders, which is what makes rotation matter.
/// </remarks>
public sealed class Agent
{
    public Agent(Player player, int side, string role)
    {
        Player = player;
        Side = side;
        Role = role;
        Energy = (float)Math.Clamp(player.Condition, 30.0, 100.0);
        RefreshPhysics();
    }

    public Player Player { get; }
    public int Id => Player.Id;
    public string Name => Player.Name;
    public int Side { get; }

    /// <summary>Where she plays in this line-up: GK, LW, LB, CB, RB, RW or Pivot.</summary>
    public string Role { get; set; }
    public bool IsKeeper => Role == "GK";

    /// <summary>A goalkeeper by trade, whatever she is doing at the moment.</summary>
    public bool IsKeeperByTrade => Player.Position == "GK";

    public AgentState State { get; set; } = AgentState.Bench;
    public bool OnCourt => State is AgentState.OnCourt or AgentState.LeavingCourt;

    public Vector2 Pos;
    public Vector2 Vel;

    /// <summary>Where she was a tick ago, so a renderer drawing between ticks can glide rather than jump.</summary>
    public Vector2 PrevPos;
    public Vector2 Target;
    public float DesiredSpeed;

    /// <summary>
    /// Off the court but in view: walking back to the bench, or waiting at the line to come on.
    /// Those sitting on the bench are not drawn; these are.
    /// </summary>
    public bool OnTouchline;

    /// <summary>0–100. Starts at her condition and drains with every sprint.</summary>
    public float Energy;

    /// <summary>The day's multiplier — form, morale, chemistry and the crowd — set at kick-off.</summary>
    public float Form = 1f;

    /// <summary>Beaten in a duel: rooted to the spot until this time.</summary>
    public float StunnedUntil;

    /// <summary>When she next re-reads the play with the ball in her hands.</summary>
    public float NextDecisionAt;

    /// <summary>In the air over the goal area — which a court player may be, as long as she is not standing in it.</summary>
    public bool Airborne;

    /// <summary>How she came by the ball she has: "pass", "loose", "restart" or "steal".</summary>
    public string GotBallBy = "";

    /// <summary>Landed inside the goal area after a shot: she may be there while she jogs out.</summary>
    public float LandedUntil;

    /// <summary>A shooter in the air: she lands the shot when this time comes. Zero when not jumping.</summary>
    public float JumpUntil;
    public Vector2 JumpFrom, JumpTo;
    public float JumpStart;

    /// <summary>
    /// A keeper's dive at a shot, for the eye: from where she stood towards where the ball goes,
    /// starting once she has reacted (at the release, at a seven-metre she has read) and done as
    /// it arrives. Zero when not diving. The outcome is settled before; this only shows it.
    /// </summary>
    public float DiveUntil, DiveStart;
    public Vector2 DiveFrom, DiveTo;

    /// <summary>The spot she is getting open to, held for a moment rather than re-picked every tick.</summary>
    public float OpenDu, OpenDw, OpenUntil;

    /// <summary>When she got the ball, for the three-second rule.</summary>
    public float HasBallSince;

    /// <summary>
    /// Her read of the play for as long as she has the ball. Drawn once, when it arrives, so she
    /// does not change her mind every tenth of a second — re-rolling the judgement that often
    /// would make every marginal shot a certainty sooner or later.
    /// </summary>
    public int ReadSeed;

    /// <summary>A keeper's error in reading the middle of the angle, metres — smaller the better her Positioning.</summary>
    public float Wobble;

    public float CourtSeconds;
    public float SuspendedUntil;
    public int Suspensions;

    /// <summary>What she was last asked to do, for the renderer and for the text feed.</summary>
    public string Intent = "";

    // ── Reading attributes ─────────────────────────────────────────────────────

    /// <summary>An attribute as she can use it right now, on the familiar 1–20 scale.</summary>
    public float Eff(int raw)
        => (AttributeMid + (raw - Level) * Spread + (Level - AttributeMid) * LevelWeight) * Form * Lift * (0.84f + 0.16f * Freshness);

    /// <summary>
    /// The level of the match she is in — the average of the court players on both sides, or of
    /// the two keepers for a keeper — on the 1–20 scale. Her attributes are read mostly against
    /// it: a match between two modest sides is still a match, not a festival of goals.
    /// </summary>
    public float Level = AttributeMid;

    /// <summary>How much of the match's own level, above or below a typical one, tells (Tuning.LevelWeight).</summary>
    public float LevelWeight = 1f;

    /// <summary>A typical attribute: the point <see cref="Spread"/> pulls towards.</summary>
    public const float AttributeMid = 13f;

    /// <summary>
    /// How much of the gap between her attributes and a typical player's tells on the court
    /// (Tuning.AttributeSpread; 1 for keepers). Real matches between unequal sides are closer
    /// than their squads' numbers suggest.
    /// </summary>
    public float Spread = 1f;

    /// <summary>
    /// What the state of the match does to her side, re-read once a minute: determination when
    /// chasing the game, and the manager's words in a tight finish. 1 otherwise.
    /// </summary>
    public float Lift = 1f;

    /// <summary>
    /// How much of her energy tells, 0–1. The top twelve points are free: a player a few minutes
    /// into a spell plays like a rested one, and the cost starts where a coach would notice it.
    /// Energy is on the scale the side panels show, where a starter ends a half in the sixties.
    /// </summary>
    public float Freshness => MathF.Min(1f, (Energy + 12f) / 100f);

    /// <summary>Tiredness as a 0–1 penalty, for the things that fall apart as legs go.</summary>
    public float Fatigue => 1f - Freshness;

    // The three physical limits are read many times a tick and change only with energy, so
    // they are worked out once per tick by RefreshPhysics rather than on every read.
    private float _maxSpeed, _acceleration, _turning;

    public float MaxSpeed => _maxSpeed;

    /// <summary>
    /// A keeper's shuffle across her goal is short, low and explosive — two quick steps, not a
    /// sprint — so she gets up to speed far faster than an outfielder over the same ground.
    /// </summary>
    public float Acceleration => _acceleration;

    /// <summary>How quickly she changes direction — turning, braking, side-stepping.</summary>
    public float Turning => _turning;

    public void RefreshPhysics()
    {
        float top = IsKeeper ? 4.6f + 1.6f * Player.Pace / 20f : 5.2f + 2.6f * Eff(Player.Pace) / 20f;
        _maxSpeed = top * (0.78f + 0.22f * Freshness);
        _acceleration = (IsKeeper ? 2f : 1f) * (3.0f + 4.0f * Eff(Player.Acceleration) / 20f);
        _turning = _acceleration * (0.8f + 0.6f * Eff(Player.Agility) / 20f);
    }

    /// <summary>Seconds between the ball arriving and her first real decision with it.</summary>
    public float DecisionInterval => 0.22f + 0.28f * (1f - Eff(Player.Decisions) / 20f);

    public float HeightM => (Player.Height > 0 ? Player.Height : 175) / 100f;
}
