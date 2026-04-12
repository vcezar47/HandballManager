using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandballManager.ViewModels;

public partial class SquadSelectionViewModel : BaseViewModel
{
    private readonly SquadSelection _squad;
    private readonly SquadSelection _aiSquad;
    private readonly Team _homeTeam;
    private readonly Team _awayTeam;
    private readonly bool _isUserHome;
    private readonly double _homeAdvantage;
    private readonly string _venueName;
    private readonly bool _isKnockout;
    private readonly Action<LiveMatchEngine, SquadSelection, SquadSelection> _onEnterArena;

    [ObservableProperty] private string _matchInfo = string.Empty;
    [ObservableProperty] private bool _isSquadValid;
    [ObservableProperty] private string _userTeamLogo = string.Empty;
    public ObservableCollection<SquadSlot> StartingLineupSlots { get; } = new();

    [ObservableProperty] private ObservableCollection<Player> _availablePlayers = new();

    // Fixed 9-slot substitutes bench: GK, LW, LB, CB, RB, RW, Pivot, Free1, Free2
    public ObservableCollection<SubSlot> SubstituteSlots { get; } = new();

    // Filters for right panel
    [ObservableProperty] private string _selectedPositionFilter = "All";
    public List<string> PositionFilters { get; } = new() { "All", "GK", "LW", "LB", "CB", "RB", "RW", "Pivot" };

    [ObservableProperty] private SquadSlot? _activeSlot;

    // The currently "pending" sub-slot that a player from the pool should be assigned to
    [ObservableProperty] private SubSlot? _activeSubSlot;

    public Team UserTeam => _isUserHome ? _homeTeam : _awayTeam;
    public Team OpponentTeam => _isUserHome ? _awayTeam : _homeTeam;

    public SquadSelectionViewModel(Team home, Team away, bool isUserHome, double homeAdvantage, string venueName, string matchInfo, bool isKnockout, Action<LiveMatchEngine, SquadSelection, SquadSelection> onEnterArena)
    {
        Title = "SQUAD SELECTION";
        _homeTeam = home;
        _awayTeam = away;
        _isUserHome = isUserHome;
        _homeAdvantage = homeAdvantage;
        _venueName = venueName;
        _matchInfo = matchInfo;
        _isKnockout = isKnockout;
        _onEnterArena = onEnterArena;

        _squad = new SquadSelection { TeamId = UserTeam.Id };
        _aiSquad = new SquadSelection { TeamId = OpponentTeam.Id };

        UserTeamLogo = UserTeam.LogoPath;

        InitializeSlots();
        InitializeSubSlots();
        LoadPlayers();
        ValidateSquad();
    }

    private void InitializeSlots()
    {
        string[] pos = ["GK", "LW", "LB", "CB", "RB", "RW", "Pivot"];
        foreach (var p in pos)
        {
            StartingLineupSlots.Add(new SquadSlot(p));
        }
    }

    private void InitializeSubSlots()
    {
        // 7 positional + 2 free-choice slots
        string[] positions = ["GK", "LW", "LB", "CB", "RB", "RW", "Pivot", "Free", "Free"];
        foreach (var pos in positions)
            SubstituteSlots.Add(new SubSlot(pos));
    }

    private void LoadPlayers()
    {
        AvailablePlayers.Clear();
        foreach (var p in UserTeam.Players.OrderByDescending(x => x.Overall100))
            AvailablePlayers.Add(p);
    }


    [RelayCommand]
    private void SelectSlot(SquadSlot slot)
    {
        // Deselect any sub slot
        if (ActiveSubSlot != null) { ActiveSubSlot.IsSelected = false; ActiveSubSlot = null; }

        if (ActiveSlot != null) ActiveSlot.IsSelected = false;
        ActiveSlot = slot;
        if (ActiveSlot != null) ActiveSlot.IsSelected = true;
        SelectedPositionFilter = slot.Position;
        UpdateAvailablePlayers();
    }

