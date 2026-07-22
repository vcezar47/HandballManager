using HandballManager.Mobile.Session;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.Mobile;

/// <summary>
/// Team of the Week for one competition — any played matchweek of a league, any played
/// matchday of a cup — plus, for leagues only, the Team of the Season archived at each
/// season end.
/// </summary>
public partial class AwardsPage : ContentPage
{
    private readonly string _league;
    private readonly CompetitionType _type;

    private AwardsService? _awards;
    private bool _showingSeason;

    private List<AwardPeriod> _periods = [];
    private int _periodIndex;

    private List<string> _seasons = [];
    private int _seasonIndex;

    /// <param name="league">Competition key the clubs belong to (e.g. "Liga Florilor").</param>
    /// <param name="type">Which of that country's competitions these awards cover.</param>
    public AwardsPage(string league, CompetitionType type)
    {
        InitializeComponent();
        _league = league;
        _type = type;

        var (name, logo) = CompetitionCatalog.Describe(league, type);
        CompetitionLabel.Text = name;
        CompetitionLogo.Source = MauiImages.Get(logo);

        // A cup has no season-long table to crown a XI from.
        TabSeason.IsVisible = type == CompetitionType.League;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (GameSession.Current is not { } session) return;

        _awards = new AwardsService(session.Db);
        _periods = await _awards.GetAwardPeriodsAsync(_league, _type);
        _seasons = _type == CompetitionType.League
            ? await _awards.GetArchivedSeasonsAsync(_league)
            : [];
        _periodIndex = 0;
        _seasonIndex = 0;

        await RefreshAsync();
    }

    private async void OnWeekTabClicked(object? sender, EventArgs e)
    {
        _showingSeason = false;
        await RefreshAsync();
    }

    private async void OnSeasonTabClicked(object? sender, EventArgs e)
    {
        _showingSeason = true;
        await RefreshAsync();
    }

    private async void OnPreviousClicked(object? sender, EventArgs e) => await StepAsync(+1);

    private async void OnNextClicked(object? sender, EventArgs e) => await StepAsync(-1);

    /// <summary>
    /// Both lists are newest-first, so "‹" (older) moves the index up.
    /// </summary>
    private async Task StepAsync(int delta)
    {
        if (_showingSeason)
        {
            if (_seasons.Count == 0) return;
            _seasonIndex = Math.Clamp(_seasonIndex + delta, 0, _seasons.Count - 1);
        }
        else
        {
            if (_periods.Count == 0) return;
            _periodIndex = Math.Clamp(_periodIndex + delta, 0, _periods.Count - 1);
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_awards == null) return;

        TabWeek.BackgroundColor = _showingSeason ? Color.FromArgb("#16213E") : Color.FromArgb("#E94560");
        TabWeek.TextColor = _showingSeason ? Color.FromArgb("#8888AA") : Colors.White;
        TabSeason.BackgroundColor = _showingSeason ? Color.FromArgb("#E94560") : Color.FromArgb("#16213E");
        TabSeason.TextColor = _showingSeason ? Colors.White : Color.FromArgb("#8888AA");

        if (_showingSeason)
        {
            Title = "TEAM OF THE SEASON";

            if (_seasons.Count == 0)
            {
                PeriodLabel.Text = "No completed seasons yet";
                EmptyLabel.Text = "The Team of the Season is chosen once a season finishes.";
                LineupView.ItemsSource = null;
                return;
            }

            string season = _seasons[_seasonIndex];
            PeriodLabel.Text = $"Season {season}";
            LineupView.ItemsSource = await _awards.GetTeamOfTheSeasonAsync(_league, season);
        }
        else
        {
            Title = "TEAM OF THE WEEK";

            if (_periods.Count == 0)
            {
                PeriodLabel.Text = _type == CompetitionType.League ? "No matchweeks played yet" : "No matches played yet";
                EmptyLabel.Text = _type == CompetitionType.League
                    ? "Play a league matchweek to see its team of the week."
                    : "Play a round of this competition to see its team of the week.";
                LineupView.ItemsSource = null;
                return;
            }

            var period = _periods[_periodIndex];
            PeriodLabel.Text = period.Label;
            LineupView.ItemsSource = await _awards.GetTeamOfThePeriodAsync(_league, _type, period.Key);
        }
    }
}
