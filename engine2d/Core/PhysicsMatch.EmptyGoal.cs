using System.Numerics;
using HandballManager.Models;

namespace HandballManager.Engine2D;

/// <summary>
/// The seventh outfielder. A side that pulls its keeper plays seven against six with the ball,
/// and every time it loses it, its goal stands empty until the keeper is back in it.
/// </summary>
/// <remarks>
/// Done the way the rules make teams do it: the keeper runs off through the substitution zone
/// and the extra player comes on there, and the other way round when the ball is gone. The
/// seconds that takes are the whole risk — a defender who wins the ball can throw it into the
/// empty net from her own half, and so can a keeper.
/// </remarks>
public sealed partial class PhysicsMatch
{
    /// <summary>Whether this side is playing with an extra court player instead of a keeper right now.</summary>
    public bool HasExtraAttacker(int side) => Sides[side].Squad.Any(a => a.Role == ExtraRole && a.OnCourt);

    public const string ExtraRole = "Extra";

    private void UpdateEmptyGoal(TeamSide s)
    {
        if (s.IsChanging || Phase is MatchPhase.HalfTime or MatchPhase.FullTime or MatchPhase.Timeout) return;

        bool attacking = Possession == s.Index && Phase == MatchPhase.Play;
        bool want = attacking && (s.Tactics.UseEmptyGoal || _aiEmptyGoal[s.Index]);
        var extra = s.Squad.FirstOrDefault(a => a.Role == ExtraRole && a.OnCourt);
        var keeperOnCourt = s.Keeper;

        if (want && extra == null && keeperOnCourt != null && keeperOnCourt != Ball.Holder)
        {
            var fresh = s.Squad.Where(b => b.State == AgentState.Bench && !b.IsKeeper && b.SuspendedUntil <= 0)
                .OrderByDescending(b => b.Player.Overall100 * (0.7 + 0.3 * b.Energy / 100)).FirstOrDefault();
            if (fresh == null) return;
            fresh.Role = ExtraRole;
            if (!StartSwap(s, keeperOnCourt, fresh)) fresh.Role = fresh.Player.Position;
        }
        else if (!want && extra != null)
        {
            // The ball is gone: whoever is nearest the bench comes off — not necessarily the
            // seventh — and the keeper, waiting at the zone, sprints back to her goal.
            // The keeper the manager named for a seven-metre, if there is one waiting; otherwise the first.
            var keeper = _sevenMetreKeeper is { State: AgentState.Bench, SuspendedUntil: <= 0 } named && named.Side == s.Index
                ? named
                : s.Squad.FirstOrDefault(b => b.State == AgentState.Bench && b.IsKeeperByTrade && b.SuspendedUntil <= 0);
            if (keeper == null) return;
            var zone = SubPoint(s);
            var off = s.Court.Where(a => !a.IsKeeper && a != Ball.Holder)
                .OrderBy(a => Vector2.DistanceSquared(a.Pos, zone)).FirstOrDefault();
            if (off == null) return;
            keeper.Role = "GK";
            if (StartSwap(s, off, keeper) && off != extra)
            {
                // The one who stayed on takes over the position of the one who went off.
                extra.Role = off.Role;
                off.Role = off.Player.Position;
            }
        }
    }

    /// <summary>A change through the zone that may swap a keeper for an outfielder, which an ordinary substitution may not.</summary>
    private bool StartSwap(TeamSide s, Agent outgoing, Agent incoming)
    {
        if (s.IsChanging || outgoing.State != AgentState.OnCourt || incoming.State != AgentState.Bench) return false;
        s.Changes.Add(new SubChange(outgoing, incoming, keepsRoles: true));
        outgoing.State = AgentState.LeavingCourt;
        s.RefreshCourt();
        return true;
    }

    /// <summary>
    /// Whether the goal a side is attacking is effectively empty: no keeper, or a keeper so far
    /// from her line that anyone can shoot at it from anywhere.
    /// </summary>
    private bool GoalIsOpen(TeamSide attacking)
    {
        var k = Opp(attacking).Keeper;
        if (k == null) return true;
        var (u, w) = Frame(attacking, k.Pos);
        return u > 8f || MathF.Abs(w) > 6f;
    }
}
