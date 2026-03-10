using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class YouthSignViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly YouthIntakeService _youthService;
    private readonly GameClock _clock;
    private readonly int _youthId;
    private readonly Action _onDone;

    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private string _position = string.Empty;

    [ObservableProperty]
    private string _suggestedWageText = "500";

    [ObservableProperty]
    private string _proposedWageText = "500";

    [ObservableProperty]
    private int _contractYears = 3;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    public YouthSignViewModel(HandballDbContext db, YouthIntakeService youthService, GameClock clock, int youthId, Action onDone)
    {
        _db = db;
        _youthService = youthService;
        _clock = clock;
        _youthId = youthId;
        _onDone = onDone;
        ContractYearsOptions = new[] { 1, 2, 3, 4, 5 };
    }

    public async Task LoadAsync()
    {
        var youth = await _db.YouthIntakePlayers.FirstOrDefaultAsync(y => y.Id == _youthId);
        if (youth == null) return;
        PlayerName = youth.Name;
        Position = youth.Position;
        SuggestedWageText = "500";
        ProposedWageText = "500";
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var userTeamId = (await _db.Teams.FirstAsync(t => t.IsPlayerTeam)).Id;
        if (!decimal.TryParse(ProposedWageText, out var wage) || wage < 0) wage = 500;
        int years = Math.Clamp(ContractYears, 1, 5);
        var player = await _youthService.SignYouthAsync(_youthId, userTeamId, wage, years, _clock.CurrentDate);
        if (player != null)
        {
            StatusMessage = $"{player.Name} signed to the first team.";
            IsSuccess = true;
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(IsSuccess));
            // Small delay so the success message is visible, then close
            await Task.Delay(900);
            _onDone?.Invoke();
        }
        else
        {
            StatusMessage = "Could not complete signing.";
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(IsSuccess));
        }
    }

    [RelayCommand]
    private void Close() => _onDone?.Invoke();

    public int[] ContractYearsOptions { get; }
}