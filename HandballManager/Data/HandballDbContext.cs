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
    public DbSet<MatchRecord> MatchRecords { get; set; }
    public DbSet<MatchEvent> MatchEvents { get; set; }
    public DbSet<MatchPlayerStat> MatchPlayerStats { get; set; }
    public DbSet<ChampionRecord> ChampionRecords { get; set; }
    public DbSet<TransferOffer> TransferOffers { get; set; }
    public DbSet<PendingTransfer> PendingTransfers { get; set; }
    public DbSet<NewsItem> NewsItems { get; set; }
    public DbSet<YouthIntakePlayer> YouthIntakePlayers { get; set; }

    private readonly string _dbPath;

    public string DbPath => _dbPath;

    public HandballDbContext()
    {
        // Move database from C: drive to the project directory on E: drive
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handball.db");
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
    }
}