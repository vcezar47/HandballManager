using System.Numerics;
using HandballManager.Engine2D;
using Microsoft.Maui.Graphics;
using Court = HandballManager.Engine2D.Court;

namespace HandballManager.Mobile.Match2D;

/// <summary>
/// Everything that moves: the players in their kits, the ball, the two referees. Redrawn every
/// frame, between engine ticks, from where everybody was a tick ago to where they are now.
/// </summary>
public sealed class PlayersDrawable(Match2DViewModel vm) : IDrawable
{
    private readonly Match2DViewModel _vm = vm;
    private readonly KitColours.MatchKits _kits = vm.Look.Kits;

    /// <summary>Player id → highlighted, for the substitution picker.</summary>
    public int Highlighted { get; set; }

    public void Draw(ICanvas canvas, RectF rect)
    {
        var from = _vm.From;
        var to = _vm.To;
        var t = new CourtTransform(rect);
        float a = _vm.Alpha;
        float radius = MathF.Max(6f, t.Metres(0.42f));
        canvas.Antialias = true;

        // The referees.
        foreach (var (p0, p1) in new[] { (from.Ref0, to.Ref0), (from.Ref1, to.Ref1) })
        {
            var at = t.Map(Vector2.Lerp(p0, p1, a));
            float rr = radius * 0.8f;
            canvas.FillColor = Color.FromArgb("#111111");
            canvas.FillCircle(at, rr);
            canvas.StrokeColor = Color.FromArgb("#FF4081");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(at, rr);
        }

        // Bench and the suspended first, then the court, so the court players are on top.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < to.Count; i++)
            {
                var p = to.Players[i];
                bool onCourt = p.State is AgentState.OnCourt or AgentState.LeavingCourt;
                if (onCourt != (pass == 1)) continue;
                // Who is sitting down is in the panels beside the court. Drawn: whoever is walking
                // back to the bench, waiting at the line to come on, or on her way to serve two minutes.
                if (p.State == AgentState.Bench ? !p.Touchline : !onCourt && p.Pos.Y < -1.1f) continue;
                var prev = i < from.Count && from.Players[i].Id == p.Id ? from.Players[i].Pos : p.Pos;
                DrawPlayer(canvas, t, p, Vector2.Lerp(prev, p.Pos, a), radius, onCourt);
            }
        }

        DrawBall(canvas, t, from, to, a, radius);
    }

    private void DrawPlayer(ICanvas canvas, CourtTransform t, PlayerShot p, Vector2 pos, float radius, bool onCourt)
    {
        var kit = p.Keeper ? (p.Side == 0 ? _kits.HomeKeeper : _kits.AwayKeeper) : (p.Side == 0 ? _kits.Home : _kits.Away);
        var at = t.Map(pos);
        bool faded = p.State is AgentState.Suspended or AgentState.SentOff or AgentState.Injured;
        // Benched players are drawn small, a metre apart, so their numbers can still be read.
        float r = onCourt ? radius : MathF.Min(radius * 0.8f, MathF.Max(4.5f, t.Metres(0.4f)));
        if (p.Down) r = radius;                                                               // down on the court
        if (p.Airborne) { r = radius * 1.25f; at = new PointF(at.X, at.Y - radius * 0.8f); } // in the air
        canvas.Alpha = faded ? 0.45f : 1f;

        // A shadow grounds them on the floor.
        canvas.FillColor = Color.FromArgb("#55000000");
        canvas.FillEllipse(at.X - r + 1.5f, at.Y - r + 2.5f, r * 2, r * 2);

        canvas.FillColor = Color.FromArgb(kit.Shirt);
        canvas.FillCircle(at, r);
        canvas.StrokeColor = p.Id == Highlighted ? Colors.Gold : Color.FromArgb("#AA000000");
        canvas.StrokeSize = p.Id == Highlighted ? 3f : 1f;
        canvas.DrawCircle(at, r);

        if (p.HasBall)
        {
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(at, r + 3f);
        }

        canvas.FontColor = Color.FromArgb(kit.Number);
        canvas.FontSize = r * 1.05f;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(p.Number.ToString(), at.X - r, at.Y - r, r * 2, r * 2, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.Alpha = 1f;

        if (p.State == AgentState.Injured)
        {
            canvas.StrokeColor = Color.FromArgb("#E53935");
            canvas.StrokeSize = 3f;
            canvas.DrawLine(at.X - r * 0.5f, at.Y, at.X + r * 0.5f, at.Y);
            canvas.DrawLine(at.X, at.Y - r * 0.5f, at.X, at.Y + r * 0.5f);
        }
        if (p.State == AgentState.SentOff)
        {
            canvas.FillColor = Color.FromArgb("#E53935");
            canvas.FillRectangle(at.X + r * 0.4f, at.Y - r * 1.3f, r * 0.55f, r * 0.75f);
        }
    }

    private static void DrawBall(ICanvas canvas, CourtTransform t, FrameShot from, FrameShot to, float a, float radius)
    {
        var ground = Vector2.Lerp(from.Ball, to.Ball, a);
        float h = from.BallHeight + (to.BallHeight - from.BallHeight) * a;
        var at = t.Map(ground);

        // In a player's hands it sits at her shoulder; in the air it lifts off its shadow.
        if (to.BallState == BallState.Held) at = new PointF(at.X + radius * 0.8f, at.Y - radius * 0.6f);
        float r = MathF.Max(3.5f, t.Metres(0.19f));

        canvas.FillColor = Color.FromArgb("#66000000");
        canvas.FillEllipse(at.X - r, at.Y - r * 0.5f, r * 2, r);

        var lifted = new PointF(at.X, at.Y - t.Metres(h * 0.35f));
        canvas.FillColor = Color.FromArgb("#FFD54F");
        canvas.FillCircle(lifted, r);
        canvas.StrokeColor = Color.FromArgb("#1565C0");
        canvas.StrokeSize = 1.2f;
        canvas.DrawCircle(lifted, r);
    }
}
