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
    private static readonly Dictionary<string, string[]> HistoricalNames = new()
    {
        { "SCM Râmnicu Vâlcea", new[] { "Chimistul Râmnicu Vâlcea", "Oltchim Râmnicu Vâlcea", "CS Oltchim Râmnicu Vâlcea" } },
        { "Universitatea București", new[] { "Știința București" } },
        { "Universitatea Timișoara", new[] { "Știința Timișoara", "Universitatea Știința Timișoara" } },
        { "HC Zalău", new[] { "Silcotub Zalău" } },
        { "Minaur Baia Mare", new[] { "HCM Baia Mare" } },
        { "CSM Corona Brașov", new[] { "Rulmentul Brașov" } },
        { "Rapid București", new[] { "CS Rapid București", "Rapid CFR București" } },
        { "CSM Bacău", new[] { "Universitatea Bacău", "Știința Bacău" } },
        { "CSU Târgu Mureș", new[] { "Progresul Târgu Mureș", "Mureșul Târgu Mureș" } }
    };

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

        // Use team name as the canonical key — historical records were seeded with the exact name
        // from champions.json and the competition is always tagged. Count by name within competition
        // to avoid double-counting (TeamId OR TeamName could hit the same row twice).

        // 1. League titles (Liga Florilor or NB I depending on the team's competition)
        string leagueComp = Team.CompetitionName ?? "Liga Florilor";
        string leagueLabel = leagueComp == "NB I" ? "NB I" : "Liga Florilor";
        string leagueTrophyImg = leagueComp == "NB I"
            ? "pack://application:,,,/Assets/trophies/placeholdertrophy.png"
            : "pack://application:,,,/Assets/trophies/ligaflorilor.png";

        var teamNamesToMatch = new List<string> { Team.Name };
        if (HistoricalNames.TryGetValue(Team.Name, out var pastNames))
        {
            teamNamesToMatch.AddRange(pastNames);
        }

        int leagueTitles = await _db.ChampionRecords
            .CountAsync(r => r.CompetitionName == leagueComp && teamNamesToMatch.Contains(r.TeamName));
        if (leagueTitles > 0)
        {
            Trophies.Add(new TrophyViewModel
            {
                Name = leagueLabel,
                ImagePath = leagueTrophyImg,
                Count = leagueTitles
            });
        }

        // 2. Cupa României Trophies (Romanian teams only)
        if (leagueComp == "Liga Florilor")
        {
            int cupTitles = await _db.CupWinnerRecords
                .CountAsync(r => teamNamesToMatch.Contains(r.TeamName));
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
            int supercupTitles = await _db.SupercupWinnerRecords
                .CountAsync(r => teamNamesToMatch.Contains(r.TeamName));
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
}
