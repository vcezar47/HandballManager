namespace HandballManager.Models;

public class ChampionRecord
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    /// <summary>Champion (gold).</summary>
    public string TeamName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string CompetitionName { get; set; } = "Liga Florilor";

    /// <summary>Optional — used for leagues with full podium history (e.g. Kvindeligaen).</summary>
    public string? RunnerUpTeamName { get; set; }

    /// <summary>Optional bronze medalist.</summary>
    public string? ThirdPlaceTeamName { get; set; }
}
