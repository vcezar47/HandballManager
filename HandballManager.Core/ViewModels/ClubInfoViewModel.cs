using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public class TrophyViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public int Count { get; set; }
}

public partial class ClubInfoViewModel : BaseViewModel
{
    private static readonly Dictionary<string, string[]> HistoricalNames = new()
    {
        { "SCM Râmnicu Vâlcea", new[] { "Chimistul Râmnicu Vâlcea", "Oltchim Râmnicu Vâlcea", "CS Oltchim Râmnicu Vâlcea" } },
        { "Universitatea București", new[] { "Știința București" } },
        { "Universitatea Timișoara", new[] { "Știința Timișoara", "Universitatea Știința Timișoara" } },
        { "HC Zalău", new[] { "Silcotub Zalău" } },
        { "Minaur Baia Mare", new[] { "HCM Baia Mare" } },
        { "CSM Corona Brașov", new[] { "Rulmentul Brașov" } },
        { "Rapid București", new[] { "CS Rapid București", "Rapid CFR București" } },
        { "CSM Bacău", new[] { "Universitatea Bacău", "Știința Bacău" } },
        { "CSU Târgu Mureș", new[] { "Progresul Târgu Mureș", "Mureșul Târgu Mureș" } },

        // Hungary (match historical winners names to current in-game club names)
        { "Győr", new[] { "Győri ETO", "Győri ETO KC", "Győri Audi ETO KC" } },
        { "DVSC", new[] { "Debreceni VSC", "Debrecen" } },
        { "Vasas SC", new[] { "Vasas", "Vasas Budapest" } },
        { "Dunaújváros", new[] { "Dunaferr", "Dunaújvárosi Kohász", "Dunaújvárosi Kohász KA", "DKKA" } },
        { "Ferencváros", new[] { "FTC-Rail Cargo Hungaria", "FTC" } },

        // France (match historical winners names to current in-game club names)
        { "Toulon Métropole Var Handball", new[] { "Toulon Handball", "Toulon Saint-Cyr Var HB" } },
        { "Le Havre Athletic Club Handball", new[] { "Le Havre AC", "Le Havre AC Handball", "Le Havre Athletic Club" } },
        { "Brest Bretagne Handball", new[] { "Brest Bretagne Handball" } },
        { "ESBF Besancon", new[] { "ES Besançon", "ES Besancon" } }
    };

    private readonly HandballDbContext _db;
    private readonly ScoutingService _scouting;
    private readonly TransferService _transferService;
    private readonly FacilityService _facilityService;
    private readonly GameClock _clock;
    private readonly Action<Player> _onPlayerSelected;
    private readonly Action<int, string> _onNegotiateTransfer;
    private readonly Action<Manager> _onManagerDetail;
    private readonly IUserNotifier _notifier;

    [ObservableProperty]
    private Team? _team;

    [ObservableProperty]
    private TeamRosterViewModel? _squadVM;

    [ObservableProperty]
    private Manager? _manager; // Added Manager property

    // ── Facility properties ────────────────────────────────────────────────
    [ObservableProperty] private bool _isPlayerTeam;
    [ObservableProperty] private string _trainingFacilityLabel = "";
    [ObservableProperty] private string _trainingFacilityColour = "#8888AA";
    [ObservableProperty] private string _trainingFacilityDescription = "";
    [ObservableProperty] private string _trainingUpgradeCostText = "";
    [ObservableProperty] private bool _canUpgradeTraining;
    [ObservableProperty] private bool _isTrainingUpgrading;
    [ObservableProperty] private string _trainingCompletionText = "";
    [ObservableProperty] private int _trainingFacilityLevel;

    [ObservableProperty] private string _youthFacilityLabel = "";
    [ObservableProperty] private string _youthFacilityColour = "#8888AA";
    [ObservableProperty] private string _youthFacilityDescription = "";
    [ObservableProperty] private string _youthUpgradeCostText = "";
    [ObservableProperty] private bool _canUpgradeYouth;
    [ObservableProperty] private bool _isYouthUpgrading;
    [ObservableProperty] private string _youthCompletionText = "";
    [ObservableProperty] private int _youthFacilityLevel;

    public ObservableCollection<TrophyViewModel> Trophies { get; } = new();

