using System.Numerics;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.Engine2D;

/// <summary>
/// Injuries. The risk per minute on court is the same calibrated roll the classic engine and
/// the season use (<see cref="InjuryCatalogue.RollMatchInjury"/>), so the 2D engine hurts
/// nobody more or less often than the rest of the game. What it adds is the scene: she goes
/// down, the referees stop it, she is helped off, and somebody comes on.
/// </summary>
public sealed partial class PhysicsMatch
{
    private int _lastInjuryMinute;

    private void RollInjuries()
    {
        int minute = (int)(Clock / 60f);
        if (minute == _lastInjuryMinute) return;
        _lastInjuryMinute = minute;

        foreach (var s in Sides)
        {
            foreach (var a in s.Court.ToList())
            {
                var injury = InjuryCatalogue.RollMatchInjury(a.Player, 1.0 / 60.0, Player.GlobalGameDate,
                    s.Team.TrainingFacilityLevel, _rng);
                if (injury == null) continue;

                // No database here: the flag keeps the rest of the match honest, and the season
                // files the injury and its return date when it records the result.
                a.Player.IsInjured = true;
                MatchInjuries.Add(injury);
                Emit(EventKind.Injury, s.Index, a);
                LastInjuryName = injury.Name;
                GoDown(s, a);
            }
        }
    }

    /// <summary>The name of the last injury, for the commentary line.</summary>
    public string LastInjuryName { get; private set; } = "";

    private void GoDown(TeamSide s, Agent hurt)
    {
        // The referees stop play for a player down; whoever had the ball keeps it.
        if (Phase == MatchPhase.Play && Possession >= 0)
        {
            var spot = Ball.Holder?.Pos ?? Ball.Pos;
            var keepIt = Ball.Holder == hurt ? null : Ball.Holder;
            AwardFreeThrow(Possession, spot, keepIt != null && keepIt.Side == Possession ? keepIt : null);
        }
        if (Ball.Holder == hurt) Ball.Holder = null;

        hurt.State = AgentState.Injured;
        hurt.StunnedUntil = Clock + 6f;
        s.Changes.RemoveAll(c => c.Leaving == hurt);
        s.RefreshCourt();

        // Not somebody already waiting at the line to replace a team-mate.
        bool Free(Agent b) => b.State == AgentState.Bench && b.SuspendedUntil <= 0 && s.Changes.All(c => c.Entering != b);
        var replacement = s.Squad.FirstOrDefault(b => Free(b) && b.Player.Position == hurt.Role)
                          ?? (hurt.IsKeeper ? null
                              : s.Squad.FirstOrDefault(b => Free(b) && !b.IsKeeper));
        if (hurt.IsKeeper && replacement == null)
            replacement = s.Squad.FirstOrDefault(b => Free(b) && b.Player.Position == "GK");

        if (replacement != null)
        {
            replacement.Role = hurt.Role;
            replacement.State = AgentState.OnCourt;
            replacement.Pos = SubPoint(s);
            replacement.Vel = Vector2.Zero;
            EnsureStat(replacement);
            Emit(EventKind.Substitution, s.Index, replacement, hurt);
        }
        else if (hurt.IsKeeper)
        {
            // Nobody else to go in goal: the least valuable outfielder pulls on the shirt.
            var standIn = s.Court.Where(a => !a.IsKeeper).OrderBy(a => a.Player.Overall100).FirstOrDefault();
            if (standIn != null)
            {
                standIn.Role = "GK";
                Emit(EventKind.Substitution, s.Index, standIn, hurt);
            }
        }
        s.RefreshCourt();
    }
}
