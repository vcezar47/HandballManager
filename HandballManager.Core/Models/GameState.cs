namespace HandballManager.Models;

/// <summary>
/// Single-row table (Id == 1) that persists the game state which does NOT live
/// naturally in the domain tables: the current season year, the game clock, and
/// the simulation engine's progression/wage bookkeeping dates. Saved alongside the
/// rest of the database so a copied .hbm file is a complete, self-contained save.
/// </summary>
public class GameState
{
    public int Id { get; set; }

    /// <summary>Save format version — bumped when the schema/meaning changes so old/newer saves can be rejected gracefully.</summary>
    public int SaveFormatVersion { get; set; }

    public DateTime SavedAtUtc { get; set; }

    public int CurrentSeasonYear { get; set; }
    public DateTime CurrentDate { get; set; }
    public DateTime LastDailyProgressionDate { get; set; }
    public DateTime LastWeeklyWageDate { get; set; }

    // Purely for display on the (future) load screen / save confirmation.
    public string ClubName { get; set; } = string.Empty;
    public string SeasonLabel { get; set; } = string.Empty;
}
