using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class LeagueTableViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly LeagueService _leagueService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action<string>? _onNavigateToLeagueHistory;

    [ObservableProperty]
    private List<LeagueEntry> _standings = [];
    
    [ObservableProperty]
    private string _competitionName = "Liga Florilor";

    public LeagueTableViewModel(HandballDbContext db, LeagueService leagueService, Action<Team>? onTeamSelected = null, Action<string>? onNavigateToLeagueHistory = null)
    {
        Title = "League Table";
        _db = db;
        _leagueService = leagueService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueHistory = onNavigateToLeagueHistory;
    }

    public async Task InitializeAsync(string? competitionOverride = null)
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        CompetitionName = competitionOverride ?? playerTeam?.CompetitionName ?? "Liga Florilor";
        Standings = await _leagueService.GetStandingsAsync(CompetitionName);
    }

    [RelayCommand]
    private void ViewTeam(LeagueEntry? entry)
    {
        var team = entry?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewLeagueHistory()
    {
        _onNavigateToLeagueHistory?.Invoke(CompetitionName);
    }
}
