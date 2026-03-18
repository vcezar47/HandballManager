using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class CompetitionsViewModel : BaseViewModel
{
    private readonly LeagueService _leagueService;
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action? _onNavigateToLeagueDetail;
    private readonly Action? _onNavigateToCupDetail;
    private readonly Action? _onNavigateToSupercupDetail;

    [ObservableProperty]
    private List<LeagueEntry> _leagueStandings = [];

    [ObservableProperty]
    private CupGroup? _playerCupGroup;

    [ObservableProperty]
    private string _cupGroupTitle = "Cupa României";

    [ObservableProperty]
    private List<SupercupFixture> _supercupFixtures = [];

    public CompetitionsViewModel(
        LeagueService leagueService,
        CupService cupService,
        SupercupService supercupService,
        Action<Team>? onTeamSelected = null,
        Action? onNavigateToLeagueDetail = null,
        Action? onNavigateToCupDetail = null,
        Action? onNavigateToSupercupDetail = null)
    {
        Title = "Competitions";
        _leagueService = leagueService;
        _cupService = cupService;
        _supercupService = supercupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueDetail = onNavigateToLeagueDetail;
        _onNavigateToCupDetail = onNavigateToCupDetail;
        _onNavigateToSupercupDetail = onNavigateToSupercupDetail;
    }

    public async Task InitializeAsync()
    {
        LeagueStandings = await _leagueService.GetStandingsAsync();
        PlayerCupGroup = await _cupService.GetPlayerTeamGroupAsync();

        if (PlayerCupGroup != null)
            CupGroupTitle = $"Cupa României — Group {PlayerCupGroup.GroupName}";

        var knockout = await _supercupService.GetKnockoutFixturesAsync();
        // Determine whether to show finals or semi finals based on if semis are played
        if (knockout.Any(f => f.Round == "Final" || f.Round == "ThirdPlace"))
        {
            SupercupFixtures = knockout.Where(f => f.Round == "Final" || f.Round == "ThirdPlace").ToList();
        }
        else
        {
            SupercupFixtures = knockout.Where(f => f.Round == "SemiFinal").ToList();
        }
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

    [RelayCommand]
    private void ViewSupercupDetail() => _onNavigateToSupercupDetail?.Invoke();
}