    /// <summary>
    /// Clicking a substitute slot:
    ///  - If it has a player → remove the player back to available
    ///  - If it's empty → mark as the target for next player-pool click
    /// </summary>
    [RelayCommand]
    private void SelectSubSlot(SubSlot subSlot)
    {
        if (subSlot.Player != null)
        {
            // Remove player back to pool
            subSlot.Player = null;
            if (ActiveSubSlot != null) { ActiveSubSlot.IsSelected = false; ActiveSubSlot = null; }
            UpdateAvailablePlayers();
            ValidateSquad();
        }
        else
        {
            // Deselect any starter slot
            if (ActiveSlot != null) { ActiveSlot.IsSelected = false; ActiveSlot = null; }

            if (ActiveSubSlot != null) ActiveSubSlot.IsSelected = false;
            ActiveSubSlot = subSlot;
            ActiveSubSlot.IsSelected = true;
            SelectedPositionFilter = subSlot.Position == "Free" ? "All" : subSlot.Position;
            UpdateAvailablePlayers();
        }
    }

    partial void OnSelectedPositionFilterChanged(string value) => UpdateAvailablePlayers();

    private void UpdateAvailablePlayers()
    {
        var filtered = UserTeam.Players.Where(p => !p.IsInjured).AsEnumerable();

        if (SelectedPositionFilter != "All")
            filtered = filtered.Where(p => p.Position == SelectedPositionFilter);

        var activeIds = StartingLineupSlots.Where(s => s.Player != null).Select(s => s.Player!.Id).ToHashSet();

        // Must exclude all players on the bench to prevent duplicates in the Available Players list.
        var subIds = SubstituteSlots.Where(s => s.Player != null).Select(s => s.Player!.Id).ToHashSet();

        filtered = filtered.Where(p => !activeIds.Contains(p.Id) && !subIds.Contains(p.Id))
                           .OrderByDescending(p => p.Overall100);

        AvailablePlayers = new ObservableCollection<Player>(filtered);
    }

    [RelayCommand]
    private void AssignPlayer(Player p)
    {
        if (ActiveSlot != null)
        {
            var existing = StartingLineupSlots.FirstOrDefault(s => s.Player?.Id == p.Id);
            if (existing != null) existing.Player = null;

            ActiveSlot.Player = p;
            _squad.StartingLineup[ActiveSlot.Position] = p;

            var freeSubSlot = SubstituteSlots.FirstOrDefault(s => s.Player?.Id == p.Id && s.Position == "Free");
            if (freeSubSlot != null) freeSubSlot.Player = null;

            ActiveSlot.IsSelected = false;
            ActiveSlot = null;
            
            UpdateAvailablePlayers();
            ValidateSquad();
        }
        else if (ActiveSubSlot != null)
        {
            ActiveSubSlot.Player = p;
            ActiveSubSlot.IsSelected = false;
            ActiveSubSlot = null;
            
            UpdateAvailablePlayers();
            ValidateSquad();
        }
    }

    [RelayCommand]
    private void ClearSlot(SquadSlot slot)
    {
        slot.Player = null;
        _squad.StartingLineup[slot.Position] = null;
        UpdateAvailablePlayers();
        ValidateSquad();
    }

    [RelayCommand]
    private void AutoPick()
    {
        _squad.AutoPickBestSquad(UserTeam.Players.ToList());

        foreach (var s in StartingLineupSlots) s.Player = _squad.StartingLineup[s.Position];

        var autoSubsQueue = _squad.Substitutes.Where(p => !SubstituteSlots.Any(s => s.Player?.Id == p.Id)).ToList();

        foreach (var slot in SubstituteSlots.Where(s => s.Position != "Free" && s.Player == null))
        {
            var match = autoSubsQueue.FirstOrDefault(p => p.Position == slot.Position);
            if (match != null)
            {
                slot.Player = match;
                autoSubsQueue.Remove(match);
            }
        }

        foreach (var slot in SubstituteSlots.Where(s => s.Player == null))
        {
            if (!autoSubsQueue.Any()) break;
            slot.Player = autoSubsQueue.First();
            autoSubsQueue.RemoveAt(0);
        }

        ActiveSlot = null;
        ActiveSubSlot = null;
        SelectedPositionFilter = "All";
        UpdateAvailablePlayers();
        ValidateSquad();
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var s in StartingLineupSlots) s.Player = null;
        foreach (var s in SubstituteSlots) s.Player = null;

