using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class TransferNegotiationViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly TransferService _transferService;
    private readonly GameClock _clock;
    private readonly Player _player;
    private readonly string _mode;
    private readonly Action _onDone;

    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private string _modeTitle = string.Empty;

    [ObservableProperty]
    private decimal _suggestedWage;

    [ObservableProperty]
    private string _proposedWageText = "0";

    [ObservableProperty]
    private int _contractYears = 3;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    public TransferNegotiationViewModel(HandballDbContext db, TransferService transferService, GameClock clock, Player player, string mode, Action onDone)
    {
        _db = db;
        _transferService = transferService;
        _clock = clock;
        _player = player;
        _mode = mode;
        _onDone = onDone;

        PlayerName = player.Name;
        ModeTitle = mode == "ApproachToSign" ? "Approach to sign (free transfer)" : mode == "MakeOffer" ? "Make offer (buyout)" : "Negotiate contract";
        SuggestedWage = TransferService.EstimateRequestedMonthlyWage(player);
        ProposedWageText = SuggestedWage.ToString("F0");
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!decimal.TryParse(ProposedWageText, out var proposedWage) || proposedWage < 0)
            proposedWage = SuggestedWage;

        int userTeamId = (await _db.Teams.FirstAsync(t => t.IsPlayerTeam)).Id;
        string transferType = _mode == "ApproachToSign" ? "FreeContract" : "Buyout";
        int years = Math.Clamp(ContractYears, 1, 5);

        var (executedNow, message) = await _transferService.AgreeTransferAsync(
            _player.Id,
            userTeamId,
            proposedWage,
            years,
            transferType,
            _clock.CurrentDate);

        StatusMessage = message;
        IsSuccess = true;
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsSuccess));
    }

    [RelayCommand]
    private void Cancel()
    {
        _onDone?.Invoke();
    }

    [RelayCommand]
    private void DoneAndClose()
    {
        _onDone?.Invoke();
    }

    public int[] ContractYearsOptions { get; } = { 1, 2, 3, 4, 5 };
}
