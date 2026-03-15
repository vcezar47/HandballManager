using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class CupDetailViewModel : BaseViewModel
{
    private readonly CupService _cupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action? _onNavigateToHistory;

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

    public CupDetailViewModel(CupService cupService, Action<Team>? onTeamSelected = null, Action? onNavigateToHistory = null)
    {
        Title = "Cupa României";
        _cupService = cupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToHistory = onNavigateToHistory;
    }

    public async Task InitializeAsync()
    {
        AllGroups = await _cupService.GetAllGroupsAsync();
        var knockout = await _cupService.GetKnockoutFixturesAsync();

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
    private void ViewCupHistory() => _onNavigateToHistory?.Invoke();
}
