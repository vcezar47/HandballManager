using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class ContractRenewalViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly TransferService _transferService;
    private readonly GameClock _clock;
    private readonly int _playerId;
    private readonly Action _onDone;

    [ObservableProperty]
    private string _playerName = string.Empty;

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

    public ContractRenewalViewModel(HandballDbContext db, TransferService transferService, GameClock clock, int playerId, Action onDone)
    {
        _db = db;
        _transferService = transferService;
        _clock = clock;
        _playerId = playerId;
        _onDone = onDone;
        ContractYearsOptions = new[] { 1, 2, 3, 4, 5 };
    }

    public async Task LoadAsync()
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == _playerId);
        if (player == null) return;
        PlayerName = player.Name;
        SuggestedWage = TransferService.EstimateRequestedMonthlyWage(player);
        ProposedWageText = SuggestedWage.ToString("F0");
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!decimal.TryParse(ProposedWageText, out var wage) || wage < 0)
            wage = SuggestedWage;
        int years = Math.Clamp(ContractYears, 1, 5);
        await _transferService.RenewContractAsync(_playerId, years, wage, _clock.CurrentDate);
        StatusMessage = "Contract renewed successfully.";
        IsSuccess = true;
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsSuccess));
    }

    [RelayCommand]
    private void Close() => _onDone?.Invoke();

    public int[] ContractYearsOptions { get; }
}
