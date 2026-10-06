using System.Numerics;

namespace HandballManager.Engine2D;

/// <summary>One of the two referees: where she is, and which job she is doing this attack.</summary>
public sealed class Referee
{
    public Vector2 Pos, PrevPos;

    /// <summary>On the goal line of the goal being attacked, rather than trailing the play.</summary>
    public bool OnGoalLine;
}

/// <summary>
/// The referee pair. They make no decisions — the rules are applied where the contact happens —
/// but they move the way a real pair does, so the court looks refereed.
/// </summary>
/// <remarks>
/// Handball referees swap jobs at every change of possession: the one on the goal line of the
/// old attack turns and trails the new one as court referee, and the old court referee runs
/// ahead to the far goal line. The court referee stands off the play on the far side, a few
/// metres behind the ball; the goal-line referee on the bench side of the goal, just outside
/// the post.
/// </remarks>
public sealed partial class PhysicsMatch
{
    public Referee[] Referees { get; } =
    [
        new() { Pos = new Vector2(Court.CentreX - 6f, 15f), PrevPos = new Vector2(Court.CentreX - 6f, 15f) },
        new() { Pos = new Vector2(Court.CentreX + 6f, 5f), PrevPos = new Vector2(Court.CentreX + 6f, 5f), OnGoalLine = true },
    ];

    private int _refereedAttackSide = -1;

    private void MoveReferees()
    {
        foreach (var r in Referees) r.PrevPos = r.Pos;
        if (Possession < 0) return;

        var s = Sides[Possession];
        if (Possession != _refereedAttackSide)
        {
            // Change of possession: whoever is nearer the new attack's goal takes the goal line.
            _refereedAttackSide = Possession;
            var goal = new Vector2(s.AttackGoalX, Court.CentreY);
            bool firstNearer = Vector2.Distance(Referees[0].Pos, goal) < Vector2.Distance(Referees[1].Pos, goal);
            Referees[0].OnGoalLine = firstNearer;
            Referees[1].OnGoalLine = !firstNearer;
        }

        foreach (var r in Referees)
        {
            Vector2 target = r.OnGoalLine
                ? new Vector2(s.AttackGoalX - s.Dir * 0.4f, 4.2f)
                : new Vector2(Math.Clamp(Ball.Pos.X - s.Dir * 6.5f, 2f, Court.Length - 2f), Court.Width - 3.5f);

            var d = target - r.Pos;
            float len = d.Length();
            float step = MathF.Min(len, 6.5f * Dt);
            if (len > 0.01f) r.Pos += d / len * step;
        }
    }
}
