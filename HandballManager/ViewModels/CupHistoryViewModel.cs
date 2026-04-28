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
        { "HCM Baia Mare", "Minaur Baia Mare" },
        { "Rulmentul Brașov", "CSM Corona Brașov" },
        { "Rapid CFR București", "Rapid București" },
        { "Progresul Târgu Mureș", "CSU Târgu Mureș" },
        { "Mureșul Târgu Mureș", "CSU Târgu Mureș" },
        { "Vasas", "Vasas SC" },
        { "Ferencváros", "FTC-Rail Cargo Hungaria" },
        { "Debreceni VSC", "DVSC Schaeffler" },
        { "Győri ETO", "Győri Audi ETO KC" }
    };

    [ObservableProperty]
    private ObservableCollection<CupWinnerRecord> _winners = new();

    [ObservableProperty]
    private ObservableCollection<TeamTitleSummary> _medalTable = new();

    [ObservableProperty]
    private string _cupDisplayName = "Cupa României";

    public CupHistoryViewModel(HandballDbContext db)
    {
        Title = "Cup History";
        _db = db;
    }

    public async Task InitializeAsync(string? competitionName = null)
    {
        if (competitionName == null)
        {
            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        }

        CupDisplayName = competitionName == "NB I" ? "Magyar Kupa" : "Cupa României";
        Title = $"{CupDisplayName} History";

        var records = await _db.CupWinnerRecords
            .Where(c => c.CompetitionName == competitionName)
            .OrderByDescending(c => c.Season)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        Winners = new ObservableCollection<CupWinnerRecord>(records);

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
