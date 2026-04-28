using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class CupDetailViewModel : BaseViewModel
{
    private readonly CupService _cupService;
    private readonly HandballDbContext _db;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action<string>? _onNavigateToHistory;

    [ObservableProperty]
    private string _selectedTab = "GroupPhase";

    [ObservableProperty]
    private List<CupGroup> _allGroups = [];

    [ObservableProperty]
    private List<CupFixture> _knockoutFixtures = [];

    [ObservableProperty]
    private List<CupFixture> _quarterFinals = [];

    [ObservableProperty]
    private List<CupFixture> _semiFinals = [];

    [ObservableProperty]
    private CupFixture? _thirdPlaceMatch;

    [ObservableProperty]
    private CupFixture? _finalMatch;

    [ObservableProperty]
    private string _cupDisplayName = "Cupa României";

    [ObservableProperty]
    private string _cupLogoPath = "/Assets/leaguelogo/cuparomaniei.png";

    [ObservableProperty]
    private bool _isHungarianCup;

    [ObservableProperty]
    private string _competitionName = "Liga Florilor";

    public CupDetailViewModel(CupService cupService, HandballDbContext db, Action<Team>? onTeamSelected = null, Action<string>? onNavigateToHistory = null)
    {
        Title = "Cup Detail";
        _cupService = cupService;
        _db = db;
        _onTeamSelected = onTeamSelected;
        _onNavigateToHistory = onNavigateToHistory;
    }

    public async Task InitializeAsync(string? comp = null)
    {
        if (comp != null)
            CompetitionName = comp;
        else
        {
            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            CompetitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        }

        IsHungarianCup = CompetitionName == "NB I";
        CupDisplayName = IsHungarianCup ? "Magyar Kupa" : "Cupa României";
        CupLogoPath = IsHungarianCup ? "/Assets/leaguelogo/magyarkupa.png" : "/Assets/leaguelogo/cuparomaniei.png";
        Title = CupDisplayName;

        AllGroups = await _cupService.GetAllGroupsAsync(CompetitionName);
        var knockout = await _cupService.GetKnockoutFixturesAsync(CompetitionName);

        QuarterFinals = knockout.Where(f => f.Round == "QuarterFinal").ToList();
        SemiFinals = knockout.Where(f => f.Round == "SemiFinal").ToList();
        ThirdPlaceMatch = knockout.FirstOrDefault(f => f.Round == "ThirdPlace");
        FinalMatch = knockout.FirstOrDefault(f => f.Round == "Final");
        KnockoutFixtures = knockout;
    }

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (tab != null)
            SelectedTab = tab;
    }

    [RelayCommand]
    private void ViewTeam(CupGroupEntry? entry)
    {
        var team = entry?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewCupHistory() => _onNavigateToHistory?.Invoke(CompetitionName);
}
