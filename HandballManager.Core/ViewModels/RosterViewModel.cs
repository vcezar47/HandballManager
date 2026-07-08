using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public partial class RosterViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Action<Player>? _onPlayerSelected;
    private readonly Action<int>? _onOpenRenewContract;

    [ObservableProperty]
    private string _teamName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Player> _players = new();

    [ObservableProperty]
    private string _filterPosition = "All";

    public List<string> Positions { get; } = ["All", "GK", "LW", "RW", "LB", "RB", "CB", "Pivot"];

    private List<Player> _allPlayers = new();

    public RosterViewModel(HandballDbContext db, Action<Player>? onPlayerSelected = null, Action<int>? onOpenRenewContract = null)
    {
        _db = db;
        _onPlayerSelected = onPlayerSelected;
        _onOpenRenewContract = onOpenRenewContract;
    }

    [RelayCommand]
    private void RenewContract(Player? player)
    {
        if (player != null)
            _onOpenRenewContract?.Invoke(player.Id);
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
        LoadData();
    }

    private void LoadData()
    {
        var team = _db.Teams
            .Include(t => t.Players)
            .FirstOrDefault(t => t.IsPlayerTeam);

        if (team != null)
        {
            TeamName = team.Name;
            _allPlayers = team.Players.ToList();
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void SelectPlayer(Player? player)
    {
        if (player != null)
        {
            _onPlayerSelected?.Invoke(player);
        }
    }

    partial void OnFilterPositionChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = FilterPosition == "All"
            ? _allPlayers
            : _allPlayers.Where(p => p.Position == FilterPosition);

        Players = new ObservableCollection<Player>(filtered);
    }
}
