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
        { "CS Oltchim Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" },
        { "HCM Baia Mare", "Minaur Baia Mare" }
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


        Winners = new ObservableCollection<SupercupWinnerRecord>(records);

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
