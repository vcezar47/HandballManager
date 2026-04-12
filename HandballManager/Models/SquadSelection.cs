using System.Collections.ObjectModel;
using System.Linq;

namespace HandballManager.Models;

public class SquadSelection
{
    public int TeamId { get; set; }
    public ObservableCollection<Player> Substitutes { get; set; } = new();
    
    // Master Starting Lineup mapping position -> Player
    public Dictionary<string, Player?> StartingLineup { get; set; } = new()
    {
        {"GK", null}, {"LW", null}, {"LB", null}, {"CB", null}, {"RB", null}, {"RW", null}, {"Pivot", null}
    };

    public bool IsValid()
    {
        // 1. Must have all 7 on-court players.
        bool startersFilled = StartingLineup.Values.All(p => p != null);
        if (!startersFilled) return false;

        // 2. Starting lineup must have 7 distinct players
        var startingDistinct = StartingLineup.Values.Where(p => p != null).Distinct().Count();
        if (startingDistinct != 7) return false;

        // 3. User just wants to ensure 7 subs are on the bench. 
        // We drop the strict position-validation rule so managers can pick out-of-position backups.
        return Substitutes.Count >= 7;
    }

    public void AutoPickBestSquad(List<Player> roster)
    {
        StartingLineup.Clear();
        Substitutes.Clear();

        var available = roster.Where(p => !p.IsInjured).OrderByDescending(p => p.Overall100).ToList();

        // 1. Pick Best Starters (one for each spot)
        string[] pTypes = ["GK", "LW", "LB", "CB", "RB", "RW", "Pivot"];
        foreach(var pos in pTypes)
        {
            var best = available.FirstOrDefault(p => p.Position == pos) ?? available.FirstOrDefault();
            if (best != null) 
            {
                available.Remove(best);
                StartingLineup[pos] = best;
            }
        }

        // 2. Pick Bench Coverage (one for each spot)
        foreach(var pos in pTypes)
        {
            var bestSub = available.FirstOrDefault(p => p.Position == pos) ?? available.FirstOrDefault();
            if (bestSub != null)
            {
                available.Remove(bestSub);
                Substitutes.Add(bestSub);
            }
        }

        // 3. Pick Remaining 2 bench spots (best overall)
        foreach(var extra in available.Take(2))
        {
            Substitutes.Add(extra);
        }
    }
}
