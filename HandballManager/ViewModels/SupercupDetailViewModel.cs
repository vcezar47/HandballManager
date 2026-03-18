using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class SupercupDetailViewModel : BaseViewModel
{
    private readonly SupercupService _supercupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action? _onNavigateToHistory;

    [ObservableProperty]
    private List<SupercupFixture> _knockoutFixtures = [];

    [ObservableProperty]
    private List<SupercupFixture> _semiFinals = [];

    [ObservableProperty]
    private SupercupFixture? _thirdPlaceMatch;

    [ObservableProperty]
    private SupercupFixture? _finalMatch;

    public SupercupDetailViewModel(SupercupService supercupService, Action<Team>? onTeamSelected = null, Action? onNavigateToHistory = null)
    {
        Title = "Supercupa României";
        _supercupService = supercupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToHistory = onNavigateToHistory;
    }

    public async Task InitializeAsync()
    {
        var knockout = await _supercupService.GetKnockoutFixturesAsync();

        SemiFinals = knockout.Where(f => f.Round == "SemiFinal").ToList();
        ThirdPlaceMatch = knockout.FirstOrDefault(f => f.Round == "ThirdPlace");
        FinalMatch = knockout.FirstOrDefault(f => f.Round == "Final");
        KnockoutFixtures = knockout;
    }

    [RelayCommand]
    private void ViewSupercupHistory() => _onNavigateToHistory?.Invoke();
}
