using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.Models;

public partial class PlayerDot : ObservableObject
{
    [ObservableProperty]
    private int _playerId;

    [ObservableProperty]
    private int _shirtNumber;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _hasBall;

    [ObservableProperty]
    private bool _isHomeTeam;
}
