using System.Data;
using System.IO;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Services;

/// <summary>
/// Reads/writes the single <see cref="GameState"/> row that carries the non-DB game
/// state (season year, clock, progression cursors) inside the database file itself.
/// </summary>
public class GameStateService
{
    /// <summary>Bump when the save schema/meaning changes. Older saves with a higher version are refused.</summary>
    public const int CurrentSaveVersion = 1;

    private readonly HandballDbContext _db;

    public GameStateService(HandballDbContext db) => _db = db;

    public async Task WriteStateAsync(GameClock clock, SimulationEngine engine)
    {
        var state = await _db.GameStates.FirstOrDefaultAsync();
        if (state == null)
        {
            state = new GameState { Id = 1 };
            _db.GameStates.Add(state);
        }

        state.SaveFormatVersion = CurrentSaveVersion;
        state.SavedAtUtc = DateTime.UtcNow;
        state.CurrentSeasonYear = LeagueService.CurrentSeasonYear;
        state.CurrentDate = clock.CurrentDate;
        state.LastDailyProgressionDate = engine.LastDailyProgressionDate;
        state.LastWeeklyWageDate = engine.LastWeeklyWageDate;

        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        state.ClubName = playerTeam?.Name ?? "";
        state.SeasonLabel = $"{LeagueService.CurrentSeasonYear}/{LeagueService.CurrentSeasonYear + 1}";

        await _db.SaveChangesAsync();
    }

    public async Task<GameState?> ReadStateAsync() => await _db.GameStates.FirstOrDefaultAsync();
}

/// <summary>
/// File-level save/load: copies the live working database to/from a user-chosen
/// <c>.hbm</c> file. Uses SQLite's online backup API to snapshot a live connection
/// safely, and validates a candidate save before it is allowed to overwrite the
/// working database.
/// </summary>
public static class GameSaveFile
{
    public const string Extension = ".hbm";
    public const string DialogFilter = "Handball Manager Save (*.hbm)|*.hbm";

    /// <summary>Snapshots the live working database to <paramref name="targetPath"/> (called after GameState is flushed).</summary>
    public static void SaveTo(HandballDbContext db, string targetPath)
    {
        var source = (SqliteConnection)db.Database.GetDbConnection();
        if (source.State != ConnectionState.Open) source.Open();

        // Overwrite any existing target: BackupDatabase copies into an existing (empty) db.
        if (File.Exists(targetPath)) File.Delete(targetPath);

        using var dest = new SqliteConnection($"Data Source={targetPath}");
        dest.Open();
        source.BackupDatabase(dest);
    }

    /// <summary>
    /// Returns the embedded <see cref="GameState"/> from a candidate save file, or null
    /// if the file can't be opened as a valid save. Does not touch the working database.
    /// </summary>
    public static async Task<GameState?> PeekStateAsync(string path)
    {
        try
        {
            using var probe = new HandballDbContext(path);
            return await probe.GameStates.FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces the working database file with the chosen save. The caller MUST dispose
    /// its live context first; this clears the SQLite connection pool so the file handle
    /// is released before the copy.
    /// </summary>
    public static void CopyIntoWorkingDatabase(string savePath, string workingDbPath)
    {
        SqliteConnection.ClearAllPools();
        File.Copy(savePath, workingDbPath, overwrite: true);
    }
}
