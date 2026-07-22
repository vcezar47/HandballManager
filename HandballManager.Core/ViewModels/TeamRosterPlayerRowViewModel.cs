using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class TeamRosterPlayerRowViewModel : ObservableObject
{
    private readonly ScoutingService _scouting;
    private readonly bool _isPlayerTeamRoster;
    private readonly TransferService? _transferService;
    private readonly GameClock? _clock;
    private readonly Action<int, string>? _onOpenTransferNegotiation;

    public Player Player { get; }

    public int Id => Player.Id;
    public int ShirtNumber => Player.ShirtNumber;
    public string Name => Player.Name;
    public string TeamName => Player.Team?.Name ?? string.Empty;
    public string Nationality => Player.Nationality;
    public string Position => Player.Position;
    public int Age => Player.Age;

    public TeamRosterPlayerRowViewModel(Player player, bool isPlayerTeamRoster, ScoutingService scouting,
        TransferService? transferService = null, GameClock? clock = null, Action<int, string>? onOpenTransferNegotiation = null)
    {
        Player = player;
        _isPlayerTeamRoster = isPlayerTeamRoster;
        _scouting = scouting;
        _transferService = transferService;
        _clock = clock;
        _onOpenTransferNegotiation = onOpenTransferNegotiation;
    }

    public bool IsFullyKnown => _isPlayerTeamRoster || _scouting.IsScouted(Player.Id);

    public string OverallDisplay
        => IsFullyKnown ? Player.Overall100.ToString() : Estimation.Range(Player.Id, "Overall100", Player.Overall100, 10, 99, 12);

    /// <summary>
    /// Whether the scout/shortlist/offer row is shown at all. Gated on the player
    /// belonging to another club — not on <see cref="CanScout"/>, which goes false
    /// the moment scouting starts and used to take the offer button down with it.
    /// </summary>
    public bool ShowMarketActions => !_isPlayerTeamRoster;

    public bool CanScout => !_isPlayerTeamRoster && _scouting.CanStartScouting(Player.Id);

    /// <summary>Scouting already under way or finished — the button reads as done rather than disappearing.</summary>
    public string ScoutButtonText => _scouting.IsScouted(Player.Id) ? "SCOUTED" : CanScout ? "SCOUT" : "SCOUTING…";

    public bool IsShortlisted => _scouting.ShortlistPlayerIds.Contains(Player.Id);

    public string ShortlistButtonText => IsShortlisted ? "SHORTLISTED" : "SHORTLIST";

    public bool CanAddToShortlist => !_isPlayerTeamRoster && !IsShortlisted;

    public bool CanApproachToSign => !_isPlayerTeamRoster && _transferService != null && _clock != null &&
        _transferService.CanApproachToSign(Player, _clock.CurrentDate);

    public bool CanMakeOffer => !_isPlayerTeamRoster && _transferService != null && _clock != null &&
        _transferService.CanMakeOffer(Player, _clock.CurrentDate);

    [RelayCommand]
    private void Scout()
    {
        if (!CanScout) return;
        _scouting.TryStartScouting(Player.Id);
        NotifyStateChanged();
    }

    [RelayCommand]
    private void AddToShortlist()
    {
        if (_isPlayerTeamRoster) return;
        _scouting.AddToShortlist(Player.Id);
        NotifyStateChanged();
    }

    [RelayCommand]
    private void RemoveFromShortlist()
    {
        _scouting.RemoveFromShortlist(Player.Id);
        NotifyStateChanged();
    }

    [RelayCommand]
    private void ApproachToSign()
    {
        if (!CanApproachToSign) return;
        _onOpenTransferNegotiation?.Invoke(Player.Id, "ApproachToSign");
    }

    [RelayCommand]
    private void MakeOffer()
    {
        if (!CanMakeOffer) return;
        _onOpenTransferNegotiation?.Invoke(Player.Id, "MakeOffer");
    }

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsFullyKnown));
        OnPropertyChanged(nameof(OverallDisplay));
        OnPropertyChanged(nameof(CanScout));
        OnPropertyChanged(nameof(ScoutButtonText));
        OnPropertyChanged(nameof(IsShortlisted));
        OnPropertyChanged(nameof(ShortlistButtonText));
        OnPropertyChanged(nameof(CanAddToShortlist));
        OnPropertyChanged(nameof(CanApproachToSign));
        OnPropertyChanged(nameof(CanMakeOffer));
    }
}

