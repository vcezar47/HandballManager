using Microsoft.EntityFrameworkCore;

namespace HandballManager.Data;

/// <summary>
/// Brings an existing save's schema up to date.
///
/// The game builds its database with <c>EnsureCreated</c>, not EF migrations, so a
/// save file created by an older build keeps whatever schema it was born with — a
/// table added to the model later simply will not exist there, and querying it
/// throws "no such table". Every table added after a public release therefore needs
/// a CREATE TABLE IF NOT EXISTS here, and this must run before the first query on
/// any loaded save. New columns need the same treatment — SQLite has no
/// "ADD COLUMN IF NOT EXISTS", so those go through <see cref="AddColumnAsync"/>.
/// </summary>
public static class SchemaUpgrader
{
    /// <summary>
    /// Idempotent. Safe on brand new databases (everything already exists) and on
    /// saves from any earlier build.
    /// </summary>
    public static async Task UpgradeAsync(HandballDbContext db)
    {
        // Added 2026-07: Team of the Season archive.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TeamOfTheSeasonEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TeamOfTheSeasonEntries" PRIMARY KEY AUTOINCREMENT,
                "CompetitionName" TEXT NOT NULL,
                "Season" TEXT NOT NULL,
                "PlayerId" INTEGER NOT NULL,
                "PlayerName" TEXT NOT NULL,
                "TeamName" TEXT NOT NULL,
                "Position" TEXT NOT NULL,
                "AverageRating" REAL NOT NULL,
                "MatchesPlayed" INTEGER NOT NULL,
                "Goals" INTEGER NOT NULL,
                "Assists" INTEGER NOT NULL,
                "Saves" INTEGER NOT NULL
            );
            """);

        // Added 2026-07: shots taken and conceded, previously computed in-match and
        // discarded. Rows written before this hold 0, so a save percentage built from
        // them reads as "no shots faced" rather than a bogus 100%.
        await AddColumnAsync(db, "MatchPlayerStats", "Shots", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnAsync(db, "MatchPlayerStats", "GoalsAgainst", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Adds a column unless it is already there. SQLite rejects a duplicate ADD COLUMN
    /// outright, so the column list is checked first.
    /// </summary>
    private static async Task AddColumnAsync(HandballDbContext db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
            var existing = Convert.ToInt64(await probe.ExecuteScalarAsync() ?? 0L);
            if (existing > 0) return;
        }

        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
    }
}
