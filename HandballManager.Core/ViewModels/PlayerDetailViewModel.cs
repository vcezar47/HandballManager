using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class PlayerDetailViewModel : BaseViewModel
{
    private readonly ScoutingService? _scouting;
    private readonly bool _isPlayerTeamPlayer;

    [ObservableProperty]
    private Player _player;

    public bool IsFullyKnown => _isPlayerTeamPlayer || (_scouting?.IsScouted(Player.Id) ?? true);

    public MaskedPlayerProxy Masked { get; }

    public PlayerDetailViewModel(Player player, bool isPlayerTeamPlayer = true, ScoutingService? scouting = null)
    {
        _player = player;
        _isPlayerTeamPlayer = isPlayerTeamPlayer;
        _scouting = scouting;
        Title = $"{player.Name} - Profile";

        Masked = new MaskedPlayerProxy(player, () => IsFullyKnown);

        if (_scouting != null)
        {
            _scouting.StateChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(IsFullyKnown));
                Masked.NotifyAllChanged();
            };
        }
    }

    // Category groupings for easier binding if needed, 
    // but the XAML can just bind directly to the properties.
}