        _squad.StartingLineup = new Dictionary<string, Player?> { { "GK", null }, { "LW", null }, { "LB", null }, { "CB", null }, { "RB", null }, { "RW", null }, { "Pivot", null } };

        ActiveSlot = null;
        ActiveSubSlot = null;
        SelectedPositionFilter = "All";
        UpdateAvailablePlayers();
        ValidateSquad();
    }

    private void ValidateSquad()
    {
        MaterializeSubstitutes();
        IsSquadValid = _squad.IsValid();
    }

    /// <summary>
    /// Materializes SubstituteSlots into the squad's Substitutes list before entering the arena.
    /// </summary>
    private void MaterializeSubstitutes()
    {
        _squad.Substitutes.Clear();
        foreach (var slot in SubstituteSlots.Where(s => s.Player != null))
            _squad.Substitutes.Add(slot.Player!);
    }

    [RelayCommand]
    private void EnterTheArena()
    {
        if (!IsSquadValid) return;

        MaterializeSubstitutes();
        _aiSquad.AutoPickBestSquad(OpponentTeam.Players.ToList());
        var homeSquad = _isUserHome ? _squad : _aiSquad;
        var awaySquad = _isUserHome ? _aiSquad : _squad;

        var engine = new LiveMatchEngine(_homeTeam, _awayTeam, homeSquad, awaySquad, _homeAdvantage, _venueName, _isKnockout);
        _onEnterArena?.Invoke(engine, homeSquad, awaySquad);
    }

    [RelayCommand]
    private void SimulateGame()
    {
        if (!IsSquadValid) return;

        MaterializeSubstitutes();
        _aiSquad.AutoPickBestSquad(OpponentTeam.Players.ToList());
        var homeSquad = _isUserHome ? _squad : _aiSquad;
        var awaySquad = _isUserHome ? _aiSquad : _squad;

        var engine = new LiveMatchEngine(_homeTeam, _awayTeam, homeSquad, awaySquad, _homeAdvantage, _venueName, _isKnockout);

        // Fast-forward to end
        while (!engine.IsFullTime)
        {
            if (engine.IsHalfTime) engine.StartSecondHalf();
            engine.Tick(1.0);
        }

        // Handle Extra time and shootout for knockout
        if (_isKnockout && engine.HomeScore == engine.AwayScore)
        {
            engine.StartExtraTime();
            while (!engine.IsFullTime)
            {
                if (engine.IsExtraTimeHalfTime) engine.StartExtraTimeSecondHalf();
                engine.Tick(1.0);
            }
            if (engine.HomeScore == engine.AwayScore)
                engine.ResolveShootout();
        }

        _onEnterArena?.Invoke(engine, homeSquad, awaySquad);
    }
}

public partial class SquadSlot : ObservableObject
{
    public string Position { get; }

    [ObservableProperty]
    private Player? _player;

    [ObservableProperty]
    private bool _isSelected;

    public SquadSlot(string position) => Position = position;
}

/// <summary>Fixed bench slot for substitute players.</summary>
public partial class SubSlot : ObservableObject
{
    /// <summary>Position label: GK / LW / LB / CB / RB / RW / Pivot / Free</summary>
    public string Position { get; }

    [ObservableProperty]
    private Player? _player;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsEmpty => Player == null;
    public string DisplayLabel => Position == "Free" ? "ANY" : Position;

    public SubSlot(string position) => Position = position;

    partial void OnPlayerChanged(Player? value) => OnPropertyChanged(nameof(IsEmpty));
}
