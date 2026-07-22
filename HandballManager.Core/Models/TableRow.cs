namespace HandballManager.Models;

/// <summary>
/// One row of a standings table, snapshotted for display.
/// </summary>
/// <remarks>
/// Views must not bind straight to <see cref="LeagueEntry"/> or <see cref="CupGroupEntry"/>.
/// Those are EF entities with no change notification, and EF's identity map hands back the
/// same instances on every reload — so a list rebuilt after a matchday is full of the very
/// objects a live view is already displaying. Their numbers change underneath it and
/// nothing tells the view to redraw, which is why testers saw a single table holding some
/// clubs several games behind and others up to date. Handing the view a new record each
/// load leaves nothing to go stale.
/// </remarks>
public record TableRow(
    int Rank,
    int TeamId,
    Team? Team,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points)
{
    public static TableRow From(LeagueEntry e, int rank)
        => new(rank, e.TeamId, e.Team, e.Played, e.Won, e.Drawn, e.Lost,
               e.GoalsFor, e.GoalsAgainst, e.GoalDifference, e.Points);

    public static TableRow From(CupGroupEntry e, int rank)
        => new(rank, e.TeamId, e.Team, e.Played, e.Won, e.Drawn, e.Lost,
               e.GoalsFor, e.GoalsAgainst, e.GoalDifference, e.Points);

    public static List<TableRow> FromLeague(IEnumerable<LeagueEntry> entries)
        => entries.Select((e, i) => From(e, i + 1)).ToList();

    public static List<TableRow> FromCupGroup(IEnumerable<CupGroupEntry> entries)
        => entries.Select((e, i) => From(e, i + 1)).ToList();
}
