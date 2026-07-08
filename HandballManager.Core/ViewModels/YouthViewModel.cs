using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class YouthViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly GameClock _clock;
    private readonly YouthIntakeService _youthService;
    private readonly TransferService _transferService;
    private readonly Action<int>? _onOpenYouthSign;
    private readonly Action<int>? _onOpenYouthDetail;

    [ObservableProperty]
    private ObservableCollection<YouthIntakePlayer> _intake = new();

    [ObservableProperty]
    private bool _hasIntakeAvailable;

    [ObservableProperty]
    private string _intakeYearText = string.Empty;

    public YouthViewModel(HandballDbContext db, GameClock clock, YouthIntakeService youthService, TransferService transferService, Action<int>? onOpenYouthSign = null, Action<int>? onOpenYouthDetail = null)
    {
        _db = db;
        _clock = clock;
        _youthService = youthService;
        _transferService = transferService;
        _onOpenYouthSign = onOpenYouthSign;
        _onOpenYouthDetail = onOpenYouthDetail;
    }

    public async Task InitializeAsync()
    {
        var userTeamId = (await _db.Teams.FirstAsync(t => t.IsPlayerTeam)).Id;
        int year = _clock.CurrentDate.Year;
        var list = await _db.YouthIntakePlayers
            .Where(y => y.ClubId == userTeamId && y.IntakeYear == year)
            .OrderBy(y => y.LastName)
            .ToListAsync();
        Intake = new ObservableCollection<YouthIntakePlayer>(list);
        HasIntakeAvailable = list.Count > 0;
        IntakeYearText = list.Count > 0 ? $"Youth intake {year}" : "No youth intake this year. New intake on March 20.";
    }

    [RelayCommand]
    private void SignYouth(int youthId)
    {
        _onOpenYouthSign?.Invoke(youthId);
    }

    [RelayCommand]
    private void SelectYouth(YouthIntakePlayer youth)
    {
        _onOpenYouthDetail?.Invoke(youth.Id);
    }
}