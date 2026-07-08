using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class TransfersViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly TransferService _transferService;
    private readonly GameClock _clock;
    private readonly Action<int, string>? _onOpenTransferNegotiation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoOffers))]
    private ObservableCollection<TransferOfferRowViewModel> _offers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFreeAgents))]
    private ObservableCollection<FreeAgentRowViewModel> _freeAgents = new();

    public bool HasNoOffers => Offers.Count == 0;
    public bool HasNoFreeAgents => FreeAgents.Count == 0;

    // Change constructor parameter from Action<int,string> to Func<Task>
    private readonly Func<Task>? _onRefresh;

    public TransfersViewModel(HandballDbContext db, TransferService transferService, GameClock clock,
        Func<Task>? onRefresh = null, Action<int, string>? onOpenTransferNegotiation = null)
    {
        _db = db;
        _transferService = transferService;
        _clock = clock;
        _onRefresh = onRefresh;
        _onOpenTransferNegotiation = onOpenTransferNegotiation;
    }

    public async Task InitializeAsync()
    {
        var userTeamId = (await _db.Teams.FirstAsync(t => t.IsPlayerTeam)).Id;

        var list = await _db.TransferOffers
            .Include(o => o.FromTeam)
            .Include(o => o.ForPlayer)
            .Where(o => o.ForPlayer != null && o.ForPlayer.TeamId == userTeamId && o.Status == "Pending")
            .OrderByDescending(o => o.OfferedAt)
            .Select(o => new TransferOfferRowViewModel(o.Id, o.ForPlayer!.Name, o.FromTeam!.Name, o.OfferType, o.OfferAmount, o.ProposedMonthlyWage, o.ProposedContractYears))
            .ToListAsync();
        Offers = new ObservableCollection<TransferOfferRowViewModel>(list);

        var freeAgentList = await _db.Players
            .Where(p => !p.IsRetired && p.TeamId == null)
            .OrderBy(p => p.Position)
            .ToListAsync(); // fetch from DB first

        FreeAgents = new ObservableCollection<FreeAgentRowViewModel>(
            freeAgentList
                .OrderBy(p => p.Position)
                .ThenByDescending(p => p.Overall100)
                .Select(p => new FreeAgentRowViewModel(p.Id, p.Name, p.Position, p.Age, p.Overall100, p.Nationality))
        );
    }

    [RelayCommand]
    private async Task AcceptOfferAsync(int offerId)
    {
        await _transferService.AcceptOfferAsync(offerId, _clock.CurrentDate);
        await InitializeAsync();
        if (_onRefresh != null) await _onRefresh();
    }

    [RelayCommand]
    private async Task RejectOfferAsync(int offerId)
    {
        await _transferService.RejectOfferAsync(offerId);
        await InitializeAsync();
        if (_onRefresh != null) await _onRefresh();
    }

    [RelayCommand]
    private void SignFreeAgent(int playerId)
    {
        _onOpenTransferNegotiation?.Invoke(playerId, "ApproachToSign");
    }
}

public class TransferOfferRowViewModel(int id, string playerName, string fromTeamName, string offerType, decimal offerAmount, decimal proposedWage, int proposedYears)
{
    public int Id => id;
    public string PlayerName => playerName;
    public string FromTeamName => fromTeamName;
    public string OfferType => offerType;
    public string OfferAmountText => offerType == "Buyout" ? $"{offerAmount:N0} €" : "Free";
    public string ProposedWageText => $"{proposedWage:N0} €/mo";
    public int ProposedYears => proposedYears;
}

public class FreeAgentRowViewModel(int id, string name, string position, int age, int overall, string nationality)
{
    public int Id => id;
    public string Name => name;
    public string Position => position;
    public int Age => age;
    public int Overall => overall;
    public string Nationality => nationality;
}