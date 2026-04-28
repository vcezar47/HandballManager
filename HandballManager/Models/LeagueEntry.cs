namespace HandballManager.Models;

public class LeagueEntry
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Points => Won * 2 + Drawn;
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public string CompetitionName { get; set; } = "Liga Florilor"; // Default for existing data

    /// <summary>Set by LeagueService after sorting. Not persisted to DB.</summary>
    public int Rank { get; set; }
}