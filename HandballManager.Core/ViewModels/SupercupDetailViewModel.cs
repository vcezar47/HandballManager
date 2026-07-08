using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class SupercupDetailViewModel : BaseViewModel
{
    private readonly SupercupService _supercupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Func<Task>? _onNavigateToHistory;

    [ObservableProperty]
    private List<SupercupFixture> _knockoutFixtures = [];

    [ObservableProperty]
    private List<SupercupFixture> _semiFinals = [];

    [ObservableProperty]
    private SupercupFixture? _thirdPlaceMatch;

    [ObservableProperty]
    private SupercupFixture? _finalMatch;

    [ObservableProperty]
    private string _supercupKicker = "SUPERCUPA ROMÂNIEI";

    [ObservableProperty]
    private string _supercupPageTitle = "Supercupa României";

    [ObservableProperty]
    private string _headerLogoPath = "/Assets/leaguelogo/cuparomaniei.png";

    [ObservableProperty]
    private bool _showSemiFinalAndThirdPlace = true;

    /// <summary>League/competition key for this screen (e.g. Liga Florilor, Kvindeligaen).</summary>
    public string ActiveCompetition { get; private set; } = "Liga Florilor";

    public SupercupDetailViewModel(SupercupService supercupService, Action<Team>? onTeamSelected = null, Func<Task>? onNavigateToHistory = null)
    {
        Title = "Supercupa României";
        _supercupService = supercupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToHistory = onNavigateToHistory;
    }

    public async Task InitializeAsync(string competitionName)
    {
        ActiveCompetition = competitionName;

        if (competitionName == "Kvindeligaen")
        {
            Title = "Bambuni Supercup";
            SupercupKicker = "BAMBUNI SUPERCUP";
            SupercupPageTitle = "Bambuni Supercup";
            HeaderLogoPath = "/Assets/leaguelogo/bambunisupercup.png";
            ShowSemiFinalAndThirdPlace = false;

            var knockout = await _supercupService.GetKnockoutFixturesAsync("Kvindeligaen");
            SemiFinals = [];
            ThirdPlaceMatch = null;
            FinalMatch = knockout.FirstOrDefault(f => f.Round == "Final");
            KnockoutFixtures = knockout;
        }
        else
        {
            Title = "Supercupa României";
            SupercupKicker = "SUPERCUPA ROMÂNIEI";
            SupercupPageTitle = "Supercupa României";
            HeaderLogoPath = "/Assets/leaguelogo/cuparomaniei.png";
            ShowSemiFinalAndThirdPlace = true;

            var knockout = await _supercupService.GetKnockoutFixturesAsync("Liga Florilor");
            SemiFinals = knockout.Where(f => f.Round == "SemiFinal").ToList();
            ThirdPlaceMatch = knockout.FirstOrDefault(f => f.Round == "ThirdPlace");
            FinalMatch = knockout.FirstOrDefault(f => f.Round == "Final");
            KnockoutFixtures = knockout;
        }
    }

    [RelayCommand]
    private async Task ViewSupercupHistoryAsync()
    {
        if (_onNavigateToHistory != null)
            await _onNavigateToHistory();
    }
}
