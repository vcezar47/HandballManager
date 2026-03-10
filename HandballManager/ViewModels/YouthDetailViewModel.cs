using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;
using static HandballManager.Services.YouthIntakeService;

namespace HandballManager.ViewModels;

public partial class YouthDetailViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly int _youthId;
    private readonly Action _onSign;
    private readonly Action _onBack;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private string _nationality = "ROU";
    [ObservableProperty] private int _age;
    [ObservableProperty] private int _height;
    [ObservableProperty] private int _weight;

    // Technical
    [ObservableProperty] private int _dribbling;
    [ObservableProperty] private int _finishing;
    [ObservableProperty] private int _marking;
    [ObservableProperty] private int _passing;
    [ObservableProperty] private int _technique;
    [ObservableProperty] private int _receiving;
    [ObservableProperty] private int _longThrows;
    [ObservableProperty] private int _sevenMeterTaking;
    [ObservableProperty] private int _tackling;

    // Physical
    [ObservableProperty] private int _acceleration;
    [ObservableProperty] private int _pace;
    [ObservableProperty] private int _stamina;
    [ObservableProperty] private int _strength;
    [ObservableProperty] private int _agility;
    [ObservableProperty] private int _balance;
    [ObservableProperty] private int _jumpingReach;
    [ObservableProperty] private int _naturalFitness;

    // Mental
    [ObservableProperty] private int _aggression;
    [ObservableProperty] private int _anticipation;
    [ObservableProperty] private int _composure;
    [ObservableProperty] private int _concentration;
    [ObservableProperty] private int _decisions;
    [ObservableProperty] private int _determination;
    [ObservableProperty] private int _flair;
    [ObservableProperty] private int _leadership;
    [ObservableProperty] private int _offTheBall;
    [ObservableProperty] private int _teamwork;
    [ObservableProperty] private int _vision;
    [ObservableProperty] private int _positioning;

    // GK
    [ObservableProperty] private int _reflexes;
    [ObservableProperty] private int _handling;
    [ObservableProperty] private int _oneOnOnes;
    [ObservableProperty] private int _aerialReach;
    [ObservableProperty] private int _communication;
    [ObservableProperty] private int _eccentricity;
    [ObservableProperty] private int _throwing;

    [ObservableProperty] private bool _isGoalkeeper;
    [ObservableProperty] private int _overall;

    public YouthDetailViewModel(HandballDbContext db, int youthId, Action onSign, Action onBack)
    {
        _db = db;
        _youthId = youthId;
        _onSign = onSign;
        _onBack = onBack;
    }

    public async Task LoadAsync()
    {
        var youth = await _db.YouthIntakePlayers.FirstOrDefaultAsync(y => y.Id == _youthId);
        if (youth == null) return;

        Name = youth.Name;
        Position = youth.Position;
        Nationality = youth.Nationality;
        Age = DateTime.Today.Year - youth.Birthdate.Year;
        Height = youth.Height;
        Weight = youth.Weight;
        IsGoalkeeper = youth.Position == "GK";

        var s = JsonSerializer.Deserialize<PlayerSnapshot>(youth.PlayerDataJson);
        if (s == null) return;

        Dribbling = s.Dribbling; Finishing = s.Finishing; Marking = s.Marking;
        Passing = s.Passing; Technique = s.Technique; Receiving = s.Receiving;
        LongThrows = s.LongThrows; SevenMeterTaking = s.SevenMeterTaking; Tackling = s.Tackling;

        Acceleration = s.Acceleration; Pace = s.Pace; Stamina = s.Stamina;
        Strength = s.Strength; Agility = s.Agility; Balance = s.Balance;
        JumpingReach = s.JumpingReach; NaturalFitness = s.NaturalFitness;

        Aggression = s.Aggression; Anticipation = s.Anticipation; Composure = s.Composure;
        Concentration = s.Concentration; Decisions = s.Decisions; Determination = s.Determination;
        Flair = s.Flair; Leadership = s.Leadership; OffTheBall = s.OffTheBall;
        Teamwork = s.Teamwork; Vision = s.Vision; Positioning = s.Positioning;

        Reflexes = s.Reflexes; Handling = s.Handling; OneOnOnes = s.OneOnOnes;
        AerialReach = s.AerialReach; Communication = s.Communication;
        Eccentricity = s.Eccentricity; Throwing = s.Throwing;

        // Simple average overall
        var allAttrs = new[] {
            s.Dribbling, s.Finishing, s.Marking, s.Passing, s.Technique,
            s.Acceleration, s.Pace, s.Stamina, s.Strength, s.Agility,
            s.Anticipation, s.Decisions, s.Determination, s.Teamwork, s.Vision
        };
        Overall = (int)Math.Round(allAttrs.Average() * 5.0);
    }

    [RelayCommand]
    private void Sign() => _onSign();

    [RelayCommand]
    private void Back() => _onBack();
}