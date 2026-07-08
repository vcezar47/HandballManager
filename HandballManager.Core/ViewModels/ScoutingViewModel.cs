using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace HandballManager.ViewModels;

public partial class ScoutingViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly GameClock _clock;
    private readonly ScoutingService _scouting;
    private readonly Action<Player> _onPlayerSelected;
    private readonly TransferService? _transferService;
    private readonly Action<int, string>? _onOpenTransferNegotiation;

    [ObservableProperty]
    private string _activeScoutingTitle = "No active scouting assignments";

    [ObservableProperty]
    private string _activeScoutingSubtitle = "Right-click opponent players and choose Scout Player (up to 5 at a time).";

    [ObservableProperty]
    private int _daysRemaining;

    [ObservableProperty]
    private ObservableCollection<TeamRosterPlayerRowViewModel> _shortlist = new();

    public ScoutingViewModel(HandballDbContext db, GameClock clock, ScoutingService scouting, Action<Player> onPlayerSelected,
        TransferService? transferService = null, Action<int, string>? onOpenTransferNegotiation = null)
    {
        Title = "Scouting";
        _db = db;
        _clock = clock;
        _scouting = scouting;
        _onPlayerSelected = onPlayerSelected;
        _transferService = transferService;
        _onOpenTransferNegotiation = onOpenTransferNegotiation;

        _scouting.StateChanged += async (_, __) => await RefreshAsync();
        _clock.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(GameClock.CurrentDate))
                await RefreshAsync();
        };
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await RefreshActiveAsync();
        await RefreshShortlistAsync();
    }

    private async Task RefreshActiveAsync()
    {
        var actives = _scouting.ActiveAssignments.ToList();
        if (actives.Count == 0)
        {
            ActiveScoutingTitle = "No active scouting assignments";
            ActiveScoutingSubtitle = "Right-click opponent players and choose Scout Player (up to 5 at a time).";
            DaysRemaining = 0;
            return;
        }

        // Show summary based on the assignment that completes soonest.
        var next = actives.OrderBy(a => a.CompletesOn).First();

        var player = await _db.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == next.PlayerId);

        var playerName = player?.Name ?? "Unknown player";
        var teamName = player?.Team?.Name ?? "Unknown team";

        DaysRemaining = next.DaysRemaining(_clock.CurrentDate);
        ActiveScoutingTitle = $"Active scouting: {actives.Count} player(s)";
        ActiveScoutingSubtitle = $"Next: {playerName} ({teamName}) • completes on {next.CompletesOn:ddd, MMM d, yyyy} • {DaysRemaining} day(s) remaining";
    }

    private async Task RefreshShortlistAsync()
    {
        var ids = _scouting.ShortlistPlayerIds.ToList();
        if (ids.Count == 0)
        {
            Shortlist = new ObservableCollection<TeamRosterPlayerRowViewModel>();
            return;
        }

        var players = await _db.Players
            .Include(p => p.Team)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        // Preserve shortlist order in the UI.
        var byId = players.ToDictionary(p => p.Id);
        var rows = new List<TeamRosterPlayerRowViewModel>();
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var p))
                rows.Add(new TeamRosterPlayerRowViewModel(p, isPlayerTeamRoster: false, _scouting, _transferService, _clock, _onOpenTransferNegotiation));
        }

        Shortlist = new ObservableCollection<TeamRosterPlayerRowViewModel>(rows);
    }

    [RelayCommand]
    private void ViewPlayer(TeamRosterPlayerRowViewModel? row)
    {
        if (row?.Player is null) return;
        _onPlayerSelected(row.Player);
    }

    [RelayCommand]
    private void RemoveFromShortlist(TeamRosterPlayerRowViewModel? row)
    {
        if (row is null) return;
        _scouting.RemoveFromShortlist(row.Id);
        row.NotifyStateChanged();
    }

    [RelayCommand]
    private void CancelScouting()
    {
        _scouting.CancelAllScouting();
    }
}

