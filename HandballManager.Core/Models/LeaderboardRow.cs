namespace HandballManager.Models;

/// <summary>
/// Which strand of a country's season a stat belongs to. Match records carry only
/// <c>IsCupMatch</c> plus a round label, so the supercup is identified by its
/// "Supercup ..." round rather than a field of its own.
/// </summary>
public enum CompetitionType
{
    League,
    Cup,
    Supercup
}

/// <summary>
/// One row of a season leaderboard. <see cref="Stat"/> is preformatted so goals,
/// assists, saves and average rating can all share a single list template.
/// <see cref="StatDetail"/> is the working behind it where one exists — "142 / 386"
/// under a save percentage — and empty otherwise.
/// </summary>
public record LeaderboardRow(int Rank, string PlayerName, string TeamName, string TeamLogo, string Position, int MatchesPlayed, string Stat, string StatDetail = "")
{
    public bool HasStatDetail => StatDetail.Length > 0;
}

/// <summary>The four season leaderboards for one competition.</summary>
public record LeagueLeaderboards(
    List<LeaderboardRow> TopScorers,
    List<LeaderboardRow> TopAssists,
    List<LeaderboardRow> TopSaves,
    List<LeaderboardRow> TopRated)
{
    public static LeagueLeaderboards Empty => new([], [], [], []);
}
