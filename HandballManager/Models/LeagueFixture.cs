namespace HandballManager.Models;

public class LeagueFixture
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public int Round { get; set; } // Matchweek number
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public bool IsPlayed { get; set; }
    public int? MatchRecordId { get; set; }

    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public string CompetitionName { get; set; } = "Liga Florilor";
    public MatchRecord? MatchRecord { get; set; }
}