    // Modified constructor to include FacilityService
    public ClubInfoViewModel(HandballDbContext db, ScoutingService scouting, TransferService transferService, FacilityService facilityService, GameClock clock, Action<Player> onPlayerSelected, Action<int, string> onNegotiateTransfer, Action<Manager> onManagerDetail, IUserNotifier notifier)
    {
        _db = db;
        _scouting = scouting;
        _transferService = transferService;
        _facilityService = facilityService;
        _clock = clock;
        _onPlayerSelected = onPlayerSelected;
        _onNegotiateTransfer = onNegotiateTransfer;
        _onManagerDetail = onManagerDetail;
        _notifier = notifier;
    }

    [RelayCommand]
    private void NavigateToManagerDetail()
    {
        if (Manager != null)
        {
            _onManagerDetail(Manager);
        }
    }

    [RelayCommand]
    private async Task UpgradeTrainingFacility()
    {
        if (Team == null || !Team.IsPlayerTeam) return;
        var (success, message) = await _facilityService.UpgradeFacilityAsync(Team.Id, Services.FacilityType.Training, _clock.CurrentDate);
        if (success)
        {
            // Reload team from DB to pick up changes
            await _db.Entry(Team).ReloadAsync();
            RefreshFacilityProperties();
        }
        else
        {
            _notifier.Warn("Upgrade Failed", message);
        }
    }

    [RelayCommand]
    private async Task UpgradeYouthFacility()
    {
        if (Team == null || !Team.IsPlayerTeam) return;
        var (success, message) = await _facilityService.UpgradeFacilityAsync(Team.Id, Services.FacilityType.Youth, _clock.CurrentDate);
        if (success)
        {
            await _db.Entry(Team).ReloadAsync();
            RefreshFacilityProperties();
        }
        else
        {
            _notifier.Warn("Upgrade Failed", message);
        }
    }

    public async Task InitializeAsync(int teamId)
    {
        Team = await _db.Teams
            .Include(t => t.LeagueEntry)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (Team == null) return;

        IsPlayerTeam = Team.IsPlayerTeam;

        // Load Manager
        await _db.Entry(Team).Reference(t => t.Manager).LoadAsync();
        Manager = Team.Manager;

        // Initialize Squad VM
        SquadVM = new TeamRosterViewModel(_db, teamId, _scouting, _onPlayerSelected, _transferService, _clock, _onNegotiateTransfer);
        await SquadVM.InitializeAsync();

        // Load Trophies
        await LoadTrophiesAsync();

        // Refresh facility display
        RefreshFacilityProperties();
    }

    private void RefreshFacilityProperties()
    {
        if (Team == null) return;

        // Training
        TrainingFacilityLevel = Team.TrainingFacilityLevel;
        TrainingFacilityLabel = FacilityLevel.GetLabel(Team.TrainingFacilityLevel);
        TrainingFacilityColour = FacilityLevel.GetColour(Team.TrainingFacilityLevel);
        TrainingFacilityDescription = Team.TrainingFacilityLevel >= 0 && Team.TrainingFacilityLevel <= FacilityLevel.MaxLevel
            ? FacilityLevel.TrainingDescriptions[Team.TrainingFacilityLevel] : "";
        IsTrainingUpgrading = Team.TrainingFacilityUpgradeCompleteDate != null;

        if (Team.TrainingFacilityLevel >= FacilityLevel.MaxLevel)
        {
            TrainingUpgradeCostText = "MAX LEVEL";
            CanUpgradeTraining = false;
        }
        else
        {
            decimal cost = FacilityLevel.GetTrainingCost(Team.TrainingFacilityLevel);
            TrainingUpgradeCostText = $"{cost:N0} €";
            CanUpgradeTraining = IsPlayerTeam && !IsTrainingUpgrading && !IsYouthUpgrading && Team.ClubBalance >= cost;
        }

        TrainingCompletionText = IsTrainingUpgrading
            ? $"Completing: {Team.TrainingFacilityUpgradeCompleteDate:d MMM yyyy}"
            : "";

        // Youth
        YouthFacilityLevel = Team.YouthFacilityLevel;
        YouthFacilityLabel = FacilityLevel.GetLabel(Team.YouthFacilityLevel);
        YouthFacilityColour = FacilityLevel.GetColour(Team.YouthFacilityLevel);
        YouthFacilityDescription = Team.YouthFacilityLevel >= 0 && Team.YouthFacilityLevel <= FacilityLevel.MaxLevel
            ? FacilityLevel.YouthDescriptions[Team.YouthFacilityLevel] : "";
        IsYouthUpgrading = Team.YouthFacilityUpgradeCompleteDate != null;

        if (Team.YouthFacilityLevel >= FacilityLevel.MaxLevel)
        {
            YouthUpgradeCostText = "MAX LEVEL";
            CanUpgradeYouth = false;
        }
        else
        {
            decimal cost = FacilityLevel.GetYouthCost(Team.YouthFacilityLevel);
            YouthUpgradeCostText = $"{cost:N0} €";
            CanUpgradeYouth = IsPlayerTeam && !IsTrainingUpgrading && !IsYouthUpgrading && Team.ClubBalance >= cost;
        }

        YouthCompletionText = IsYouthUpgrading
            ? $"Completing: {Team.YouthFacilityUpgradeCompleteDate:d MMM yyyy}"
            : "";
    }


