using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public partial class SupercupHistoryViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;

    private static readonly Dictionary<string, string> NameMapping = new()
    {
        { "CS Oltchim Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" }
    };

    [ObservableProperty]
    private ObservableCollection<SupercupWinnerRecord> _winners = new();

    [ObservableProperty]
    private ObservableCollection<TeamTitleSummary> _medalTable = new();

    public SupercupHistoryViewModel(HandballDbContext db)
    {
        Title = "Supercup History";
        _db = db;
    }

    public async Task InitializeAsync()
    {
        var records = await _db.SupercupWinnerRecords
            .OrderByDescending(c => c.Season)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var teams = await _db.Teams.ToListAsync();

        Winners = new ObservableCollection<SupercupWinnerRecord>(records);

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
