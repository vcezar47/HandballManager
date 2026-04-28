using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using HandballManager.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace HandballManager.ViewModels;

public partial class WorldLeaguesViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly LeagueService _leagueService;
    private readonly CupService _cupService;
    private readonly SupercupService _supercupService;
    private readonly Action<Team>? _onTeamSelected;
    private readonly Func<string, Task>? _onNavigateToLeagueDetail;
    private readonly Func<string, Task>? _onNavigateToLeagueHistory;
    private readonly Func<string, Task>? _onNavigateToCupDetail;
    private readonly Func<Task>? _onNavigateToSupercupDetail;

    // ── Country tab selection ──────────────────────────────────────────────
    [ObservableProperty]
    private string _selectedCompetition = "Liga Florilor";

    public List<CompetitionInfo> AvailableCompetitions { get; } = new()
    {
        new CompetitionInfo { Name = "Liga Florilor", Country = "Romania", Flag = "/Assets/flags/romania.png" },
        new CompetitionInfo { Name = "NB I",          Country = "Hungary",  Flag = "/Assets/flags/hungary.png"  }
    };

    // ── League table ───────────────────────────────────────────────────────
    [ObservableProperty] private List<LeagueEntry> _standings = [];
    [ObservableProperty] private string _leagueLogoPath = "/Assets/leaguelogo/ligaflorilor.png";
    [ObservableProperty] private string _leagueDisplayName = "Liga Florilor";

    // ── Romanian cups (right panel) ────────────────────────────────────────
    [ObservableProperty] private bool _isRomanianLeague;
    [ObservableProperty] private bool _isHungarianLeague;
    [ObservableProperty] private CupGroup? _playerCupGroup;
    [ObservableProperty] private string _cupGroupTitle = "Cupa României";
    [ObservableProperty] private string _cupLogoPath = "/Assets/leaguelogo/cuparomaniei.png";
    [ObservableProperty] private List<SupercupFixture> _supercupFixtures = [];

    public WorldLeaguesViewModel(
        HandballDbContext db,
        LeagueService leagueService,
        CupService cupService,
        SupercupService supercupService,
        Action<Team>? onTeamSelected = null,
        Func<string, Task>? onNavigateToLeagueDetail = null,
        Func<string, Task>? onNavigateToLeagueHistory = null,
        Func<string, Task>? onNavigateToCupDetail = null,
        Func<Task>? onNavigateToSupercupDetail = null)
    {
        Title = "World Leagues";
        _db = db;
        _leagueService = leagueService;
        _cupService = cupService;
        _supercupService = supercupService;
        _onTeamSelected = onTeamSelected;
        _onNavigateToLeagueDetail = onNavigateToLeagueDetail;
        _onNavigateToLeagueHistory = onNavigateToLeagueHistory;
        _onNavigateToCupDetail = onNavigateToCupDetail;
        _onNavigateToSupercupDetail = onNavigateToSupercupDetail;
    }

    public async Task InitializeAsync()
    {
        // Default to Romania tab so the country with the most competitions is visible
        await LoadCountryAsync(SelectedCompetition);
    }

    private async Task LoadCountryAsync(string competitionName)
    {
        IsRomanianLeague = competitionName == "Liga Florilor";
        IsHungarianLeague = competitionName == "NB I";

        LeagueLogoPath = competitionName == "NB I"
            ? "/Assets/leaguelogo/nbi.png"
            : "/Assets/leaguelogo/ligaflorilor.png";

        CupLogoPath = competitionName == "NB I"
            ? "/Assets/leaguelogo/magyarkupa.png"
            : "/Assets/leaguelogo/cuparomaniei.png";

        LeagueDisplayName = competitionName == "NB I" ? "NB I" : "Liga Florilor";

        Standings = await _leagueService.GetStandingsAsync(competitionName);

        // Load cup data for both countries
        PlayerCupGroup = await _cupService.GetPlayerTeamGroupAsync(competitionName);
        if (PlayerCupGroup == null)
        {
            var allGroups = await _cupService.GetAllGroupsAsync(competitionName);
            PlayerCupGroup = allGroups.FirstOrDefault();
        }

        if (competitionName == "NB I")
        {
            CupGroupTitle = PlayerCupGroup != null
                ? $"Magyar Kupa — Group {PlayerCupGroup.GroupName}"
                : "Magyar Kupa";
            SupercupFixtures = [];
        }
        else
        {
            CupGroupTitle = PlayerCupGroup != null
                ? $"Cupa României — Group {PlayerCupGroup.GroupName}"
                : "Cupa României";

            var knockout = await _supercupService.GetKnockoutFixturesAsync();
            if (knockout.Any(f => f.Round == "Final" || f.Round == "ThirdPlace"))
                SupercupFixtures = knockout.Where(f => f.Round == "Final" || f.Round == "ThirdPlace").ToList();
            else
                SupercupFixtures = knockout.Where(f => f.Round == "SemiFinal").ToList();
        }
    }

    partial void OnSelectedCompetitionChanged(string value) => _ = LoadCountryAsync(value);

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectCompetition(string competitionName) => SelectedCompetition = competitionName;

    [RelayCommand]
    private void ViewTeam(LeagueEntry? entry) => _onTeamSelected?.Invoke(entry?.Team!);

    [RelayCommand]
    private void ViewCupTeam(CupGroupEntry? entry) => _onTeamSelected?.Invoke(entry?.Team!);

    [RelayCommand]
    private async Task ViewLeagueDetail()
    {
        if (_onNavigateToLeagueDetail != null) await _onNavigateToLeagueDetail(SelectedCompetition);
    }

    [RelayCommand]
    private async Task ViewLeagueHistory()
    {
        if (_onNavigateToLeagueHistory != null) await _onNavigateToLeagueHistory(SelectedCompetition);
    }

    [RelayCommand]
    private async Task ViewCupDetail()
    {
        if (_onNavigateToCupDetail != null) await _onNavigateToCupDetail(SelectedCompetition);
    }

    [RelayCommand]
    private async Task ViewSupercupDetail()
    {
        if (_onNavigateToSupercupDetail != null) await _onNavigateToSupercupDetail();
    }
}

public class CompetitionInfo
{
    public string Name    { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Flag    { get; set; } = string.Empty;
}
