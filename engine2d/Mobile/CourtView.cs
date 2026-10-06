using System.Numerics;
using HandballManager.Engine2D;
using Microsoft.Maui.Graphics;
using Court = HandballManager.Engine2D.Court;

namespace HandballManager.Mobile.Match2D;

/// <summary>
/// Where the court sits in a view of a given size: a 40×20 court with room for the benches
/// below it and the goals at either end, scaled to fit and centred.
/// </summary>
public readonly struct CourtTransform
{
    /// <summary>
    /// The world rectangle that has to fit: the nets behind each goal, and the benches below the
    /// near touchline, where substitutes walk off and wait to come on.
    /// </summary>
    private const float MinX = -1.05f, MaxX = Court.Length + 1.05f, MinY = -1.9f, MaxY = Court.Width + 0.3f;

    public readonly float Scale, OffsetX, OffsetY;

    public CourtTransform(RectF view)
    {
        Scale = MathF.Min(view.Width / (MaxX - MinX), view.Height / (MaxY - MinY));
        OffsetX = view.X + (view.Width - (MaxX - MinX) * Scale) / 2f - MinX * Scale;
        OffsetY = view.Y + (view.Height - (MaxY - MinY) * Scale) / 2f + MaxY * Scale;
    }

    /// <summary>World metres to screen points. The bench side (y = 0) is at the bottom, as a TV camera sees it.</summary>
    public PointF Map(Vector2 p) => new(OffsetX + p.X * Scale, OffsetY - p.Y * Scale);

    public PointF Map(float x, float y) => new(OffsetX + x * Scale, OffsetY - y * Scale);

    public float Metres(float m) => m * Scale;
}

/// <summary>The court itself: floor, lines, goal areas, goals and benches. Drawn once per size or theme.</summary>
public sealed class CourtDrawable(CourtTheme theme) : IDrawable
{
    public CourtTheme Theme { get; set; } = theme;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var t = new CourtTransform(rect);
        var th = Theme;
        canvas.Antialias = true;

        canvas.FillColor = Color.FromArgb(th.Surround);
        canvas.FillRectangle(rect);

        // The floor.
        var tl = t.Map(0, Court.Width);
        canvas.FillColor = Color.FromArgb(th.Floor);
        canvas.FillRectangle(tl.X, tl.Y, t.Metres(Court.Length), t.Metres(Court.Width));

        // Goal areas, with the rounded corners the six-metre line really has.
        foreach (float gx in new[] { 0f, Court.Length })
        {
            float dir = gx == 0 ? 1f : -1f;
            var area = AreaPath(t, gx, dir, Court.GoalAreaRadius);
            canvas.FillColor = Color.FromArgb(th.GoalArea);
            canvas.FillPath(area);
        }

        float line = MathF.Max(1.2f, t.Metres(0.05f) * 1.6f);
        canvas.StrokeColor = Color.FromArgb(th.Lines);
        canvas.StrokeSize = line;

        // Outer lines and the halfway line.
        canvas.DrawRectangle(tl.X, tl.Y, t.Metres(Court.Length), t.Metres(Court.Width));
        var c0 = t.Map(Court.CentreX, 0);
        var c1 = t.Map(Court.CentreX, Court.Width);
        canvas.DrawLine(c0, c1);

        // The throw-off area in the middle, where the competition's crest sits.
        var mid = t.Map(Court.CentreX, Court.CentreY);
        canvas.FillColor = Color.FromArgb("#26FFFFFF");
        canvas.FillCircle(mid, t.Metres(2f));
        canvas.DrawCircle(mid, t.Metres(2f));

        // The two benches, either side of the zone.
        canvas.FillColor = Color.FromArgb("#14FFFFFF");
        foreach (float dir in new[] { -1f, 1f })
        {
            var b0 = t.Map(Court.CentreX + dir * 2.7f, -0.95f);
            var b1 = t.Map(Court.CentreX + dir * 19.0f, -1.65f);
            canvas.FillRoundedRectangle(MathF.Min(b0.X, b1.X), b0.Y, MathF.Abs(b1.X - b0.X), b1.Y - b0.Y, t.Metres(0.3f));
        }

