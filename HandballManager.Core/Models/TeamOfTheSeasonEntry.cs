namespace HandballManager.Models;

/// <summary>
/// One player in a league's Team of the Season, captured at the end of a season.
///
/// Team of the Week needs no equivalent table — match stats live in the database for
/// the whole season and are queried on demand. Team of the Season does need storing,
/// because <c>ProcessEndOfSeasonAsync</c> wipes every match record before the new
/// season starts, and these should survive for the life of the save.
/// </summary>
public class TeamOfTheSeasonEntry
{
    public int Id { get; set; }

    public string CompetitionName { get; set; } = string.Empty;

    /// <summary>e.g. "2025/2026".</summary>
    public string Season { get; set; } = string.Empty;

    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;

    /// <summary>GK, LW, LB, CB, RB, RW or Pivot.</summary>
    public string Position { get; set; } = string.Empty;

    public double AverageRating { get; set; }
    public int MatchesPlayed { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Saves { get; set; }
}
