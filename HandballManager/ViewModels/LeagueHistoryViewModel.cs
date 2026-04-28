using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public class TeamTitleSummary
{
    public string TeamName { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Rank { get; set; }
}

public partial class LeagueHistoryViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;

    private static readonly Dictionary<string, string> NameMapping = new()
    {
        { "Chimistul Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" },
        { "Oltchim Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" },
        { "Știința București", "Universitatea București" },
        { "Știința Timișoara", "Universitatea Timișoara" },
        { "Universitatea Știința Timișoara", "Universitatea Timișoara" },
        { "Silcotub Zalău", "HC Zalău" },
        { "HCM Baia Mare", "Minaur Baia Mare" },
        { "Rulmentul Brașov", "CSM Corona Brașov" },
        { "CS Rapid București", "Rapid București" }
    };

    [ObservableProperty]
    private ObservableCollection<ChampionRecord> _champions = new();

    [ObservableProperty]
    private ObservableCollection<TeamTitleSummary> _medalTable = new();

    [ObservableProperty]
    private string _competitionName = "Liga Florilor";
    
    [ObservableProperty]
    private bool _isRomanianLeague;

    public LeagueHistoryViewModel(HandballDbContext db)
    {
        Title = "League History";
        _db = db;
    }

    public async Task InitializeAsync(string? competitionOverride = null)
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        CompetitionName = competitionOverride ?? playerTeam?.CompetitionName ?? "Liga Florilor";
        IsRomanianLeague = CompetitionName == "Liga Florilor";

        var records = await _db.ChampionRecords
            .Where(c => c.CompetitionName == CompetitionName)
            .ToListAsync();

        // Sort by season descending (extracting the end year from "2024/2025")
        var sortedRecords = records
            .OrderByDescending(c => int.Parse(c.Season.Split('/')[1]))
            .ToList();

        var teams = await _db.Teams
            .Where(t => t.CompetitionName == CompetitionName)
            .ToListAsync();

        Champions = new ObservableCollection<ChampionRecord>(sortedRecords);

        var summary = records
            .GroupBy(r => NameMapping.TryGetValue(r.TeamName, out var modern) ? modern : r.TeamName)
            .Select(g => 
            {
                return new TeamTitleSummary 
                { 
                    TeamName = g.Key, 
                    Count = g.Count()
                };
            })
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.TeamName)
            .ToList();

        for (int i = 0; i < summary.Count; i++)
        {
            summary[i].Rank = i + 1;
        }

        MedalTable = new ObservableCollection<TeamTitleSummary>(summary);
    }
}
