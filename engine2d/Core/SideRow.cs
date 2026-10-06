using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.Engine2D;

/// <summary>
/// One player in the side panels beside the court: who is on, who is on the bench, who is
/// serving two minutes, and how much each has left in her legs. Updated in place every second,
/// so the list does not flicker; rebuilt only when somebody changes places.
/// </summary>
public sealed partial class SideRow : ObservableObject
{
    public int Id { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public string Shirt { get; init; } = "#3355AA";
    public string NumberColour { get; init; } = "#FFFFFF";

    [ObservableProperty] private string _role = "";
    [ObservableProperty] private int _energy;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _onCourt;
    [ObservableProperty] private bool _available;
    [ObservableProperty] private bool _selected;

    /// <summary>Her live match rating, once she has been on the court.</summary>
    [ObservableProperty] private double _rating;
    [ObservableProperty] private bool _played;

    public bool HasStatus => Status.Length > 0;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    /// <summary>Energy as a 0–1 bar.</summary>
    public double EnergyShare => Energy / 100.0;

    /// <summary>The bar's colour: green while fresh, amber when tiring, red when spent.</summary>
    public string EnergyColour => Energy >= 70 ? "#4CAF50" : Energy >= 50 ? "#FFB300" : "#E53935";

    partial void OnEnergyChanged(int value)
    {
        OnPropertyChanged(nameof(EnergyShare));
        OnPropertyChanged(nameof(EnergyColour));
    }
}
