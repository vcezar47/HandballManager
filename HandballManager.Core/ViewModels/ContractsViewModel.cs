using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Data;
using HandballManager.Helpers;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class ContractsViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly GameClock _clock;

    [ObservableProperty]
    private ObservableCollection<ContractRowViewModel> _contracts = new();

    public ContractsViewModel(HandballDbContext db, GameClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task InitializeAsync()
    {
        var team = await _db.Teams.Include(t => t.Players).FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (team == null) { Contracts = new ObservableCollection<ContractRowViewModel>(); return; }
        var date = _clock.CurrentDate;
        var rows = team.Players
            .Select(p => new ContractRowViewModel(p.Name, p.Position, p.ShirtNumber, ContractDisplayHelper.FormatContractTimeLeft(p.ContractEndDate, date)))
            .OrderBy(c => c.PlayerName)
            .ToList();
        Contracts = new ObservableCollection<ContractRowViewModel>(rows);
    }
}

public class ContractRowViewModel : ObservableObject
{
    public string PlayerName { get; }
    public string Position { get; }
    public int ShirtNumber { get; }
    public string ContractTimeLeft { get; }

    public ContractRowViewModel(string playerName, string position, int shirtNumber, string contractTimeLeft)
    {
        PlayerName = playerName;
        Position = position;
        ShirtNumber = shirtNumber;
        ContractTimeLeft = contractTimeLeft;
    }
}
