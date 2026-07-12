using HandballManager.Data;
using HandballManager.Mobile.Infrastructure;
using HandballManager.Models;
using HandballManager.Services;
using HandballManager.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Mobile.Session;

/// <summary>
/// Mobile counterpart of the desktop MainViewModel's world-building: owns the db context,
/// services, and the shared Core view models for the running game.
///
/// Unlike desktop there is no explicit save file — the working database in app-data IS the
/// save. Game state (clock, season, progression cursors) is written after team selection and
/// after every game-advancing action, so a career survives Android killing the process.
/// </summary>
public sealed class GameSession
{
    public static GameSession? Current { get; private set; }

    public HandballDbContext Db { get; }
    public GameClock Clock { get; }

    private readonly LeagueService _leagueService;
    private readonly SimulationEngine _simulationEngine;
    private readonly TransferService _transferService;
    private readonly YouthIntakeService _youthIntakeService;
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;
    private readonly FacilityService _facilityService;
    private readonly GameStateService _gameStateService;
    private readonly ScoutingService _scoutingService;

    public StartViewModel StartVM { get; }
    public HomeViewModel HomeVM { get; }
    public RosterViewModel RosterVM { get; }
    public TransfersViewModel TransfersVM { get; }
    public YouthViewModel YouthVM { get; }
    public FinancesViewModel FinancesVM { get; }
    public CompetitionsViewModel CompetitionsVM { get; }
    public WorldLeaguesViewModel WorldLeaguesVM { get; }
    public NewsViewModel NewsVM { get; }
    public ContractsViewModel ContractsVM { get; }
    public ScoutingViewModel ScoutingVM { get; }
    public LiveMatchViewModel? LiveMatchVM { get; private set; }
    public ClubBadgeCounts Badges { get; } = new();

    private GameSession(HandballDbContext db, GameClock clock)
    {
        Db = db;
        Clock = clock;

        _leagueService = new LeagueService(db);
        var progressionService = new PlayerProgressionService();
        _transferService = new TransferService(db);
        _youthIntakeService = new YouthIntakeService(db);
        _cupService = new CupService(db);
        _supercupService = new SupercupService(db);
        _facilityService = new FacilityService(db);
        _simulationEngine = new SimulationEngine(db, progressionService, _transferService, _youthIntakeService,
            _cupService, _supercupService, _leagueService, _facilityService);
        _gameStateService = new GameStateService(db);
        _scoutingService = new ScoutingService(clock);

        StartVM = new StartViewModel(db, OnTeamSelected);
        HomeVM = new HomeViewModel(db, _leagueService, _simulationEngine, _cupService, _supercupService, clock,
            NavigateToMatchDetailAsync, onNavigateToSquadSelection: NavigateToSquadSelection, onDayAdvanced: PersistStateAsync);
        RosterVM = new RosterViewModel(db, onPlayerSelected: NavigateToPlayerDetail, onOpenRenewContract: OpenContractRenewal);
        TransfersVM = new TransfersViewModel(db, _transferService, clock,
            onRefresh: RefreshBadgesAsync, onOpenTransferNegotiation: OpenTransferNegotiation);
        YouthVM = new YouthViewModel(db, clock, _youthIntakeService, _transferService, OpenYouthSign, OpenYouthDetail);
        FinancesVM = new FinancesViewModel(db);
        CompetitionsVM = new CompetitionsViewModel(db, _leagueService, _cupService, _supercupService,
            onTeamSelected: NavigateToTeamRoster,
            onNavigateToLeagueDetail: () => _ = GoToLeagueTableAsync(),
            onNavigateToCupDetail: () => _ = OpenCupDetailAsync(null),
            onNavigateToSupercupDetail: () => _ = OpenSupercupDetailAsync(null));
        // World Leagues already renders each country's table inline, so its cards don't
        // deep-link to the Table tab (that tab only ever shows the player's own league).
        WorldLeaguesVM = new WorldLeaguesViewModel(db, _leagueService, _cupService, _supercupService,
            onTeamSelected: NavigateToTeamRoster,
            onNavigateToLeagueHistory: comp => OpenLeagueHistoryAsync(comp),
            onNavigateToCupDetail: comp => OpenCupDetailAsync(comp),
            onNavigateToSupercupDetail: () => OpenSupercupDetailAsync(WorldLeaguesVM!.SelectedCompetition));
        NewsVM = new NewsViewModel(db, onNewsRead: RefreshBadgesAsync);
        ContractsVM = new ContractsViewModel(db, clock);
        ScoutingVM = new ScoutingViewModel(db, clock, _scoutingService, NavigateToPlayerDetail,
            _transferService, OpenTransferNegotiation);
    }

