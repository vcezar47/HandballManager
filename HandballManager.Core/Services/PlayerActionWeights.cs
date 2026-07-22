using HandballManager.Models;

namespace HandballManager.Services;

/// <summary>
/// Shared shooter/assist weighting for both match engines.
/// The quick-sim and the live engine used to distribute goals and assists
/// differently, which let a single player (whoever sat first in the lineup
/// dictionary) collect nearly every assist in a watched match.
/// </summary>
public static class PlayerActionWeights
{
    /// <summary>
    /// Chance a goal is credited with an assist. The old 0.8 produced ~22 team assists
    /// a match; penalties, fast breaks and individual breakthroughs go unassisted, so
    /// real handball lands nearer 55%.
    /// </summary>
    public const double AssistedGoalChance = 0.55;

    /// <summary>How likely a player is to take the shot, before attributes.</summary>
    private static double ShotPositionWeight(string position) => position switch
    {
        "LB" or "RB" => 3.0,
        "Pivot" => 2.4,
        "CB" => 2.0,
        "LW" or "RW" => 1.9,
        _ => 0.0 // keepers do not shoot
    };

    /// <summary>How likely a player is to lay on the assist, before attributes.</summary>
    private static double AssistPositionWeight(string position) => position switch
    {
        "CB" => 3.2,
        "LB" or "RB" => 2.0,
        "Pivot" => 1.0,
        "LW" or "RW" => 0.6,
        _ => 0.0 // keepers do not assist
    };

    /// <summary>
    /// Shot likelihood. Finishing carries it; Agility matters most to wings and
    /// pivots, who score from tight angles rather than distance.
    /// </summary>
    public static double ShotWeight(Player p)
    {
        double w = ShotPositionWeight(p.Position);
        if (w <= 0) return 0;

        bool anglePlayer = p.Position is "LW" or "RW" or "Pivot";
        double skill = anglePlayer
            ? (p.Finishing * 0.7 + p.Agility * 0.3)
            : (p.Finishing * 0.8 + p.Technique * 0.2);

        return w * Math.Max(skill, 1.0) / 10.0;
    }

    /// <summary>
    /// Assist likelihood. Vision decides who sees the pass, Passing whether it
    /// arrives, Decisions whether it was the right ball.
    /// </summary>
    public static double AssistWeight(Player p)
    {
        double w = AssistPositionWeight(p.Position);
        if (w <= 0) return 0;

        double skill = p.Vision * 0.45 + p.Passing * 0.4 + p.Decisions * 0.15;
        return w * Math.Max(skill, 1.0) / 10.0;
    }

    /// <summary>
    /// Weighted random pick. Returns null when every candidate weighs zero,
    /// rather than falling back to the last entry in the list.
    /// </summary>
    public static Player? Pick(IEnumerable<Player?> candidates, Func<Player, double> weight, Random rng, int excludePlayerId = -1)
    {
        var pool = new List<Player>();
        var weights = new List<double>();
        double total = 0;

        foreach (var p in candidates)
        {
            if (p == null || p.Id == excludePlayerId) continue;
            double w = weight(p);
            if (w <= 0) continue;
            pool.Add(p);
            weights.Add(w);
            total += w;
        }

        if (total <= 0) return null;

        double r = rng.NextDouble() * total;
        double running = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            running += weights[i];
            if (r <= running) return pool[i];
        }
        return pool[^1];
    }

    /// <summary>
    /// Share of a squad's minutes a player realistically gets, used by the quick-sim,
    /// which has no lineup to work from and would otherwise spread stats evenly
    /// across the whole roster. Depth is ranked per position by Overall.
    /// </summary>
    public static Dictionary<int, double> MinutesShareByPlayer(Team team)
    {
        var share = new Dictionary<int, double>();
        foreach (var group in team.Players.GroupBy(p => p.Position))
        {
            var ranked = group.OrderByDescending(p => p.Overall).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                share[ranked[i].Id] = i switch
                {
                    0 => 1.0,
                    1 => 0.35,
                    _ => 0.12
                };
            }
        }
        return share;
    }
}
