using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly LeagueService _leagueService;
    private readonly SimulationEngine _simulationEngine;
    private readonly GameClock _clock;
    private readonly ScoutingService _scouting;
    private readonly TransferService _transferService;
    private readonly YouthIntakeService _youthIntakeService;
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;

    private readonly Stack<BaseViewModel> _backStack = new();
    private readonly Stack<BaseViewModel> _forwardStack = new();

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    [ObservableProperty]
    private bool _isGameStarted;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingOffers))]
    private int _pendingOfferCount;

    public bool HasPendingOffers => PendingOfferCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNews))]
    private int _unreadNewsCount;

    public bool HasUnreadNews => UnreadNewsCount > 0;

    [ObservableProperty]
    private bool _isYouthIntakeActive;

    // Child VMs
    public MainMenuViewModel MainMenuVM { get; }
    public HomeViewModel HomeVM { get; }
    public RosterViewModel RosterVM { get; }
    public LeagueTableViewModel LeagueTableVM { get; }
    public CompetitionsViewModel CompetitionsVM { get; }
    public CupDetailViewModel CupDetailVM { get; }
    public ScoutingViewModel ScoutingVM { get; }
    public LeagueHistoryViewModel LeagueHistoryVM { get; }
    public CupHistoryViewModel CupHistoryVM { get; }
    public SupercupDetailViewModel SupercupDetailVM { get; }
    public SupercupHistoryViewModel SupercupHistoryVM { get; }
    public FinancesViewModel FinancesVM { get; }
    public ContractsViewModel ContractsVM { get; }
    public TransfersViewModel TransfersVM { get; }
    public NewsViewModel NewsVM { get; }
    public YouthViewModel YouthVM { get; }
    public StartViewModel StartVM { get; }

    public MainViewModel(HandballDbContext db, LeagueService leagueService, SimulationEngine simulationEngine, GameClock clock, ScoutingService scouting, TransferService transferService, YouthIntakeService youthIntakeService, CupService cupService, SupercupService supercupService)
    {
        _db = db;
        _leagueService = leagueService;
        _simulationEngine = simulationEngine;
        _clock = clock;
        _scouting = scouting;
        _transferService = transferService;
        _youthIntakeService = youthIntakeService;
        _cupService = cupService;
        _supercupService = supercupService;

        StartVM = new StartViewModel(db, OnTeamSelected);
        MainMenuVM = new MainMenuViewModel(async () =>
        {
            NavigateTo(StartVM);
            await StartVM.InitializeAsync();
        });
        HomeVM = new HomeViewModel(db, leagueService, simulationEngine, cupService, supercupService, clock, NavigateToMatchDetail);
        RosterVM = new RosterViewModel(db, NavigateToPlayerDetail, OpenContractRenewal);
        LeagueTableVM = new LeagueTableViewModel(leagueService, NavigateToTeamRoster, async () => await NavigateToLeagueHistoryAsync());
        CompetitionsVM = new CompetitionsViewModel(leagueService, cupService, supercupService, NavigateToTeamRoster,
            async () => await NavigateToLeagueDetailAsync(),
            async () => await NavigateToCupDetailAsync(),
            async () => await NavigateToSupercupDetailAsync());
        CupDetailVM = new CupDetailViewModel(cupService, NavigateToTeamRoster, async () => await NavigateToCupHistoryAsync());
        ScoutingVM = new ScoutingViewModel(db, clock, scouting, NavigateToPlayerDetail, _transferService, OpenTransferNegotiation);
        LeagueHistoryVM = new LeagueHistoryViewModel(db);
        CupHistoryVM = new CupHistoryViewModel(db);
        SupercupDetailVM = new SupercupDetailViewModel(supercupService, NavigateToTeamRoster, async () => await NavigateToSupercupHistoryAsync());
        SupercupHistoryVM = new SupercupHistoryViewModel(db);
        FinancesVM = new FinancesViewModel(db);
        ContractsVM = new ContractsViewModel(db, clock);
        TransfersVM = new TransfersViewModel(db, transferService, clock, RefreshPendingOfferCountAsync, OpenTransferNegotiation); 
        NewsVM = new NewsViewModel(db, RefreshUnreadNewsCountAsync);
        YouthVM = new YouthViewModel(db, clock, youthIntakeService, transferService, OpenYouthSign, OpenYouthDetail);

        _currentViewModel = MainMenuVM;

        _clock.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(GameClock.CurrentDate) && IsGameStarted)
            {
                await RefreshPendingOfferCountAsync();
                await RefreshUnreadNewsCountAsync();
                await RefreshYouthIntakeActiveAsync();
            }
        };
    }

    public async Task InitializeAsync()
    {
        await StartVM.InitializeAsync();
    }

    private async void OnTeamSelected(Team team)
    {
        IsGameStarted = true;

        _backStack.Clear();
        _forwardStack.Clear();
        CanGoBack = false;
        CanGoForward = false;

        // Ensure the team includes its manager
        await _db.Entry(team).Reference(t => t.Manager).LoadAsync();

        await HomeVM.InitializeAsync();
        await RefreshPendingOfferCountAsync();
        await RefreshUnreadNewsCountAsync();
        await RefreshYouthIntakeActiveAsync();

        CurrentViewModel = HomeVM;
    }

    public async Task RefreshPendingOfferCountAsync()
    {
        var userTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (userTeam == null) return;
        PendingOfferCount = await _db.TransferOffers
            .CountAsync(o => o.ForPlayer != null && o.ForPlayer.TeamId == userTeam.Id && o.Status == "Pending");
    }

    public async Task RefreshUnreadNewsCountAsync()
    {
        UnreadNewsCount = await _db.NewsItems.CountAsync(n => !n.IsRead);
    }

    public async Task RefreshYouthIntakeActiveAsync()
    {
        var date = _clock.CurrentDate;
        if (date.Month == 3 && date.Day >= 20 && date.Day <= 30)
        {
            var userTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            if (userTeam != null)
            {
                bool hasUnsigned = await _db.YouthIntakePlayers
                    .AnyAsync(y => y.ClubId == userTeam.Id && y.IntakeYear == date.Year);
                IsYouthIntakeActive = hasUnsigned;
                return;
            }
        }
        IsYouthIntakeActive = false;
    }

    private void NavigateTo(BaseViewModel target)
    {
        if (target == CurrentViewModel || target is null) return;

        if (CurrentViewModel != null)
            _backStack.Push(CurrentViewModel);

        _forwardStack.Clear();
        CurrentViewModel = target;

        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }

    public void NavigateToPlayerDetail(Player player)
    {
        bool isPlayerTeamPlayer = _db.Teams.Find(player.TeamId)?.IsPlayerTeam == true;
        var vm = new PlayerDetailViewModel(player, isPlayerTeamPlayer, _scouting);
        NavigateTo(vm);
    }

    public async void NavigateToTeamRoster(Team team)
    {
        var vm = new ClubInfoViewModel(_db, _scouting, _transferService, _clock, NavigateToPlayerDetail, OpenTransferNegotiation, this);
        await vm.InitializeAsync(team.Id);
        NavigateTo(vm);
    }

    private async void OpenContractRenewal(int playerId)
    {
        var vm = new ContractRenewalViewModel(_db, _transferService, _clock, playerId, DoNavigateBack);
        await vm.LoadAsync();
        NavigateTo(vm);
    }

    private async void OpenYouthSign(int youthId)
    {
        var vm = new YouthSignViewModel(_db, _youthIntakeService, _clock, youthId, async () =>
        {
            DoNavigateBack();
            await YouthVM.InitializeAsync();
        });
        await vm.LoadAsync();
        NavigateTo(vm);
    }

    private async void OpenYouthDetail(int youthId)
    {
        var vm = new YouthDetailViewModel(_db, youthId, () => OpenYouthSign(youthId), DoNavigateBack);
        await vm.LoadAsync();
        NavigateTo(vm);
    }

    private async void OpenTransferNegotiation(int playerId, string mode)
    {
        var player = await _db.Players
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null) return;
        var vm = new TransferNegotiationViewModel(_db, _transferService, _clock, player, mode, DoNavigateBack);
        NavigateTo(vm);
    }

    private void DoNavigateBack()
    {
        if (_backStack.Count == 0) return;
        var target = _backStack.Pop();
        if (CurrentViewModel != null)
            _forwardStack.Push(CurrentViewModel);
        CurrentViewModel = target;
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }

    public async Task NavigateToMatchDetail(int matchId)
    {
        var vm = new MatchDetailViewModel(_db, (teamId) => 
        {
            var team = _db.Teams.Find(teamId);
            if (team != null) NavigateToTeamRoster(team);
        });
        await vm.InitializeAsync(matchId);
        NavigateTo(vm);
    }

    [RelayCommand]
    private async Task NavigateToHomeAsync()
    {
        await HomeVM.InitializeAsync();
        NavigateTo(HomeVM);
    }

    [RelayCommand]
    private async Task NavigateToRosterAsync()
    {
        await RosterVM.InitializeAsync();
        NavigateTo(RosterVM);
    }

    [RelayCommand]
    private async Task NavigateToCompetitionsAsync()
    {
        await CompetitionsVM.InitializeAsync();
        NavigateTo(CompetitionsVM);
    }

    [RelayCommand]
    private async Task NavigateToLeagueDetailAsync()
    {
        await LeagueTableVM.InitializeAsync();
        NavigateTo(LeagueTableVM);
    }

    [RelayCommand]
    private async Task NavigateToCupDetailAsync()
    {
        await CupDetailVM.InitializeAsync();
        NavigateTo(CupDetailVM);
    }

    [RelayCommand]
    private async Task NavigateToScoutingAsync()
    {
        await ScoutingVM.InitializeAsync();
        NavigateTo(ScoutingVM);
    }

    [RelayCommand]
    private async Task NavigateToLeagueHistoryAsync()
    {
        await LeagueHistoryVM.InitializeAsync();
        NavigateTo(LeagueHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToCupHistoryAsync()
    {
        await CupHistoryVM.InitializeAsync();
        NavigateTo(CupHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToSupercupDetailAsync()
    {
        await SupercupDetailVM.InitializeAsync();
        NavigateTo(SupercupDetailVM);
    }

    [RelayCommand]
    private async Task NavigateToSupercupHistoryAsync()
    {
        await SupercupHistoryVM.InitializeAsync();
        NavigateTo(SupercupHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToProfile()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (playerTeam != null)
        {
            var manager = await _db.Managers.FirstOrDefaultAsync(m => m.TeamId == playerTeam.Id);
            if (manager != null)
            {
                NavigateToManagerDetail(manager);
            }
        }
    }

    public void NavigateToManagerDetail(Manager manager)
    {
        var vm = new ManagerDetailViewModel(manager);
        NavigateTo(vm);
    }

    [RelayCommand]
    private void NavigateToFinances()
    {
        var playerTeam = _db.Teams.FirstOrDefault(t => t.IsPlayerTeam);
        if (playerTeam != null)
        {
            FinancesVM.Initialize(playerTeam);
            NavigateTo(FinancesVM);
        }
    }

    [RelayCommand]
    private async Task NavigateToContractsAsync()
    {
        await ContractsVM.InitializeAsync();
        NavigateTo(ContractsVM);
    }

    [RelayCommand]
    private async Task NavigateToTransfersAsync()
    {
        await TransfersVM.InitializeAsync();
        NavigateTo(TransfersVM);
    }

    [RelayCommand]
    private async Task NavigateToNewsAsync()
    {
        await NewsVM.InitializeAsync();
        NavigateTo(NewsVM);
    }

    [RelayCommand]
    private async Task NavigateToYouthAsync()
    {
        await YouthVM.InitializeAsync();
        await RefreshYouthIntakeActiveAsync();
        NavigateTo(YouthVM);
    }

    [RelayCommand]
    private async Task NavigateToClubInfoAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (playerTeam != null)
        {
            NavigateToTeamRoster(playerTeam);
        }
    }

    [RelayCommand]
    private void NavigateBack() => DoNavigateBack();

    [RelayCommand]
    private void NavigateForward()
    {
        if (_forwardStack.Count == 0) return;
        var target = _forwardStack.Pop();
        if (CurrentViewModel != null)
            _backStack.Push(CurrentViewModel);

        CurrentViewModel = target;
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }
}