    /// <summary>The save slot the running session belongs to, or null when no career is loaded.</summary>
    public static int? CurrentSlot { get; private set; }

    /// <summary>Wipes the given slot, seeds a fresh world, and leaves the session on the setup flow.</summary>
    public static async Task<GameSession> NewGameAsync(int slot)
    {
        DisposeCurrent();
        SaveSlots.SetActive(slot);

        var db = new HandballDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        DatabaseSeeder.Seed(db);

        LeagueService.RestoreSeasonYear(2025);
        TransferService.TransfersEnabled = true;
        var clock = new GameClock(LeagueService.GameSeasonStartDate);

        var session = new GameSession(db, clock);
        await session._supercupService.InitializeInitialSupercupAsync();
        await session._supercupService.InitializeDanishSupercupAsync();
        await session._cupService.GenerateCupAsync();
        await session.StartVM.InitializeAsync();

        Current = session;
        CurrentSlot = slot;
        return session;
    }

    /// <summary>Loads the career stored in the given slot, or returns null when it holds none.</summary>
    public static async Task<GameSession?> LoadSlotAsync(int slot)
    {
        try
        {
            SaveSlots.SetActive(slot);

            var db = new HandballDbContext();
            if (!await db.Database.CanConnectAsync())
            {
                db.Dispose();
                return null;
            }

            var state = await new GameStateService(db).ReadStateAsync();
            bool hasCareer = await db.Teams.AnyAsync(t => t.IsPlayerTeam);
            if (state == null || !hasCareer || state.SaveFormatVersion > GameStateService.CurrentSaveVersion)
            {
                db.Dispose();
                return null;
            }

            DisposeCurrent();

            LeagueService.RestoreSeasonYear(state.CurrentSeasonYear);
            TransferService.TransfersEnabled = true;
            var clock = new GameClock(state.CurrentDate);

            var session = new GameSession(db, clock);
            session._simulationEngine.RestoreProgressionDates(state.LastDailyProgressionDate, state.LastWeeklyWageDate);
            await session.HomeVM.InitializeAsync();
            await session.RefreshBadgesAsync();

            Current = session;
            CurrentSlot = slot;
            return session;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Disposes the running session if it belongs to the given slot (before deleting that slot's file).</summary>
    public static void DisposeIfActive(int slot)
    {
        if (CurrentSlot == slot) DisposeCurrent();
    }

    private static void DisposeCurrent()
    {
        Current?.Db.Dispose();
        Current = null;
        CurrentSlot = null;
        TabBarState.Instance.Reset();
        // Release pooled SQLite handles so the working db file can be deleted/replaced.
        SqliteConnection.ClearAllPools();
    }

    /// <summary>Called by HomeViewModel after every game-advancing command — the mobile autosave.</summary>
    private async Task PersistStateAsync()
    {
        await _gameStateService.WriteStateAsync(Clock, _simulationEngine);
        await RefreshBadgesAsync();
    }

    /// <summary>Recomputes the Club tab badges (pending offers, unread news, unsigned youth intake).
    /// Mirrors the desktop MainViewModel's badge refreshes.</summary>
    public async Task RefreshBadgesAsync()
    {
        var userTeam = await Db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (userTeam == null)
        {
            Badges.TransfersCount = 0;
            Badges.NewsCount = 0;
            Badges.YouthCount = 0;
            return;
        }

        Badges.TransfersCount = await Db.TransferOffers
            .CountAsync(o => o.ForPlayer != null && o.ForPlayer.TeamId == userTeam.Id && o.Status == "Pending");
        Badges.NewsCount = await Db.NewsItems.CountAsync(n => !n.IsRead);

        var date = Clock.CurrentDate;
        Badges.YouthCount = date.Month == 3 && date.Day is >= 20 and <= 30
            ? await Db.YouthIntakePlayers.CountAsync(y => y.ClubId == userTeam.Id && y.IntakeYear == date.Year)
            : 0;

        TabBarState.Instance.HasNewActivity = Badges.HasAny;
        TabBarState.Instance.IsYouthIntakeActive = Badges.HasYouth;
    }

    private async void OnTeamSelected(Team team)
    {
        try
        {
            await Db.Entry(team).Reference(t => t.Manager).LoadAsync();
            await HomeVM.InitializeAsync();
            await PersistStateAsync();
            await Shell.Current.GoToAsync("//game/home");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Couldn't Start Career", ex);
        }
    }

    private async Task NavigateToMatchDetailAsync(int matchId)
    {
        var vm = new MatchDetailViewModel(Db, _ => { });
        await vm.InitializeAsync(matchId);
        await Shell.Current.Navigation.PushAsync(new MatchDetailPage(vm));
    }

    // ── Live match flow (mirrors desktop MainViewModel.NavigateToSquadSelection/Arena) ──

    private async void NavigateToSquadSelection(int homeTeamId, int awayTeamId, string venueName, string matchInfo, bool isKnockout)
    {
        try
        {
            var home = await Db.Teams.Include(t => t.Players).FirstOrDefaultAsync(t => t.Id == homeTeamId);
            var away = await Db.Teams.Include(t => t.Players).FirstOrDefaultAsync(t => t.Id == awayTeamId);
            if (home == null || away == null) return;

            bool isUserHome = home.IsPlayerTeam;
            var vm = new SquadSelectionViewModel(home, away, isUserHome, 1.05, venueName, matchInfo, isKnockout, NavigateToArena);
            await Shell.Current.Navigation.PushAsync(new SquadSelectionPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Squad Selection", ex);
        }
    }

    private async void NavigateToArena(LiveMatchEngine engine, SquadSelection homeSquad, SquadSelection awaySquad)
    {
        try
        {
            LiveMatchVM = new LiveMatchViewModel(engine, engine.HomeTeam.IsPlayerTeam, OnLiveMatchEnded, new DispatcherFrameTicker());

            var nav = Shell.Current.Navigation;
            var squadPage = nav.NavigationStack.LastOrDefault() as SquadSelectionPage;
            await nav.PushAsync(new LiveMatchPage(LiveMatchVM));
            if (squadPage != null) nav.RemovePage(squadPage);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Live Match", ex);
        }
    }

    private async void OnLiveMatchEnded(LiveMatchEngine engine, SquadSelection homeSquad, SquadSelection awaySquad)
    {
        try
        {
            LiveMatchVM?.Cleanup();
            LiveMatchVM = null;

            // Records the user's result, simulates the rest of the matchday, advances world state.
            int matchId = await _simulationEngine.RecordLiveMatchAndSimulateRestAsync(engine);

            await HomeVM.InitializeAsync();
            await PersistStateAsync();

            await Shell.Current.Navigation.PopToRootAsync(animated: false);
            await NavigateToMatchDetailAsync(matchId);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Match Result", ex);
        }
    }

    // ── Squad / market / academy navigation ──

    private async void NavigateToPlayerDetail(Player player)
    {
        try
        {
            bool isPlayerTeamPlayer = player.TeamId != null
                && (await Db.Teams.FindAsync(player.TeamId))?.IsPlayerTeam == true;
            var vm = new PlayerDetailViewModel(player, isPlayerTeamPlayer, _scoutingService);
            await Shell.Current.Navigation.PushAsync(new PlayerDetailPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Player Profile", ex);
        }
    }

    private async void OpenContractRenewal(int playerId)
    {
        try
        {
            var vm = new ContractRenewalViewModel(Db, _transferService, Clock, playerId,
                onDone: () => _ = Shell.Current.Navigation.PopAsync());
            await vm.LoadAsync();
            await Shell.Current.Navigation.PushAsync(new ContractRenewalPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Contract Renewal", ex);
        }
    }

    // ── Competitions / world navigation (mirrors desktop MainViewModel routes) ──

    private async void NavigateToTeamRoster(Team team) => await OpenClubInfoByIdAsync(team.Id);

    /// <summary>Opens the club info page for the player's own team (Club tab &gt; Information).</summary>
    public async Task OpenOwnClubInfoAsync()
    {
        var team = await Db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        if (team != null) await OpenClubInfoByIdAsync(team.Id);
    }

    /// <summary>Opens the club info page for a team id (league-table row taps, etc.).</summary>
    public async Task OpenClubInfoByIdAsync(int teamId)
    {
        try
        {
            var vm = new ClubInfoViewModel(Db, _scoutingService, _transferService, _facilityService, Clock,
                NavigateToPlayerDetail, OpenTransferNegotiation, NavigateToManagerDetail, new AlertNotifier());
            await vm.InitializeAsync(teamId);
            await Shell.Current.Navigation.PushAsync(new ClubInfoPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Club Info", ex);
        }
    }

    private async void NavigateToManagerDetail(Manager manager)
    {
        try
        {
            await Shell.Current.Navigation.PushAsync(new ManagerDetailPage(new ManagerDetailViewModel(manager)));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Manager Profile", ex);
        }
    }

    private static async Task GoToLeagueTableAsync()
    {
        await Shell.Current.GoToAsync("//game/table");
    }

    private async Task OpenCupDetailAsync(string? competition)
    {
        try
        {
            var vm = new CupDetailViewModel(_cupService, Db, NavigateToTeamRoster,
                onNavigateToHistory: comp => _ = OpenCupHistoryAsync(comp));
            await vm.InitializeAsync(competition);
            await Shell.Current.Navigation.PushAsync(new CupDetailPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Cup", ex);
        }
    }

    private async Task OpenSupercupDetailAsync(string? competition)
    {
        try
        {
            competition ??= (await Db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam))?.CompetitionName ?? "Liga Florilor";
            var vm = new SupercupDetailViewModel(_supercupService, NavigateToTeamRoster,
                onNavigateToHistory: () => OpenSupercupHistoryAsync(competition));
            await vm.InitializeAsync(competition);
            await Shell.Current.Navigation.PushAsync(new SupercupDetailPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Supercup", ex);
        }
    }

    public async Task OpenLeagueHistoryAsync(string competition)
    {
        try
        {
            var vm = new LeagueHistoryViewModel(Db);
            await vm.InitializeAsync(competition);
            var rows = vm.Champions.Select(c => new HonourRow(c.Season, c.TeamName,
                BuildPodiumDetail(c.RunnerUpTeamName, c.ThirdPlaceTeamName)));
            await Shell.Current.Navigation.PushAsync(
                new HonoursPage("LEAGUE ARCHIVES", $"{vm.CompetitionName} Champions", rows, vm.MedalTable));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("League History", ex);
        }
    }

    private async Task OpenCupHistoryAsync(string competition)
    {
        try
        {
            var vm = new CupHistoryViewModel(Db);
            await vm.InitializeAsync(competition);
            var rows = vm.Winners.Select(w => new HonourRow(w.Season, w.TeamName, null));
            await Shell.Current.Navigation.PushAsync(
                new HonoursPage("CUP ARCHIVES", $"{vm.CupDisplayName} Winners", rows, vm.MedalTable));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Cup History", ex);
        }
    }

    private async Task OpenSupercupHistoryAsync(string competition)
    {
        try
        {
            var vm = new SupercupHistoryViewModel(Db);
            await vm.InitializeAsync(competition);
            var rows = vm.Winners.Select(w => new HonourRow(w.Season, w.TeamName, null));
            await Shell.Current.Navigation.PushAsync(
                new HonoursPage("SUPERCUP ARCHIVES", vm.ArchivePageTitle, rows, vm.MedalTable));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Supercup History", ex);
        }
    }

    private static string? BuildPodiumDetail(string? runnerUp, string? third)
    {
        if (string.IsNullOrEmpty(runnerUp) && string.IsNullOrEmpty(third)) return null;
        var parts = new List<string>(2);
        if (!string.IsNullOrEmpty(runnerUp)) parts.Add($"2nd: {runnerUp}");
        if (!string.IsNullOrEmpty(third)) parts.Add($"3rd: {third}");
        return string.Join("  ·  ", parts);
    }

    private async void OpenTransferNegotiation(int playerId, string mode)
    {
        try
        {
            var player = await Db.Players.Include(p => p.Team).FirstOrDefaultAsync(p => p.Id == playerId);
            if (player == null) return;

            // TransfersPage refreshes itself OnAppearing, so closing just pops.
            var vm = new TransferNegotiationViewModel(Db, _transferService, Clock, player, mode,
                onDone: () => _ = Shell.Current.Navigation.PopAsync());
            await Shell.Current.Navigation.PushAsync(new TransferNegotiationPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Negotiation", ex);
        }
    }

    private async void OpenYouthSign(int youthId)
    {
        try
        {
            var vm = new YouthSignViewModel(Db, _youthIntakeService, Clock, youthId,
                onDone: () => _ = CloseYouthFlowAsync());
            await vm.LoadAsync();
            await Shell.Current.Navigation.PushAsync(new YouthSignPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Youth Signing", ex);
        }
    }

    private async void OpenYouthDetail(int youthId)
    {
        try
        {
            var vm = new YouthDetailViewModel(Db, youthId,
                onSign: () => OpenYouthSign(youthId),
                onBack: () => _ = Shell.Current.Navigation.PopAsync());
            await vm.LoadAsync();
            await Shell.Current.Navigation.PushAsync(new YouthDetailPage(vm));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Youth Prospect", ex);
        }
    }

    /// <summary>After a youth signing, pops the sign page (and the detail page beneath it) back to the academy list.</summary>
    private static async Task CloseYouthFlowAsync()
    {
        var nav = Shell.Current.Navigation;
        if (nav.NavigationStack.LastOrDefault() is YouthSignPage) await nav.PopAsync();
        if (nav.NavigationStack.LastOrDefault() is YouthDetailPage) await nav.PopAsync();
    }

    private static async Task ShowErrorAsync(string title, Exception ex)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null) await page.DisplayAlertAsync(title, ex.Message, "OK");
    }
}
