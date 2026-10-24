using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>
/// Distance, length, dot product, normalisation and blending for the court's vectors, written out
/// one multiplication and one addition at a time.
/// </summary>
/// <remarks>
/// <see cref="Vector2"/>'s own versions are free to use SIMD instructions and fused multiply-adds
/// as the hardware allows: <see cref="Vector2.Lerp(Vector2, Vector2, float)"/> was measured
/// rounding differently from a plain blend on 42% of inputs on one PC, and a phone may do
/// something else again. The engine is chaotic, so every vector it measures goes through here,
/// where the arithmetic is the same on every device. Vector2's + − and × by a number stay: each
/// lane of those is a single IEEE operation and cannot differ.
/// </remarks>
public static class Vec
{
    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    public static float LenSq(Vector2 v) => v.X * v.X + v.Y * v.Y;

    public static float Len(Vector2 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y);

    public static float DistSq(Vector2 a, Vector2 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    public static float Dist(Vector2 a, Vector2 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The unit vector, or NaNs for a zero vector, as <see cref="Vector2.Normalize"/>.</summary>
    public static Vector2 Norm(Vector2 v)
    {
        float len = Len(v);
        return new Vector2(v.X / len, v.Y / len);
    }

    /// <summary>a·(1 − t) + b·t, lane by lane.</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        float s = 1f - t;
        return new Vector2(a.X * s + b.X * t, a.Y * s + b.Y * t);
    }
}
