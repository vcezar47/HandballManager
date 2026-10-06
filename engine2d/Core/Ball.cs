using System.Numerics;

namespace HandballManager.Engine2D;

public enum BallState
{
    Held,
    /// <summary>A pass on its way. Anyone in its path can get a hand to it.</summary>
    Pass,
    /// <summary>A shot on its way. Its fate was decided at release; the flight is for the eye.</summary>
    Shot,
    /// <summary>Nobody has it. Rolling, bouncing, up for grabs.</summary>
    Loose,
    /// <summary>The whistle has gone; it is with the referee or the thrower.</summary>
    Dead,
}

public enum ShotOutcome { Goal, Saved, SavedHeld, Missed, Post, Blocked }

public sealed class Ball
{
    public BallState State = BallState.Dead;
    public Vector2 Pos = new(Court.CentreX, Court.CentreY);
    public Vector2 PrevPos = new(Court.CentreX, Court.CentreY);
    public float PrevHeight = 1f;

    /// <summary>Height above the floor, for the renderer — a pass is thrown at shoulder height, a jump shot higher.</summary>
    public float Height = 1f;

    public Agent? Holder;

    /// <summary>The last player to touch it, which decides who throws it back in.</summary>
    public Agent? LastTouch;

    // ── In flight ──────────────────────────────────────────────────────────────
    public Vector2 From, To;
    public float FromHeight, ToHeight;
    public float Elapsed, Duration;
    public Agent? Thrower;
    public Agent? Receiver;

    /// <summary>A defender who reached the passing lane and will get to it at this point in the flight.</summary>
    public Agent? Interceptor;
    public float InterceptAt;
    public bool InterceptorCatches;

    public ShotOutcome PendingShot;
    public Agent? ShotKeeper;
    public Agent? ShotBlocker;
    public bool IsSevenMetre;

    /// <summary>A lob into the goal area for a team-mate to take in the air.</summary>
    public bool IsKempa;

    // ── Loose ──────────────────────────────────────────────────────────────────
    public Vector2 Vel;

    public float Progress => Duration <= 0 ? 1f : Math.Clamp(Elapsed / Duration, 0f, 1f);
}