    private async Task LoadTrophiesAsync()
    {
        if (Team == null) return;

        Trophies.Clear();

        // Use team name as the canonical key — historical records were seeded with the exact name
        // from champions.json and the competition is always tagged. Count by name within competition
        // to avoid double-counting (TeamId OR TeamName could hit the same row twice).

        // 1. League titles (Liga Florilor or NB I depending on the team's competition)
        string leagueComp = Team.CompetitionName ?? "Liga Florilor";
        string leagueLabel = leagueComp switch
        {
            "NB I" => "NB I",
            "Ligue Butagaz Énergie" => "Ligue Butagaz Énergie",
            "Kvindeligaen" => "Kvindeligaen",
            _ => "Liga Florilor"
        };
        string leagueTrophyImg = leagueComp switch
        {
            "Liga Florilor" => "pack://application:,,,/Assets/trophies/ligaflorilor.png",
            _ => "pack://application:,,,/Assets/trophies/placeholdertrophy.png"
        };

        var teamNamesToMatch = new List<string> { Team.Name };
        if (HistoricalNames.TryGetValue(Team.Name, out var pastNames))
        {
            teamNamesToMatch.AddRange(pastNames);
        }

        int leagueTitles = await _db.ChampionRecords
            .CountAsync(r => r.CompetitionName == leagueComp && teamNamesToMatch.Contains(r.TeamName));
        if (leagueTitles > 0)
        {
            Trophies.Add(new TrophyViewModel
            {
                Name = leagueLabel,
                ImagePath = leagueTrophyImg,
                Count = leagueTitles
            });
        }

        // 2. Domestic cup trophies
        if (leagueComp == "Liga Florilor" || leagueComp == "NB I" || leagueComp == "Ligue Butagaz Énergie" || leagueComp == "Kvindeligaen")
        {
            int cupTitles = await _db.CupWinnerRecords
                .CountAsync(r => r.CompetitionName == leagueComp && teamNamesToMatch.Contains(r.TeamName));
            if (cupTitles > 0)
            {
                Trophies.Add(new TrophyViewModel
                {
                    Name = leagueComp switch
                    {
                        "NB I" => "Magyar Kupa",
                        "Ligue Butagaz Énergie" => "Coupe de France",
                        "Kvindeligaen" => "Landspokalturnering",
                        _ => "Cupa României"
                    },
                    ImagePath = "pack://application:,,,/Assets/trophies/placeholdertrophy.png",
                    Count = cupTitles
                });
            }

            if (leagueComp == "Liga Florilor" || leagueComp == "Kvindeligaen")
            {
                int supercupTitles = await _db.SupercupWinnerRecords
                    .CountAsync(r => r.CompetitionName == leagueComp && teamNamesToMatch.Contains(r.TeamName));
                if (supercupTitles > 0)
                {
                    Trophies.Add(new TrophyViewModel
                    {
                        Name = leagueComp == "Kvindeligaen" ? "Bambuni Supercup" : "Supercupa României",
                        ImagePath = "pack://application:,,,/Assets/trophies/placeholdertrophy.png",
                        Count = supercupTitles
                    });
                }
            }
        }
    }
}
