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

    // Snapshots, not entities — see TableRow for why binding to the entities directly
    // left this tab showing a mix of current and several-matchdays-old rows.
    [ObservableProperty]
    private List<TableRow> _leagueStandings = [];

    [ObservableProperty]
    private List<TableRow> _playerCupStandings = [];

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
    private bool _isDanishLeague;
    
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

    /// <summary>
    /// Guards the shared <see cref="HandballDbContext"/>. Two screens reload this same
    /// view model (the Comps tab and the league table page it opens), and a DbContext
    /// throws if a second query starts while the first is still running.
    /// </summary>
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    public async Task InitializeAsync()
    {
        await _reloadGate.WaitAsync();
        try
        {
            await LoadAsync();
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task LoadAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        CompetitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        IsRomanianLeague = CompetitionName == "Liga Florilor";
        IsHungarianLeague = CompetitionName == "NB I";
        IsFrenchLeague = CompetitionName == "Ligue Butagaz Énergie";
        IsDanishLeague = CompetitionName == "Kvindeligaen";
        
        LeagueLogoPath = CompetitionName switch
        {
            "NB I" => "/Assets/leaguelogo/nbi.png",
            "Ligue Butagaz Énergie" => "/Assets/leaguelogo/lfhdivision1.png",
            "Kvindeligaen" => "/Assets/leaguelogo/kvindeligaen.png",
            _ => "/Assets/leaguelogo/ligaflorilor.png"
        };

        CupLogoPath = CompetitionName switch
        {
            "NB I" => "/Assets/leaguelogo/magyarkupa.png",
            "Ligue Butagaz Énergie" => "/Assets/leaguelogo/coupedefrance.png",
            "Kvindeligaen" => "/Assets/leaguelogo/santandercup.png",
            _ => "/Assets/leaguelogo/cuparomaniei.png"
        };
        
        var entries = IsDanishLeague
            ? await _leagueService.GetKvindeligaenComputedRegularStandingsAsync()
            : await _leagueService.GetStandingsAsync(CompetitionName);
        LeagueStandings = TableRow.FromLeague(entries);

        PlayerCupGroup = await _cupService.GetPlayerTeamGroupAsync();
        PlayerCupStandings = PlayerCupGroup == null ? [] : TableRow.FromCupGroup(PlayerCupGroup.Entries);

        if (PlayerCupGroup != null)
        {
            CupGroupTitle = CompetitionName switch
            {
                "NB I" => $"Magyar Kupa — Group {PlayerCupGroup.GroupName}",
                "Ligue Butagaz Énergie" => $"Coupe de France — Group {PlayerCupGroup.GroupName}",
                "Kvindeligaen" => $"Landspokalturnering — Group {PlayerCupGroup.GroupName}",
                _ => $"Cupa României — Group {PlayerCupGroup.GroupName}"
            };
        }
        else
        {
            CupGroupTitle = CompetitionName switch
            {
                "NB I" => "Magyar Kupa",
                "Ligue Butagaz Énergie" => "Coupe de France",
                "Kvindeligaen" => "Landspokalturnering",
                _ => "Cupa României"
            };
        }

        if (IsRomanianLeague)
        {
            var knockout = await _supercupService.GetKnockoutFixturesAsync("Liga Florilor");
            if (knockout.Any(f => f.Round == "Final" || f.Round == "ThirdPlace"))
                SupercupFixtures = knockout.Where(f => f.Round == "Final" || f.Round == "ThirdPlace").ToList();
            else
                SupercupFixtures = knockout.Where(f => f.Round == "SemiFinal").ToList();
        }
        else if (IsDanishLeague)
        {
            var dkKo = await _supercupService.GetKnockoutFixturesAsync("Kvindeligaen");
            SupercupFixtures = dkKo.Where(f => f.Round == "Final").ToList();
        }
        else
        {
            SupercupFixtures = [];
        }
    }

    [RelayCommand]
    private void ViewTeam(TableRow? row)
    {
        var team = row?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewCupTeam(TableRow? row)
    {
        var team = row?.Team;
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
