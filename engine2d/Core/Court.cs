using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>
/// The playing court, in metres, laid out the way the IHF rules draw it: 40 by 20, a goal at
/// each end, the goal area six metres from the goal and the dashed free-throw line nine.
/// </summary>
/// <remarks>
/// World coordinates run x along the length (0 at one goal line, 40 at the other) and y across
/// it (0 on the bench side). Almost every decision is easier in the <em>attacking frame</em>
/// instead: <c>u</c> is how far from the goal being attacked, <c>w</c> how far to the attacker's
/// left of its centre. A left wing stands at a large positive <c>w</c> whichever end she is
/// attacking, so the brains are written once and the halves swap by flipping a sign.
/// </remarks>
public static class Court
{
    public const float Length = 40f;
    public const float Width = 20f;
    public const float CentreY = Width / 2f;

    public const float GoalWidth = 3f;
    public const float GoalHeight = 2f;
    public const float PostHalf = GoalWidth / 2f;

    public const float GoalAreaRadius = 6f;
    public const float FreeThrowRadius = 9f;
    public const float SevenMetreMark = 7f;
    public const float KeeperLine = 4f;

    /// <summary>The substitution zone runs 4.5 m either side of the centre line, on the bench side.</summary>
    public const float SubZoneHalf = 4.5f;

    public const float CentreX = Length / 2f;

    // ── The attacking frame ────────────────────────────────────────────────────

    /// <summary>World position of a point given in a side's attacking frame.</summary>
    public static Vector2 ToWorld(float goalX, float dir, float u, float w)
        => new(goalX - dir * u, CentreY + dir * w);

    /// <summary>A world position in the attacking frame of the goal at <paramref name="goalX"/>.</summary>
    public static (float U, float W) ToFrame(float goalX, float dir, Vector2 p)
        => (dir * (goalX - p.X), dir * (p.Y - CentreY));

    /// <summary>
    /// Distance from the goal mouth — the segment between the posts. The goal area is every
    /// point closer than six metres to it, which is what gives the line its rounded corners.
    /// </summary>
    public static float DistanceToGoalMouth(float u, float w)
    {
        float dw = MathF.Max(0f, MathF.Abs(w) - PostHalf);
        return MathF.Sqrt(u * u + dw * dw);
    }

    public static bool InGoalArea(float u, float w) => u >= 0 && DistanceToGoalMouth(u, w) < GoalAreaRadius;

    /// <summary>
    /// The nearest point on a line drawn <paramref name="radius"/> metres from the goal mouth —
    /// how a court player is kept out of the goal area, and how a free throw awarded inside the
    /// nine metres is moved back to it.
    /// </summary>
    public static (float U, float W) PushOutTo(float u, float w, float radius)
    {
        u = MathF.Max(u, 0.01f);
        float side = w < 0 ? -1f : 1f;
        float dw = MathF.Abs(w) - PostHalf;

        // Between the posts the line is straight.
        if (dw <= 0) return (MathF.Max(u, radius), w);

        // Beyond them it is a quarter circle about the post.
        float d = MathF.Sqrt(u * u + dw * dw);
        if (d >= radius) return (u, w);
        float k = radius / d;
        return (u * k, side * (PostHalf + dw * k));
    }

    public static bool InsideCourt(Vector2 p) => p.X >= 0 && p.X <= Length && p.Y >= 0 && p.Y <= Width;

    public static Vector2 Clamp(Vector2 p, float margin = 0.3f)
        => new(Math.Clamp(p.X, margin, Length - margin), Math.Clamp(p.Y, margin, Width - margin));
}
