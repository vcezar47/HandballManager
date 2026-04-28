using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public class LeagueInfo
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
}

public partial class StartViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Action<Team> _onTeamSelected;

    [ObservableProperty]
    private int _currentStep = 0; // 0: Manager Creation, 1: League, 2: Team

    public ManagerCreationViewModel ManagerCreationVM { get; }

    [ObservableProperty]
    private List<LeagueInfo> _leagues = [];

    [ObservableProperty]
    private LeagueInfo? _selectedLeague;

    [ObservableProperty]
    private List<Team> _teams = [];

    [ObservableProperty]
    private Team? _selectedTeam;

    private Manager? _playerManager;

    public StartViewModel(HandballDbContext db, Action<Team> onTeamSelected)
    {
        Title = "Manager Creation";
        _db = db;
        _onTeamSelected = onTeamSelected;

        ManagerCreationVM = new ManagerCreationViewModel(db, manager =>
        {
            _playerManager = manager;
            CurrentStep = 1;
            Title = "League Selection";
        });

        Leagues = new List<LeagueInfo>
        {
            new() { Name = "Liga Florilor", Country = "Romania", Logo = "/Assets/leaguelogo/ligaflorilor.png" },
            new() { Name = "NB I", Country = "Hungary", Logo = "/Assets/leaguelogo/nbi.png" }
        };
        SelectedLeague = Leagues.First();
    }

    public async Task InitializeAsync()
    {
        await LoadTeamsForSelectedLeagueAsync();
    }

    [RelayCommand]
    private void MoveToNextLeague()
    {
        if (Leagues == null || Leagues.Count == 0) return;
        int index = Leagues.IndexOf(SelectedLeague!);
        if (index < Leagues.Count - 1)
        {
            SelectedLeague = Leagues[index + 1];
        }
    }

    [RelayCommand]
    private void MoveToPreviousLeague()
    {
        if (Leagues == null || Leagues.Count == 0) return;
        int index = Leagues.IndexOf(SelectedLeague!);
        if (index > 0)
        {
            SelectedLeague = Leagues[index - 1];
        }
    }

    private async Task LoadTeamsForSelectedLeagueAsync()
    {
        if (SelectedLeague == null) return;
        
        Teams = await _db.Teams
            .Where(t => t.CompetitionName == SelectedLeague.Name)
            .ToListAsync();
        SelectedTeam = Teams.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SelectLeague()
    {
        if (SelectedLeague != null)
        {
            await LoadTeamsForSelectedLeagueAsync();
            CurrentStep = 2;
            Title = "Select Your Team";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            Title = CurrentStep switch
            {
                0 => "Manager Creation",
                1 => "League Selection",
                _ => "Select Your Team"
            };
        }
    }

    [RelayCommand]
    private void StartCareer()
    {
        if (SelectedTeam != null && _playerManager != null)
        {
            // Mark the selected team as the player's team
            foreach (var t in _db.Teams) t.IsPlayerTeam = false;
            SelectedTeam.IsPlayerTeam = true;

            // Remove existing manager if any
            var existingManagers = _db.Managers.Where(m => m.TeamId == SelectedTeam.Id).ToList();
            _db.Managers.RemoveRange(existingManagers);

            // Assign player manager
            _playerManager.TeamId = SelectedTeam.Id;

            _db.SaveChanges();

            _onTeamSelected(SelectedTeam);
        }
    }
}
