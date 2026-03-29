using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Models;
using HandballManager.Data;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class MatchDetailViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Action<int> _onNavigateToTeam;

    [ObservableProperty]
    private MatchRecord? _match;

    [ObservableProperty]
    private List<MatchEvent> _homeGoals = [];

    [ObservableProperty]
    private List<MatchEvent> _awayGoals = [];

    [ObservableProperty]
    private List<ScoreHistoryItem> _scoreHistory = [];

    [ObservableProperty]
    private List<MatchPlayerStat> _homePlayerStats = [];

    [ObservableProperty]
    private List<MatchPlayerStat> _awayPlayerStats = [];

    public MatchDetailViewModel(HandballDbContext db, Action<int> onNavigateToTeam)
    {
        _db = db;
        _onNavigateToTeam = onNavigateToTeam;
        Title = "Match Details";
    }

    public void NavigateToHomeTeam()
    {
        if (Match != null) _onNavigateToTeam(Match.HomeTeamId);
    }

    public void NavigateToAwayTeam()
    {
        if (Match != null) _onNavigateToTeam(Match.AwayTeamId);
    }

    public async Task InitializeAsync(int matchId)
    {
        Match = await _db.MatchRecords
            .Include(m => m.MatchEvents)
            .Include(m => m.PlayerStats)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (Match != null)
        {
            var goals = Match.MatchEvents
                .Where(e => e.EventType == "Goal")
                .OrderBy(e => e.Minute)
                .ToList();

            HomeGoals = goals.Where(e => e.TeamId == Match.HomeTeamId).ToList();
            AwayGoals = goals.Where(e => e.TeamId == Match.AwayTeamId).ToList();

            var history = new List<ScoreHistoryItem>();
            int homeScore = 0;
            int awayScore = 0;

            foreach (var evt in Match.MatchEvents.OrderBy(e => e.Minute))
            {
                if (evt.EventType == "Goal")
                {
                    if (evt.TeamId == Match.HomeTeamId) homeScore++;
                    else awayScore++;

                    history.Add(new ScoreHistoryItem
                    {
                        PlayerName = evt.PlayerName,
                        Minute = evt.Minute,
                        HomeScore = homeScore,
                        AwayScore = awayScore,
                        IsHomeGoal = evt.TeamId == Match.HomeTeamId
                    });
                }
                else if (evt.EventType == "Shootout")
                {
                    history.Add(new ScoreHistoryItem
                    {
                        PlayerName = "PENALTY SHOOTOUT",
                        Minute = evt.Minute,
                        HomeScore = Match.HomePenaltyGoals,
                        AwayScore = Match.AwayPenaltyGoals,
                        IsHomeGoal = Match.HomePenaltyGoals > Match.AwayPenaltyGoals,
                        IsShootoutResult = true
                    });
                }
            }

            history.Reverse();
            ScoreHistory = history;

            HomePlayerStats = Match.PlayerStats
                .Where(ps => ps.TeamId == Match.HomeTeamId)
                .OrderByDescending(ps => ps.Rating)
                .ToList();

            AwayPlayerStats = Match.PlayerStats
                .Where(ps => ps.TeamId == Match.AwayTeamId)
                .OrderByDescending(ps => ps.Rating)
                .ToList();
        }
    }
}

public class ScoreHistoryItem
{
    public string PlayerName { get; set; } = string.Empty;
    public int Minute { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public bool IsHomeGoal { get; set; }
    public bool IsShootoutResult { get; set; }
    public string ScoreText => IsShootoutResult ? $"{HomeScore}–{AwayScore} (P)" : $"{HomeScore}–{AwayScore}";
}
