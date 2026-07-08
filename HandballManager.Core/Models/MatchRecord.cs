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
    public int Attendance { get; set; }
    public string VenueName { get; set; } = string.Empty;

    public int HomePenaltyGoals { get; set; }
    public int AwayPenaltyGoals { get; set; }
    public bool WasDecidedByShootout { get; set; }
    public bool WasDecidedByOvertime { get; set; }

    public List<MatchEvent> MatchEvents { get; set; } = [];
    public List<MatchPlayerStat> PlayerStats { get; set; } = [];

    /// <summary>UI-only stub for fixtures not played yet (avoids displaying 0–0 as a result).</summary>
    public bool IsUnplayedPlaceholder { get; set; }

    /// <summary>Optional subtitle shown under fixture rows on the home dashboard (e.g. Kvindeligaen phase).</summary>
    public string? LeagueSubtitle { get; set; }

    /// <summary>Short score row for dashboards (shows "vs" for scheduled fixtures).</summary>
    public string DashboardScoreDisplay => IsUnplayedPlaceholder ? "vs" : $"{HomeGoals} – {AwayGoals}";

    public string Result {
        get {
            string res = $"{HomeTeamName} {HomeGoals} – {AwayGoals} {AwayTeamName}";
            if (WasDecidedByShootout) res += $" (P: {HomePenaltyGoals}-{AwayPenaltyGoals})";
            else if (WasDecidedByOvertime) res += " (AET)";
            return res;
        }
    }
}
