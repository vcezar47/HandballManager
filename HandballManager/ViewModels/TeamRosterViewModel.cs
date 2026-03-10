using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public partial class TeamRosterViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Action<Player>? _onPlayerSelected;
    private readonly ScoutingService _scouting;

    private readonly int _teamId;
    private bool _isPlayerTeamRoster;
    private readonly TransferService? _transferService;
    private readonly GameClock? _clock;
    private readonly Action<int, string>? _onOpenTransferNegotiation;

    [ObservableProperty]
    private string _teamName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TeamRosterPlayerRowViewModel> _players = new();

    [ObservableProperty]
    private string _filterPosition = "All";

    public List<string> Positions { get; } = ["All", "GK", "LW", "RW", "LB", "RB", "CB", "Pivot"];

    private List<Player> _allPlayers = new();

    public TeamRosterViewModel(HandballDbContext db, int teamId, ScoutingService scouting, Action<Player>? onPlayerSelected = null,
        TransferService? transferService = null, GameClock? clock = null, Action<int, string>? onOpenTransferNegotiation = null)
    {
        Title = "Team Roster";
        _db = db;
        _teamId = teamId;
        _scouting = scouting;
        _onPlayerSelected = onPlayerSelected;
        _transferService = transferService;
        _clock = clock;
        _onOpenTransferNegotiation = onOpenTransferNegotiation;

        _scouting.StateChanged += (_, __) =>
        {
            foreach (var row in Players)
                row.NotifyStateChanged();
        };
    }

    public async Task InitializeAsync()
    {
        var team = await _db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == _teamId);

        if (team is null)
        {
            TeamName = "Unknown team";
            Players = new ObservableCollection<TeamRosterPlayerRowViewModel>();
            return;
        }

        TeamName = team.Name;
        _isPlayerTeamRoster = team.IsPlayerTeam;
        _allPlayers = team.Players.ToList();
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectPlayer(TeamRosterPlayerRowViewModel? player)
    {
        if (player?.Player != null)
            _onPlayerSelected?.Invoke(player.Player);
    }

    partial void OnFilterPositionChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = FilterPosition == "All"
            ? _allPlayers
            : _allPlayers.Where(p => p.Position == FilterPosition);

        Players = new ObservableCollection<TeamRosterPlayerRowViewModel>(
            filtered.Select(p => new TeamRosterPlayerRowViewModel(p, _isPlayerTeamRoster, _scouting, _transferService, _clock, _onOpenTransferNegotiation))
        );
    }
}

