namespace HandballManager.Models;

public class ChampionRecord
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
}
