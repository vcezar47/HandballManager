using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public partial class CupHistoryViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;

    private static readonly Dictionary<string, string> NameMapping = new()
    {
        { "Chimistul Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" },
        { "Oltchim Râmnicu Vâlcea", "SCM Râmnicu Vâlcea" },
        { "Universitatea Bacău", "CSM Bacău" },
        { "Știința Bacău", "CSM Bacău" },
        { "Silcotub Zalău", "HC Zalău" },
        { "HCM Baia Mare", "CS Minaur Baia Mare" },
        { "Rulmentul Brașov", "CSM Corona Brașov" },
        { "Rapid CFR București", "CS Rapid București" },
        { "Progresul Târgu Mureș", "CSU Târgu Mureș" },
        { "Mureșul Târgu Mureș", "CSU Târgu Mureș" }
    };

    [ObservableProperty]
    private ObservableCollection<CupWinnerRecord> _winners = new();

    [ObservableProperty]
    private ObservableCollection<TeamTitleSummary> _medalTable = new();

    public CupHistoryViewModel(HandballDbContext db)
    {
        Title = "Cup History";
        _db = db;
    }

    public async Task InitializeAsync()
    {
        var records = await _db.CupWinnerRecords
            .OrderByDescending(c => c.Season)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var teams = await _db.Teams.ToListAsync();

        Winners = new ObservableCollection<CupWinnerRecord>(records);

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