        // Substitution lines: 4.5 m either side of halfway, on the bench side.
        foreach (float sx in new[] { Court.CentreX - Court.SubZoneHalf, Court.CentreX + Court.SubZoneHalf })
            canvas.DrawLine(t.Map(sx, -0.15f), t.Map(sx, 0.15f));

        foreach (float gx in new[] { 0f, Court.Length })
        {
            float dir = gx == 0 ? 1f : -1f;
            canvas.StrokeColor = Color.FromArgb(th.Lines);
            canvas.StrokeDashPattern = null;
            canvas.DrawPath(AreaPath(t, gx, dir, Court.GoalAreaRadius, closeAlongGoalLine: false));

            // The nine-metre line is dashed, and clipped to the court.
            canvas.StrokeColor = Color.FromArgb(th.NineMetre);
            canvas.StrokeDashPattern = [4f, 3f];
            canvas.DrawPath(AreaPath(t, gx, dir, Court.FreeThrowRadius, closeAlongGoalLine: false));
            canvas.StrokeDashPattern = null;

            // Seven-metre mark and the keeper's four-metre line.
            canvas.StrokeColor = Color.FromArgb(th.Lines);
            canvas.DrawLine(t.Map(gx + dir * Court.SevenMetreMark, Court.CentreY - 0.5f), t.Map(gx + dir * Court.SevenMetreMark, Court.CentreY + 0.5f));
            canvas.DrawLine(t.Map(gx + dir * Court.KeeperLine, Court.CentreY - 0.075f), t.Map(gx + dir * Court.KeeperLine, Court.CentreY + 0.075f));

            // The goal: posts painted in the usual two-colour bands, and the net behind.
            var g0 = t.Map(gx, Court.CentreY + Court.PostHalf);
            var net = t.Map(gx - dir * 1.0f, Court.CentreY - Court.PostHalf);
            canvas.FillColor = Color.FromArgb("#40FFFFFF");
            canvas.FillRectangle(MathF.Min(g0.X, net.X), g0.Y, MathF.Abs(net.X - g0.X), net.Y - g0.Y);
            canvas.StrokeSize = line * 2f;
            canvas.StrokeColor = Colors.White;
            canvas.DrawLine(g0, t.Map(gx, Court.CentreY - Court.PostHalf));
            canvas.StrokeColor = Color.FromArgb("#E53935");
            canvas.StrokeDashPattern = [3f, 3f];
            canvas.DrawLine(g0, t.Map(gx, Court.CentreY - Court.PostHalf));
            canvas.StrokeDashPattern = null;
            canvas.StrokeSize = line;
        }

    }

    /// <summary>
    /// The line <paramref name="radius"/> metres from a goal mouth: a quarter circle about each
    /// post joined by a straight between them, clipped to the court's side lines.
    /// </summary>
    private static PathF AreaPath(CourtTransform t, float gx, float dir, float radius, bool closeAlongGoalLine = true)
    {
        var path = new PathF();
        float lo = Court.CentreY - Court.PostHalf, hi = Court.CentreY + Court.PostHalf;

        // Lower quarter circle, from where it meets the side line (or the goal line) to straight ahead.
        float start = -MathF.PI / 2f;
        if (lo - radius < 0f) start = MathF.Asin(-lo / radius);
        bool first = true;
        for (int i = 0; i <= 24; i++)
        {
            float a = start + (0f - start) * i / 24f;
            var p = t.Map(gx + dir * radius * MathF.Cos(a), lo + radius * MathF.Sin(a));
            if (first) { path.MoveTo(p); first = false; } else path.LineTo(p);
        }
        path.LineTo(t.Map(gx + dir * radius, hi));
        float end = MathF.PI / 2f;
        if (hi + radius > Court.Width) end = MathF.Asin((Court.Width - hi) / radius);
        for (int i = 0; i <= 24; i++)
        {
            float a = end * i / 24f;
            path.LineTo(t.Map(gx + dir * radius * MathF.Cos(a), hi + radius * MathF.Sin(a)));
        }
        if (closeAlongGoalLine) path.Close();
        return path;
    }
}
