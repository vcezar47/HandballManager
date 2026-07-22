using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class LeagueTableViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly LeagueService _leagueService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Action<string>? _onNavigateToLeagueHistory;

    [ObservableProperty]
    private List<LeagueEntry> _standings = [];

    [ObservableProperty]
    private List<LeagueEntry> _champGroupAStandings = [];

    [ObservableProperty]
    private List<LeagueEntry> _champGroupBStandings = [];

    [ObservableProperty]
    private List<LeagueEntry> _relegationGroupStandings = [];

    [ObservableProperty]
    private List<KvKnockoutFixtureRow> _kvindeligaenKnockoutRows = [];

    [ObservableProperty]
    private string _kvindeligaenTableSectionTitle = "";

    /// <summary>0 = Regular, 1 = Group stage, 2 = Knockout fixtures.</summary>
    [ObservableProperty]
    private int _kvindeligaenTabIndex;

    [ObservableProperty]
    private string _competitionName = "Liga Florilor";

    [ObservableProperty]
    private List<LeaderboardRow> _topScorers = [];

    [ObservableProperty]
    private List<LeaderboardRow> _topAssists = [];

    [ObservableProperty]
    private List<LeaderboardRow> _topSaves = [];

    [ObservableProperty]
    private List<LeaderboardRow> _topRated = [];

    /// <summary>0 = goals, 1 = assists, 2 = saves, 3 = rating.</summary>
    [ObservableProperty]
    private int _leaderboardTabIndex;

    public List<LeaderboardRow> ActiveLeaderboard => LeaderboardTabIndex switch
    {
        1 => TopAssists,
        2 => TopSaves,
        3 => TopRated,
        _ => TopScorers
    };

    public string ActiveLeaderboardStatHeader => LeaderboardTabIndex switch
    {
        1 => "AST",
        2 => "SAV",
        3 => "AVG",
        _ => "GLS"
    };

    public bool IsKvindeligaen =>
        string.Equals(CompetitionName, LeagueService.KvindeligaenCompetition, StringComparison.Ordinal);

    public bool KvShowRegularStandings => IsKvindeligaen && KvindeligaenTabIndex == 0;

    public bool KvShowGroupTables => IsKvindeligaen && KvindeligaenTabIndex == 1;

    public bool KvShowKnockoutList => IsKvindeligaen && KvindeligaenTabIndex == 2;

    /// <summary>String key for the active Kvindeligaen tab (for XAML DataTrigger highlighting).</summary>
    public string SelectedKvTabName => KvindeligaenTabIndex switch
    {
        0 => "Regular",
        1 => "GroupStage",
        2 => "Knockout",
        _ => "Regular"
    };

    /// <summary>Standard league-wide table (Kv regular tab or non-Danish leagues).</summary>
    public bool ShowStandardStandingsGrid => !IsKvindeligaen || KvShowRegularStandings;

    public LeagueTableViewModel(HandballDbContext db, LeagueService leagueService, Action<Team>? onTeamSelected = null, Action<string>? onNavigateToLeagueHistory = null)
    {
        Title = "League Table";
        _db = db;
        _leagueService = leagueService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueHistory = onNavigateToLeagueHistory;
    }

    partial void OnCompetitionNameChanged(string value)
    {
        OnPropertyChanged(nameof(IsKvindeligaen));
        NotifyKvTabLayoutProperties();
    }

    partial void OnKvindeligaenTabIndexChanged(int value) => NotifyKvTabLayoutProperties();

    private void NotifyKvTabLayoutProperties()
    {
        OnPropertyChanged(nameof(KvShowRegularStandings));
        OnPropertyChanged(nameof(KvShowGroupTables));
        OnPropertyChanged(nameof(KvShowKnockoutList));
        OnPropertyChanged(nameof(ShowStandardStandingsGrid));
        OnPropertyChanged(nameof(SelectedKvTabName));
    }

    public async Task InitializeAsync(string? competitionOverride = null)
    {
        var playerTeam = await _db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
        CompetitionName = competitionOverride ?? playerTeam?.CompetitionName ?? "Liga Florilor";
        ChampGroupAStandings = [];
        ChampGroupBStandings = [];
        RelegationGroupStandings = [];
        KvindeligaenKnockoutRows = [];
        KvindeligaenTabIndex = 0;

        if (IsKvindeligaen)
            await LoadKvindeligaenTabAsync(0);
        else
        {
            Standings = await _leagueService.GetStandingsAsync(CompetitionName);
            NotifyKvTabLayoutProperties();
        }

        await LoadLeaderboardsAsync();
    }

    private async Task LoadLeaderboardsAsync()
    {
        var boards = await _leagueService.GetLeaderboardsAsync(CompetitionName);
        TopScorers = boards.TopScorers;
        TopAssists = boards.TopAssists;
        TopSaves = boards.TopSaves;
        TopRated = boards.TopRated;
        NotifyLeaderboardChanged();
    }

    [RelayCommand]
    private void SelectLeaderboardTab(object? parameter)
    {
        LeaderboardTabIndex = parameter switch
        {
            int i => Math.Clamp(i, 0, 3),
            string s when int.TryParse(s, out var n) => Math.Clamp(n, 0, 3),
            _ => 0
        };
    }

    partial void OnLeaderboardTabIndexChanged(int value) => NotifyLeaderboardChanged();

    private void NotifyLeaderboardChanged()
    {
        OnPropertyChanged(nameof(ActiveLeaderboard));
        OnPropertyChanged(nameof(ActiveLeaderboardStatHeader));
        OnPropertyChanged(nameof(SelectedLeaderboardTabName));
    }

    /// <summary>String key for the active leaderboard tab (for XAML DataTrigger highlighting).</summary>
    public string SelectedLeaderboardTabName => LeaderboardTabIndex switch
    {
        1 => "Assists",
        2 => "Saves",
        3 => "Rating",
        _ => "Goals"
    };

    [RelayCommand]
    private async Task SelectKvindeligaenTab(object? parameter)
    {
        int idx = parameter switch
        {
            int i => i,
            string s when int.TryParse(s, out var n) => n,
            _ => 0
        };
        idx = Math.Clamp(idx, 0, 2);
        await LoadKvindeligaenTabAsync(idx);
    }

    private async Task LoadKvindeligaenTabAsync(int index)
    {
        KvindeligaenTabIndex = index;

        switch (index)
        {
            case 0:
                Standings = await _leagueService.GetKvindeligaenComputedRegularStandingsAsync();
                KvindeligaenTableSectionTitle =
                    $"Regular season (final table — all {LeagueService.KvindeligaenRegularSeasonRounds} rounds)";
                break;
            case 1:
                ChampGroupAStandings = await _leagueService.GetKvindeligaenComputedMiniLeagueStandingsAsync("ChampGroupA");
                ChampGroupBStandings = await _leagueService.GetKvindeligaenComputedMiniLeagueStandingsAsync("ChampGroupB");
                RelegationGroupStandings = await _leagueService.GetKvindeligaenComputedMiniLeagueStandingsAsync("Relegation");
                KvindeligaenTableSectionTitle = "Post-season groups (championship pools + relegation mini league)";
                break;
            default:
                KvindeligaenKnockoutRows = await _leagueService.GetKvindeligaenKnockoutRowsAsync();
                KvindeligaenTableSectionTitle = "Semi-finals, 3rd place & final (best-of-3)";
                break;
        }

        NotifyKvTabLayoutProperties();
    }

    [RelayCommand]
    private void ViewTeam(LeagueEntry? entry)
    {
        var team = entry?.Team;
        if (team is null) return;
        _onTeamSelected?.Invoke(team);
    }

    [RelayCommand]
    private void ViewLeagueHistory()
    {
        _onNavigateToLeagueHistory?.Invoke(CompetitionName);
    }
}
