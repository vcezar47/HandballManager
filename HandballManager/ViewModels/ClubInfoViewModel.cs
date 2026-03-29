using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public class TrophyViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public int Count { get; set; }
}

public partial class ClubInfoViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly ScoutingService _scouting;
    private readonly TransferService _transferService;
    private readonly GameClock _clock;
    private readonly Action<Player> _onPlayerSelected;
    private readonly Action<int, string> _onNegotiateTransfer;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private Team? _team;

    [ObservableProperty]
    private TeamRosterViewModel? _squadVM;

    [ObservableProperty]
    private Manager? _manager; // Added Manager property



    public ObservableCollection<TrophyViewModel> Trophies { get; } = new();

    // Modified constructor to include MainViewModel
    public ClubInfoViewModel(HandballDbContext db, ScoutingService scouting, TransferService transferService, GameClock clock, Action<Player> onPlayerSelected, Action<int, string> onNegotiateTransfer, MainViewModel mainVm)
    {
        _db = db;
        _scouting = scouting;
        _transferService = transferService;
        _clock = clock;
        _onPlayerSelected = onPlayerSelected;
        _onNegotiateTransfer = onNegotiateTransfer;
        _mainVm = mainVm; 
    }

    [RelayCommand]
    private void NavigateToManagerDetail()
    {
        if (Manager != null)
        {
            _mainVm.NavigateToManagerDetail(Manager);
        }
    }

    public async Task InitializeAsync(int teamId)
    {
        Team = await _db.Teams
            .Include(t => t.LeagueEntry)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (Team == null) return;

        // Load Manager
        await _db.Entry(Team).Reference(t => t.Manager).LoadAsync();
        Manager = Team.Manager;



        // Initialize Squad VM
        SquadVM = new TeamRosterViewModel(_db, teamId, _scouting, _onPlayerSelected, _transferService, _clock, _onNegotiateTransfer);
        await SquadVM.InitializeAsync();

        // Load Trophies
        await LoadTrophiesAsync();
    }

    private async Task LoadTrophiesAsync()
    {
        if (Team == null) return;

        Trophies.Clear();

        // 1. Liga Florilor Trophies
        int leagueTitles = await _db.ChampionRecords.CountAsync(r => r.TeamId == Team.Id || r.TeamName == Team.Name);
        if (leagueTitles > 0)
        {
            Trophies.Add(new TrophyViewModel
            {
                Name = "Liga Florilor",
                ImagePath = "pack://application:,,,/Assets/trophies/ligaflorilor.png",
                Count = leagueTitles
            });
        }

        // 2. Cupa României Trophies
        int cupTitles = await _db.CupWinnerRecords.CountAsync(r => r.TeamId == Team.Id || r.TeamName == Team.Name);
        if (cupTitles > 0)
        {
            Trophies.Add(new TrophyViewModel
            {
                Name = "Cupa României",
                ImagePath = "pack://application:,,,/Assets/trophies/placeholdertrophy.png",
                Count = cupTitles
            });
        }

        // 3. Supercupa României Trophies
        int supercupTitles = await _db.SupercupWinnerRecords.CountAsync(r => r.TeamId == Team.Id || r.TeamName == Team.Name);
        if (supercupTitles > 0)
        {
            Trophies.Add(new TrophyViewModel
            {
                Name = "Supercupa României",
                ImagePath = "pack://application:,,,/Assets/trophies/placeholdertrophy.png",
                Count = supercupTitles
            });
        }
    }
}
