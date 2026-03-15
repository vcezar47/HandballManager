namespace HandballManager.Models;

public class MatchRecord
{
    public int Id { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public DateTime PlayedOn { get; set; }
    public int MatchweekNumber { get; set; }
    public string HomeTeamLogo { get; set; } = string.Empty;
    public string AwayTeamLogo { get; set; } = string.Empty;

    public bool IsCupMatch { get; set; }
    public string? CupRound { get; set; }

    public List<MatchEvent> MatchEvents { get; set; } = [];
    public List<MatchPlayerStat> PlayerStats { get; set; } = [];

    public string Result => $"{HomeTeamName} {HomeGoals} – {AwayGoals} {AwayTeamName}";
}
