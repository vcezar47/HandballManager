using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class LeagueTableViewModel : BaseViewModel
{
    private readonly LeagueService _leagueService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action? _onNavigateToLeagueHistory;

    [ObservableProperty]
    private List<LeagueEntry> _standings = [];

    public LeagueTableViewModel(LeagueService leagueService, Action<Team>? onTeamSelected = null, Action? onNavigateToLeagueHistory = null)
    {
        Title = "League Table";
        _leagueService = leagueService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueHistory = onNavigateToLeagueHistory;
    }

    public async Task InitializeAsync()
    {
        Standings = await _leagueService.GetStandingsAsync();
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
        _onNavigateToLeagueHistory?.Invoke();
    }
}
