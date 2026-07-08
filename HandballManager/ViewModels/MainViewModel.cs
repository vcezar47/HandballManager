using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using HandballManager.Views.Dialogs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace HandballManager.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    // Game dependencies — rebuilt whenever a new game is started or a save is loaded.
    private HandballDbContext _db = null!;
    private LeagueService _leagueService = null!;
    private SimulationEngine _simulationEngine = null!;
    private GameClock _clock = null!;
    private ScoutingService _scouting = null!;
    private TransferService _transferService = null!;
    private YouthIntakeService _youthIntakeService = null!;
    private CupService _cupService = null!;
    private SupercupService _supercupService = null!;
    private FacilityService _facilityService = null!;
    private GameStateService _gameStateService = null!;

    private readonly Stack<BaseViewModel> _backStack = new();
    private readonly Stack<BaseViewModel> _forwardStack = new();

    // The file the current game is saved to (null until first save / on a fresh game).
    private string? _currentSavePath;

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGameChrome))]
    private bool _isGameStarted;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGameChrome))]
    private bool _canNavigate = true;

    /// <summary>The in-game top-right Save/Exit bar shows only during normal play (hidden on the menu and mid-live-match).</summary>
    public bool ShowGameChrome => IsGameStarted && CanNavigate;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    private string _transientMessage = string.Empty;

    public bool HasToast => !string.IsNullOrEmpty(TransientMessage);
    private int _toastToken;

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

    // Shell VM (lives for the whole app session).
    public MainMenuViewModel MainMenuVM { get; }

    // Game child VMs (rebuilt per game).
    public HomeViewModel HomeVM { get; private set; } = null!;
    public RosterViewModel RosterVM { get; private set; } = null!;
    public LeagueTableViewModel LeagueTableVM { get; private set; } = null!;
    public CompetitionsViewModel CompetitionsVM { get; private set; } = null!;
    public CupDetailViewModel CupDetailVM { get; private set; } = null!;
    public ScoutingViewModel ScoutingVM { get; private set; } = null!;
    public LeagueHistoryViewModel LeagueHistoryVM { get; private set; } = null!;
    public CupHistoryViewModel CupHistoryVM { get; private set; } = null!;
    public SupercupDetailViewModel SupercupDetailVM { get; private set; } = null!;
    public SupercupHistoryViewModel SupercupHistoryVM { get; private set; } = null!;
    public FinancesViewModel FinancesVM { get; private set; } = null!;
    public ContractsViewModel ContractsVM { get; private set; } = null!;
    public TransfersViewModel TransfersVM { get; private set; } = null!;
    public NewsViewModel NewsVM { get; private set; } = null!;
    public YouthViewModel YouthVM { get; private set; } = null!;
    public StartViewModel StartVM { get; private set; } = null!;
    public WorldLeaguesViewModel WorldLeaguesVM { get; private set; } = null!;
    public SquadSelectionViewModel? SquadSelectionVM { get; private set; }
    public LiveMatchViewModel? LiveMatchVM { get; private set; }

    private bool _liveMatchInProgress = false;

    public MainViewModel()
    {
        MainMenuVM = new MainMenuViewModel(NewGameAsync, LoadGamePromptAsync);
        _currentViewModel = MainMenuVM;
    }

    // ── World building ──────────────────────────────────────────────────

    private HandballDbContext CreateContext()
    {
        var db = new HandballDbContext();
        db.SavedChanges += OnDbSavedChanges;
        return db;
    }

    private void DisposeContext()
    {
        if (_db != null)
        {
            _db.SavedChanges -= OnDbSavedChanges;
            _db.Dispose();
        }
        // Release any pooled SQLite handle so the working db file can be deleted (New Game) or replaced (Load).
        SqliteConnection.ClearAllPools();
    }

    private void OnDbSavedChanges(object? sender, SavedChangesEventArgs e)
    {
        // Any persisted write during an active game means there is progress to save.
        if (IsGameStarted && e.EntitiesSavedCount > 0)
            HasUnsavedChanges = true;
    }

    private void BuildServices()
    {
        _leagueService = new LeagueService(_db);
        var progressionService = new PlayerProgressionService();
        _transferService = new TransferService(_db);
        _youthIntakeService = new YouthIntakeService(_db);
        _cupService = new CupService(_db);
        _supercupService = new SupercupService(_db);
        _facilityService = new FacilityService(_db);
        _simulationEngine = new SimulationEngine(_db, progressionService, _transferService, _youthIntakeService,
            _cupService, _supercupService, _leagueService, _facilityService);
        _scouting = new ScoutingService(_clock);
        _gameStateService = new GameStateService(_db);
    }

    private void BuildChildVms()
    {
        StartVM = new StartViewModel(_db, OnTeamSelected);
        HomeVM = new HomeViewModel(_db, _leagueService, _simulationEngine, _cupService, _supercupService, _clock,
            NavigateToMatchDetail, NavigateToSquadSelection, OnGameAdvancedAsync);
        RosterVM = new RosterViewModel(_db, NavigateToPlayerDetail, OpenContractRenewal);
        LeagueTableVM = new LeagueTableViewModel(_db, _leagueService, NavigateToTeamRoster, async (comp) => await NavigateToLeagueHistoryAsync(comp));
        CompetitionsVM = new CompetitionsViewModel(_db, _leagueService, _cupService, _supercupService, NavigateToTeamRoster,
            async () => await NavigateToLeagueDetailAsync(),
            async () => await NavigateToCupDetailAsync(),
            async () => await NavigateToSupercupDetailAsync());
        CupDetailVM = new CupDetailViewModel(_cupService, _db, NavigateToTeamRoster, async (comp) => await NavigateToCupHistoryAsync(comp));
        ScoutingVM = new ScoutingViewModel(_db, _clock, _scouting, NavigateToPlayerDetail, _transferService, OpenTransferNegotiation);
        LeagueHistoryVM = new LeagueHistoryViewModel(_db);
        CupHistoryVM = new CupHistoryViewModel(_db);
        SupercupDetailVM = new SupercupDetailViewModel(_supercupService, NavigateToTeamRoster, NavigateToSupercupHistoryFromDetailAsync);
        SupercupHistoryVM = new SupercupHistoryViewModel(_db);
        FinancesVM = new FinancesViewModel(_db);
        ContractsVM = new ContractsViewModel(_db, _clock);
        TransfersVM = new TransfersViewModel(_db, _transferService, _clock, RefreshPendingOfferCountAsync, OpenTransferNegotiation);
        NewsVM = new NewsViewModel(_db, RefreshUnreadNewsCountAsync);
        YouthVM = new YouthViewModel(_db, _clock, _youthIntakeService, _transferService, OpenYouthSign, OpenYouthDetail);
        WorldLeaguesVM = new WorldLeaguesViewModel(
            _db, _leagueService, _cupService, _supercupService,
            onTeamSelected: NavigateToTeamRoster,
            onNavigateToLeagueDetail: async (comp) => await NavigateToLeagueDetailAsync(comp),
            onNavigateToLeagueHistory: async (comp) => await NavigateToLeagueHistoryAsync(comp),
            onNavigateToCupDetail: async (comp) => await NavigateToCupDetailAsync(comp),
            onNavigateToSupercupDetail: async () => await NavigateToSupercupDetailAsync(WorldLeaguesVM?.SelectedCompetition ?? "Liga Florilor"));
    }

    // ── New game / load / save / exit ───────────────────────────────────

    private async Task NewGameAsync()
    {
        // Only reachable from the main menu, so there is no active game to warn about.
        IsBusy = true;
        try
        {
            DisposeContext();
            _db = CreateContext();
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
            DatabaseSeeder.Seed(_db);

            LeagueService.RestoreSeasonYear(2025);
            TransferService.TransfersEnabled = true;
            _clock = new GameClock(LeagueService.GameSeasonStartDate);

            BuildServices();
            await _supercupService.InitializeInitialSupercupAsync();
            await _supercupService.InitializeDanishSupercupAsync();
            await _cupService.GenerateCupAsync();
            BuildChildVms();

            _currentSavePath = null;
            IsGameStarted = false;
            HasUnsavedChanges = false;
            _backStack.Clear();
            _forwardStack.Clear();
            UpdateNavigationState();

            CurrentViewModel = StartVM;
            await StartVM.InitializeAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGamePromptAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Load Game",
            Filter = GameSaveFile.DialogFilter,
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        await LoadGameAsync(dlg.FileName);
    }

    private async Task LoadGameAsync(string path)
    {
        IsBusy = true;
        try
        {
            // Validate the candidate save BEFORE overwriting the working database.
            var probe = await GameSaveFile.PeekStateAsync(path);
            if (probe == null)
            {
                AppDialog.Info("Couldn't Load Game", "This file isn't a valid Handball Manager save.");
                return;
            }
            if (probe.SaveFormatVersion > GameStateService.CurrentSaveVersion)
            {
                AppDialog.Info("Couldn't Load Game", "This save was created with a newer version of the game and can't be opened.");
                return;
            }

            string workingPath;
            using (var tmp = new HandballDbContext()) workingPath = tmp.DbPath;

            DisposeContext();
            try
            {
                GameSaveFile.CopyIntoWorkingDatabase(path, workingPath);
            }
            catch (Exception ex)
            {
                AppDialog.Info("Couldn't Load Game", $"The save file could not be opened.\n\n{ex.Message}");
                _db = CreateContext();
                return;
            }

            _db = CreateContext();
            var state = await new GameStateService(_db).ReadStateAsync();
            if (state == null)
            {
                AppDialog.Info("Couldn't Load Game", "The save file is missing its game state.");
                return;
            }

            LeagueService.RestoreSeasonYear(state.CurrentSeasonYear);
            TransferService.TransfersEnabled = true;
            _clock = new GameClock(state.CurrentDate);

            BuildServices();
            _simulationEngine.RestoreProgressionDates(state.LastDailyProgressionDate, state.LastWeeklyWageDate);
            BuildChildVms();

            _currentSavePath = path;
            _backStack.Clear();
            _forwardStack.Clear();
            IsGameStarted = true;

            await HomeVM.InitializeAsync();
            await OnGameAdvancedAsync();
            CurrentViewModel = HomeVM;
            UpdateNavigationState();

            // The writes InitializeAsync may have made are part of the loaded state, not new progress.
            HasUnsavedChanges = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveGame()
    {
        if (!IsGameStarted) return;

        string? path = _currentSavePath;
        if (path == null)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Game",
                Filter = GameSaveFile.DialogFilter,
                DefaultExt = GameSaveFile.Extension,
                AddExtension = true,
                FileName = "Handball Manager Save"
            };
            if (dlg.ShowDialog() != true) return;
            path = dlg.FileName;
        }

        IsBusy = true;
        try
        {
            await _gameStateService.WriteStateAsync(_clock, _simulationEngine);
            GameSaveFile.SaveTo(_db, path);
            _currentSavePath = path;
            HasUnsavedChanges = false;
            ShowToast("Game saved");
        }
        catch (Exception ex)
        {
            AppDialog.Info("Save Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ExitToMainMenu()
    {
        if (_liveMatchInProgress) return;

        if (HasUnsavedChanges)
        {
            bool exit = AppDialog.Confirm(
                "Exit to Main Menu?",
                "You have unsaved changes that will be lost if you leave now. Exit without saving?",
                confirmText: "Exit", cancelText: "Stay");
            if (!exit) return;
        }

        IsGameStarted = false;
        _backStack.Clear();
        _forwardStack.Clear();
        CurrentViewModel = MainMenuVM;
        UpdateNavigationState();
    }

    /// <summary>True if it's safe to close the app (no unsaved game, or the user confirms). Called from the window's close handler.</summary>
    public bool ConfirmQuit()
    {
        if (!HasUnsavedChanges) return true;
        return AppDialog.Confirm(
            "Quit Game?",
            "You have unsaved changes that will be lost. Quit without saving?",
            confirmText: "Quit", cancelText: "Stay");
    }

    private async void ShowToast(string message)
    {
        TransientMessage = message;
        int token = ++_toastToken;
        await Task.Delay(1800);
        if (token == _toastToken) TransientMessage = string.Empty;
    }

    // ── Badge refreshes (called explicitly after game advances) ─────────

    private async Task OnGameAdvancedAsync()
    {
        await RefreshPendingOfferCountAsync();
        await RefreshUnreadNewsCountAsync();
        await RefreshYouthIntakeActiveAsync();
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

    // ── Navigation ──────────────────────────────────────────────────────

    private async void OnTeamSelected(Team team)
    {
        IsGameStarted = true;
        _currentSavePath = null;
        HasUnsavedChanges = true; // a freshly started game has progress that isn't saved yet

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

    private void NavigateToSquadSelection(int homeTeamId, int awayTeamId, string venueName, string matchInfo, bool isKnockout)
    {
        var home = _db.Teams.Include(t => t.Players).FirstOrDefault(t => t.Id == homeTeamId);
        var away = _db.Teams.Include(t => t.Players).FirstOrDefault(t => t.Id == awayTeamId);
        if (home == null || away == null) return;

        var userTeam = _db.Teams.FirstOrDefault(t => t.IsPlayerTeam);
        bool isHome = userTeam?.Id == homeTeamId;

        SquadSelectionVM = new SquadSelectionViewModel(home, away, isHome, 1.05, venueName, matchInfo, isKnockout, NavigateToArena);
        NavigateTo(SquadSelectionVM);
    }

    private void NavigateToArena(LiveMatchEngine engine, SquadSelection homeSquad, SquadSelection awaySquad)
    {
        _liveMatchInProgress = true;

        bool isUserHome = engine.HomeTeam.IsPlayerTeam;
        LiveMatchVM = new LiveMatchViewModel(engine, isUserHome, OnLiveMatchEnded);
        NavigateTo(LiveMatchVM);
    }

    private async void OnLiveMatchEnded(LiveMatchEngine engine, SquadSelection homeSquad, SquadSelection awaySquad)
    {
        _liveMatchInProgress = false;
        UpdateNavigationState(); // Re-enable nav buttons if applicable

        int matchId = await _simulationEngine.RecordLiveMatchAndSimulateRestAsync(engine);

        // Clean up rendering hook
        LiveMatchVM?.Cleanup();

        await HomeVM.InitializeAsync();
        await OnGameAdvancedAsync();
        await NavigateToMatchDetail(matchId);
    }

    private void NavigateTo(BaseViewModel target)
    {
        if (target == CurrentViewModel || target is null) return;

        if (CurrentViewModel != null)
            _backStack.Push(CurrentViewModel);

        _forwardStack.Clear();
        CurrentViewModel = target;

        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        if (_liveMatchInProgress)
        {
            CanGoBack = false;
            CanGoForward = false;
            CanNavigate = false;
        }
        else
        {
            CanGoBack = _backStack.Count > 0;
            CanGoForward = _forwardStack.Count > 0;
            CanNavigate = true;
        }
    }

    public void NavigateToPlayerDetail(Player player)
    {
        bool isPlayerTeamPlayer = _db.Teams.Find(player.TeamId)?.IsPlayerTeam == true;
        var vm = new PlayerDetailViewModel(player, isPlayerTeamPlayer, _scouting);
        NavigateTo(vm);
    }

    public async void NavigateToTeamRoster(Team team)
    {
        var vm = new ClubInfoViewModel(_db, _scouting, _transferService, _facilityService, _clock, NavigateToPlayerDetail, OpenTransferNegotiation, this);
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
        if (_liveMatchInProgress || _backStack.Count == 0) return;
        var target = _backStack.Pop();
        if (CurrentViewModel != null)
            _forwardStack.Push(CurrentViewModel);
        CurrentViewModel = target;
        UpdateNavigationState();
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
    private async Task NavigateToWorldLeaguesAsync()
    {
        await WorldLeaguesVM.InitializeAsync();
        NavigateTo(WorldLeaguesVM);
    }

    [RelayCommand]
    private async Task NavigateToLeagueDetailAsync(string? competitionName = null)
    {
        await LeagueTableVM.InitializeAsync(competitionName);
        NavigateTo(LeagueTableVM);
    }

    [RelayCommand]
    private async Task NavigateToCupDetailAsync(string? competitionName = null)
    {
        await CupDetailVM.InitializeAsync(competitionName);
        NavigateTo(CupDetailVM);
    }

    [RelayCommand]
    private async Task NavigateToScoutingAsync()
    {
        await ScoutingVM.InitializeAsync();
        NavigateTo(ScoutingVM);
    }

    [RelayCommand]
    private async Task NavigateToLeagueHistoryAsync(string? competitionName = null)
    {
        await LeagueHistoryVM.InitializeAsync(competitionName);
        NavigateTo(LeagueHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToCupHistoryAsync(string? competitionName = null)
    {
        await CupHistoryVM.InitializeAsync(competitionName);
        NavigateTo(CupHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToSupercupDetailAsync(string? competitionName = null)
    {
        if (string.IsNullOrEmpty(competitionName))
        {
            var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
            competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        }

        await SupercupDetailVM.InitializeAsync(competitionName);
        NavigateTo(SupercupDetailVM);
    }

    private async Task NavigateToSupercupHistoryFromDetailAsync()
    {
        await SupercupHistoryVM.InitializeAsync(SupercupDetailVM.ActiveCompetition);
        NavigateTo(SupercupHistoryVM);
    }

    [RelayCommand]
    private async Task NavigateToSupercupHistoryAsync()
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        var competitionName = playerTeam?.CompetitionName ?? "Liga Florilor";
        await SupercupHistoryVM.InitializeAsync(competitionName);
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
        if (_liveMatchInProgress || _forwardStack.Count == 0) return;
        var target = _forwardStack.Pop();
        if (CurrentViewModel != null)
            _backStack.Push(CurrentViewModel);

        CurrentViewModel = target;
        UpdateNavigationState();
    }
}
