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
    public string LogoPath { get; set; } = string.Empty;
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
        { "HCM Baia Mare", "CS Minaur Baia Mare" },
        { "Rulmentul Brașov", "CSM Corona Brașov" },
        { "Rapid București", "CS Rapid București" }
    };

    [ObservableProperty]
    private ObservableCollection<ChampionRecord> _champions = new();

    [ObservableProperty]
    private ObservableCollection<TeamTitleSummary> _medalTable = new();

    public LeagueHistoryViewModel(HandballDbContext db)
    {
        Title = "League History";
        _db = db;
    }

    public async Task InitializeAsync()
    {
        var records = await _db.ChampionRecords
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        var teams = await _db.Teams.ToListAsync();

        Champions = new ObservableCollection<ChampionRecord>(records);

        var summary = records
            .GroupBy(r => NameMapping.TryGetValue(r.TeamName, out var modern) ? modern : r.TeamName)
            .Select(g => 
            {
                var team = teams.FirstOrDefault(t => t.Name == g.Key);
                return new TeamTitleSummary 
                { 
                    TeamName = g.Key, 
                    Count = g.Count(),
                    LogoPath = team?.LogoPath ?? string.Empty
                };
            })
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.TeamName)
            .ToList();

        MedalTable = new ObservableCollection<TeamTitleSummary>(summary);
    }
}
