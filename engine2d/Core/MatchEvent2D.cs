using System.Numerics;

namespace HandballManager.Engine2D;

public enum EventKind
{
    ThrowOff,
    Goal,
    Save,
    Miss,
    Post,
    Block,
    Turnover,
    Interception,
    FreeThrow,
    OffensiveFoul,
    SevenMetreAwarded,
    SevenMetreGoal,
    SevenMetreSaved,
    SevenMetreMissed,
    TwoMinutes,
    RedCard,
    PassiveWarning,
    PassivePlay,
    Substitution,
    Timeout,
    Injury,
    EmptyGoal,
    KempaAttempt,
    KempaFailed,
    SuspensionOver,
    HalfTime,
    ExtraTime,
    ShootoutGoal,
    ShootoutMiss,
    FullTime,
}

/// <summary>
/// Something the referee, the scorer or the commentator would note. The renderer reads the
/// agents directly; these are for the feed, the stat sheet and the shot map.
/// </summary>
public sealed record MatchEvent2D(
    float Time,
    EventKind Kind,
    int Side,
    int PlayerId,
    string PlayerName,
    int OtherPlayerId = 0,
    string OtherPlayerName = "",
    Vector2 Where = default,
    float GameTime = -1f)
{
    /// <summary>The match clock when it happened: it stops for time-outs and seven-metres, <see cref="Time"/> does not.</summary>
    public float OnTheClock => GameTime >= 0f ? GameTime : Time;

    public int Minute => (int)(OnTheClock / 60f) + 1;
    public int Second => (int)(OnTheClock % 60f);
}
