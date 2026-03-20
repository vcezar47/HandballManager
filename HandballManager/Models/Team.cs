namespace HandballManager.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal TransferBudget { get; set; }
    public decimal WageBudget { get; set; }
    public decimal ClubBalance { get; set; }
    public int FoundedYear { get; set; }
    public string StadiumName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPlayerTeam { get; set; }
    public string LogoPath { get; set; } = string.Empty;
    public string Nation { get; set; } = string.Empty;

    public List<Player> Players { get; set; } = [];
    public LeagueEntry? LeagueEntry { get; set; }
}
