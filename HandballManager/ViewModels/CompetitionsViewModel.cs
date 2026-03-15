using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class CompetitionsViewModel : BaseViewModel
{
    private readonly LeagueService _leagueService;
    private readonly CupService _cupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action? _onNavigateToLeagueDetail;
    private readonly Action? _onNavigateToCupDetail;

    [ObservableProperty]
    private List<LeagueEntry> _leagueStandings = [];

    [ObservableProperty]
    private CupGroup? _playerCupGroup;

    [ObservableProperty]
    private string _cupGroupTitle = "Cupa României";

    public CompetitionsViewModel(
        LeagueService leagueService,
        CupService cupService,
        Action<Team>? onTeamSelected = null,
        Action? onNavigateToLeagueDetail = null,
        Action? onNavigateToCupDetail = null)
    {
        Title = "Competitions";
        _leagueService = leagueService;
        _cupService = cupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueDetail = onNavigateToLeagueDetail;
        _onNavigateToCupDetail = onNavigateToCupDetail;
    }

    public async Task InitializeAsync()
    {
        LeagueStandings = await _leagueService.GetStandingsAsync();
        PlayerCupGroup = await _cupService.GetPlayerTeamGroupAsync();

        if (PlayerCupGroup != null)
            CupGroupTitle = $"Cupa României — Group {PlayerCupGroup.GroupName}";
    }

    [RelayCommand]
    private void ViewTeam(LeagueEntry? entry)
    {
        var team = entry?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewCupTeam(CupGroupEntry? entry)
    {
        var team = entry?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewLeagueDetail() => _onNavigateToLeagueDetail?.Invoke();

    [RelayCommand]
    private void ViewCupDetail() => _onNavigateToCupDetail?.Invoke();
}
