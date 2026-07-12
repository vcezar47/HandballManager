using System;
using System.IO;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Data;

public class HandballDbContext : DbContext
{
    public DbSet<Player> Players { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<LeagueEntry> LeagueEntries { get; set; }
    public DbSet<LeagueFixture> LeagueFixtures { get; set; }
    public DbSet<MatchRecord> MatchRecords { get; set; }
    public DbSet<MatchEvent> MatchEvents { get; set; }
    public DbSet<MatchPlayerStat> MatchPlayerStats { get; set; }
    public DbSet<ChampionRecord> ChampionRecords { get; set; }
    public DbSet<TransferOffer> TransferOffers { get; set; }
    public DbSet<PendingTransfer> PendingTransfers { get; set; }
    public DbSet<NewsItem> NewsItems { get; set; }
    public DbSet<YouthIntakePlayer> YouthIntakePlayers { get; set; }
    public DbSet<CupGroup> CupGroups { get; set; }
    public DbSet<CupGroupEntry> CupGroupEntries { get; set; }
    public DbSet<CupFixture> CupFixtures { get; set; }
    public DbSet<CupWinnerRecord> CupWinnerRecords { get; set; }
    public DbSet<SupercupFixture> SupercupFixtures { get; set; }
    public DbSet<SupercupWinnerRecord> SupercupWinnerRecords { get; set; }
    public DbSet<Manager> Managers { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<GameState> GameStates { get; set; }

    private readonly string _dbPath;

    public string DbPath => _dbPath;

    /// <summary>
    /// Directory holding the working database. Desktop keeps the historical default
    /// (next to the executable); a mobile host must point this at its writable
    /// app-data directory before the first context is created.
    /// </summary>
    public static string DatabaseDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// Filename of the working database inside <see cref="DatabaseDirectory"/>. Desktop keeps the
    /// historical single file; a mobile host swaps this per save slot (e.g. "career_slot2.db") so
    /// every <c>new HandballDbContext()</c> resolves to the active career.
    /// </summary>
    public static string DatabaseFileName { get; set; } = "handball.db";

    /// <summary>Default working database (active slot on mobile).</summary>
    public HandballDbContext()
    {
        _dbPath = Path.Combine(DatabaseDirectory, DatabaseFileName);
    }

    /// <summary>Opens a context against an arbitrary database file (used to validate save files before loading).</summary>
    public HandballDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>()
            .Ignore(t => t.Budget);

        modelBuilder.Entity<LeagueEntry>()
            .Ignore(e => e.Points)
            .Ignore(e => e.GoalDifference)
            .Ignore(e => e.Rank);  // computed in-memory after sorting, not stored

        modelBuilder.Entity<Player>()
            .Ignore(p => p.Name)
            .Ignore(p => p.Overall)
            .Ignore(p => p.Overall100)
            .Ignore(p => p.BuyoutFee)
            .Ignore(p => p.AverageRating)
            .Ignore(p => p.Age)
            .Ignore(p => p.GrowthAccumulators) // serialised via GrowthAccumulatorsJson
            .Ignore(p => p.SeasonAttributeChanges) // serialised via SeasonAttributeChangesJson
            .HasQueryFilter(p => !p.IsRetired);

        modelBuilder.Entity<YouthIntakePlayer>()
            .Ignore(y => y.Name);

        modelBuilder.Entity<CupGroupEntry>()
            .Ignore(e => e.Points)
            .Ignore(e => e.GoalDifference)
            .Ignore(e => e.Rank);

        modelBuilder.Entity<Manager>()
            .Ignore(m => m.Name)
            .Ignore(m => m.Age)
            .Ignore(m => m.ClubHistory);

        modelBuilder.Entity<Team>()
            .HasOne(t => t.Manager)
            .WithOne(m => m.Team)
            .HasForeignKey<Manager>(m => m.TeamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}