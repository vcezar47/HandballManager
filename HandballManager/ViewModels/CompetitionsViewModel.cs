using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class CompetitionsViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
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
    
    [ObservableProperty]
    private string _competitionName = "Liga Florilor";
    
    [ObservableProperty]
    private bool _isRomanianLeague;

    [ObservableProperty]
    private bool _isHungarianLeague;

    [ObservableProperty]
    private bool _isFrenchLeague;
    
    [ObservableProperty]
    private string _leagueLogoPath = "/Assets/leaguelogo/ligaflorilor.png";

    [ObservableProperty]
    private string _cupLogoPath = "/Assets/leaguelogo/cuparomaniei.png";

    public CompetitionsViewModel(
        HandballDbContext db,
        LeagueService leagueService,
        CupService cupService,
        SupercupService supercupService,
        Action<Team>? onTeamSelected = null,
        Action? onNavigateToLeagueDetail = null,
        Action? onNavigateToCupDetail = null,
        Action? onNavigateToSupercupDetail = null)
    {
        Title = "Competitions";
        _db = db;
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
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        CompetitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        IsRomanianLeague = CompetitionName == "Liga Florilor";
        IsHungarianLeague = CompetitionName == "NB I";
        IsFrenchLeague = CompetitionName == "Ligue Butagaz Énergie";
        
        LeagueLogoPath = CompetitionName switch
        {
            "NB I" => "/Assets/leaguelogo/nbi.png",
            "Ligue Butagaz Énergie" => "/Assets/leaguelogo/lfhdivision1.png",
            _ => "/Assets/leaguelogo/ligaflorilor.png"
        };

        CupLogoPath = CompetitionName switch
        {
            "NB I" => "/Assets/leaguelogo/magyarkupa.png",
            "Ligue Butagaz Énergie" => "/Assets/leaguelogo/coupedefrance.png",
            _ => "/Assets/leaguelogo/cuparomaniei.png"
        };
        
        LeagueStandings = await _leagueService.GetStandingsAsync(CompetitionName);
        PlayerCupGroup = await _cupService.GetPlayerTeamGroupAsync();

        if (PlayerCupGroup != null)
        {
            CupGroupTitle = CompetitionName switch
            {
                "NB I" => $"Magyar Kupa — Group {PlayerCupGroup.GroupName}",
                "Ligue Butagaz Énergie" => $"Coupe de France — Group {PlayerCupGroup.GroupName}",
                _ => $"Cupa României — Group {PlayerCupGroup.GroupName}"
            };
        }
        else
        {
            CupGroupTitle = CompetitionName switch
            {
                "NB I" => "Magyar Kupa",
                "Ligue Butagaz Énergie" => "Coupe de France",
                _ => "Cupa României"
            };
        }

        if (IsRomanianLeague)
        {
            var knockout = await _supercupService.GetKnockoutFixturesAsync();
            if (knockout.Any(f => f.Round == "Final" || f.Round == "ThirdPlace"))
                SupercupFixtures = knockout.Where(f => f.Round == "Final" || f.Round == "ThirdPlace").ToList();
            else
                SupercupFixtures = knockout.Where(f => f.Round == "SemiFinal").ToList();
        }
        else
        {
            SupercupFixtures = [];
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
