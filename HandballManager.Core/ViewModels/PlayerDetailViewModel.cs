using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class PlayerDetailViewModel : BaseViewModel
{
    private readonly ScoutingService? _scouting;
    private readonly bool _isPlayerTeamPlayer;
    private readonly TransferService? _transferService;
    private readonly GameClock? _clock;
    private readonly Action<int, string>? _onOpenTransferNegotiation;

    [ObservableProperty]
    private Player _player;

    public bool IsFullyKnown => _isPlayerTeamPlayer || (_scouting?.IsScouted(Player.Id) ?? true);

    public MaskedPlayerProxy Masked { get; }

    public PlayerDetailViewModel(Player player, bool isPlayerTeamPlayer = true, ScoutingService? scouting = null,
        TransferService? transferService = null, GameClock? clock = null,
        Action<int, string>? onOpenTransferNegotiation = null)
    {
        _player = player;
        _isPlayerTeamPlayer = isPlayerTeamPlayer;
        _scouting = scouting;
        _transferService = transferService;
        _clock = clock;
        _onOpenTransferNegotiation = onOpenTransferNegotiation;
        Title = $"{player.Name} - Profile";

        Masked = new MaskedPlayerProxy(player, () => IsFullyKnown);

        if (_scouting != null)
        {
            _scouting.StateChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(IsFullyKnown));
                Masked.NotifyAllChanged();
                NotifyMarketStateChanged();
            };
        }
    }

    // ── Market actions, so a player can be scouted/shortlisted/bought without
    //    backing out to the squad list first. ──

    /// <summary>Whether the action bar shows at all — only for players at other clubs.</summary>
    public bool ShowMarketActions => !_isPlayerTeamPlayer && _scouting != null;

    public bool CanScout => !_isPlayerTeamPlayer && _scouting?.CanStartScouting(Player.Id) == true;

    public string ScoutButtonText => _scouting?.IsScouted(Player.Id) == true ? "SCOUTED" : CanScout ? "SCOUT" : "SCOUTING…";

    public bool IsShortlisted => _scouting?.ShortlistPlayerIds.Contains(Player.Id) == true;

    public string ShortlistButtonText => IsShortlisted ? "SHORTLISTED" : "SHORTLIST";

    public bool CanAddToShortlist => !_isPlayerTeamPlayer && _scouting != null && !IsShortlisted;

    public bool CanMakeOffer => !_isPlayerTeamPlayer && _transferService != null && _clock != null &&
        _transferService.CanMakeOffer(Player, _clock.CurrentDate);

    public bool CanApproachToSign => !_isPlayerTeamPlayer && _transferService != null && _clock != null &&
        _transferService.CanApproachToSign(Player, _clock.CurrentDate);

    /// <summary>Free agents are signed directly; contracted players need an offer to their club.</summary>
    public bool IsFreeAgent => Player.TeamId == null;

    public string OfferButtonText => IsFreeAgent ? "APPROACH TO SIGN" : "MAKE OFFER";

    public bool CanTransfer => IsFreeAgent ? CanApproachToSign : CanMakeOffer;

    [RelayCommand]
    private void Scout()
    {
        if (!CanScout) return;
        _scouting!.TryStartScouting(Player.Id);
        NotifyMarketStateChanged();
    }

    [RelayCommand]
    private void AddToShortlist()
    {
        if (!CanAddToShortlist) return;
        _scouting!.AddToShortlist(Player.Id);
        NotifyMarketStateChanged();
    }

    [RelayCommand]
    private void Transfer()
    {
        if (!CanTransfer) return;
        _onOpenTransferNegotiation?.Invoke(Player.Id, IsFreeAgent ? "ApproachToSign" : "MakeOffer");
    }

    private void NotifyMarketStateChanged()
    {
        OnPropertyChanged(nameof(CanScout));
        OnPropertyChanged(nameof(ScoutButtonText));
        OnPropertyChanged(nameof(IsShortlisted));
        OnPropertyChanged(nameof(ShortlistButtonText));
        OnPropertyChanged(nameof(CanAddToShortlist));
        OnPropertyChanged(nameof(CanMakeOffer));
        OnPropertyChanged(nameof(CanApproachToSign));
        OnPropertyChanged(nameof(CanTransfer));
    }
}
