using HandballManager.Data;
using HandballManager.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Mobile.Session;

/// <summary>Summary of a save slot shown on the slot-picker cards.</summary>
public sealed class SlotInfo
{
    public int Slot { get; init; }
    public bool Occupied { get; init; }
    public string ManagerName { get; init; } = string.Empty;
    public string ClubName { get; init; } = string.Empty;
    public string ClubLogo { get; init; } = string.Empty;
    public string CompetitionName { get; init; } = string.Empty;
    public DateTime CurrentDate { get; init; }
    public int SeasonYear { get; init; }

    public string SlotLabel => $"SLOT {Slot}";
    public string SeasonLabel => $"Season {SeasonYear}/{(SeasonYear + 1) % 100:D2}";
    public string DateLabel => CurrentDate.ToString("ddd, MMM d yyyy");
}

/// <summary>
/// Manages the three career save slots. Each slot is a separate SQLite database in app-data;
/// the "active" slot is the one <see cref="HandballDbContext"/> resolves to, so autosave and all
/// gameplay queries flow into it.
/// </summary>
public static class SaveSlots
{
    public const int Count = 3;
    private const string ActiveKey = "active_save_slot";

    public static string FileName(int slot) => $"career_slot{slot}.db";
    public static string PathFor(int slot) => Path.Combine(FileSystem.AppDataDirectory, FileName(slot));

    public static int ActiveSlot
    {
        get => Preferences.Get(ActiveKey, 1);
        private set => Preferences.Set(ActiveKey, value);
    }

    /// <summary>Points the working DbContext (and future pooled connections) at the given slot's file.</summary>
    public static void SetActive(int slot)
    {
        ActiveSlot = slot;
        SqliteConnection.ClearAllPools();
        HandballDbContext.DatabaseFileName = FileName(slot);
    }

    /// <summary>One-time move of a pre-slots career (handball.db) into slot 1, so upgraders keep it.</summary>
    public static void MigrateLegacyIfNeeded()
    {
        var legacy = Path.Combine(FileSystem.AppDataDirectory, "handball.db");
        if (!File.Exists(legacy)) return;
        if (Enumerable.Range(1, Count).Any(s => File.Exists(PathFor(s)))) return; // slots already in use
        try { File.Move(legacy, PathFor(1)); } catch { /* best effort — leave legacy in place */ }
    }

    public static async Task<SlotInfo> ReadAsync(int slot)
    {
        var path = PathFor(slot);
        if (!File.Exists(path)) return new SlotInfo { Slot = slot };

        try
        {
            using var db = new HandballDbContext(path);
            if (!await db.Database.CanConnectAsync()) return new SlotInfo { Slot = slot };

            var state = await new GameStateService(db).ReadStateAsync();
            var team = await db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            if (state == null || team == null) return new SlotInfo { Slot = slot };

            var manager = await db.Managers.FirstOrDefaultAsync(m => m.TeamId == team.Id);
            var mgrName = manager != null ? $"{manager.FirstName} {manager.LastName}".Trim() : "—";

            return new SlotInfo
            {
                Slot = slot,
                Occupied = true,
                ManagerName = string.IsNullOrWhiteSpace(mgrName) ? "—" : mgrName,
                ClubName = team.Name,
                ClubLogo = team.LogoPath,
                CompetitionName = team.CompetitionName,
                CurrentDate = state.CurrentDate,
                SeasonYear = state.CurrentSeasonYear,
            };
        }
        catch
        {
            return new SlotInfo { Slot = slot };
        }
    }

    public static async Task<List<SlotInfo>> ReadAllAsync()
    {
        var list = new List<SlotInfo>(Count);
        for (int s = 1; s <= Count; s++) list.Add(await ReadAsync(s));
        return list;
    }

    public static async Task<bool> AnyOccupiedAsync()
    {
        for (int s = 1; s <= Count; s++)
            if ((await ReadAsync(s)).Occupied) return true;
        return false;
    }

    public static void Delete(int slot)
    {
        GameSession.DisposeIfActive(slot);
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { var f = PathFor(slot) + suffix; if (File.Exists(f)) File.Delete(f); } catch { /* best effort */ }
        }
    }
}
