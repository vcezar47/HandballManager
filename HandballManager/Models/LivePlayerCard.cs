using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.Models;

public partial class LivePlayerCard : ObservableObject
{
    [ObservableProperty]
    private int _playerId;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private int _shirtNumber;
    
    [ObservableProperty]
    private double _liveRating;
    
    [ObservableProperty]
    private double _energyPercent;
    
    [ObservableProperty]
    private string _position = string.Empty;
    
    public bool IsLowEnergy => EnergyPercent < 30.0;
    public bool IsCriticalEnergy => EnergyPercent < 15.0;

    partial void OnEnergyPercentChanged(double value)
    {
        OnPropertyChanged(nameof(IsLowEnergy));
        OnPropertyChanged(nameof(IsCriticalEnergy));
    }
}
