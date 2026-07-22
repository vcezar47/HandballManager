using HandballManager.Mobile.Session;
using HandballManager.Models;

namespace HandballManager.Mobile;

/// <summary>
/// Top-10 scorers, assists, saves and average rating for one competition — the league,
/// the cup or the supercup, never a mix. Each competition gets its own instance of this
/// page, reached from that competition's own screen.
/// </summary>
public partial class StatsPage : ContentPage
{
    private readonly string _league;
    private readonly CompetitionType _type;

    private int _category;
    private LeagueLeaderboards _boards = LeagueLeaderboards.Empty;

    /// <param name="league">Competition key the clubs belong to (e.g. "Liga Florilor").</param>
    /// <param name="type">Which of that country's competitions these stats cover.</param>
    public StatsPage(string league, CompetitionType type)
    {
        InitializeComponent();
        _league = league;
        _type = type;

        var (name, logo) = CompetitionCatalog.Describe(league, type);
        CompetitionLabel.Text = name;
        CompetitionLogo.Source = MauiImages.Get(logo);
        EmptyLabel.Text = type switch
        {
            CompetitionType.Cup => "No cup matches played yet this season.",
            CompetitionType.Supercup => "The supercup hasn't been played yet this season.",
            _ => "No league matches played yet this season."
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (GameSession.Current is not { } session) return;

        _boards = await session.Leagues.GetLeaderboardsAsync(_league, _type);
        ApplyCategory();
    }

    private void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string p } || !int.TryParse(p, out int c)) return;
        _category = c;
        ApplyCategory();
    }

    private void ApplyCategory()
    {
        var buttons = new[] { CatGoals, CatAssists, CatSaves, CatRating };
        for (int i = 0; i < buttons.Length; i++)
        {
            bool on = i == _category;
            buttons[i].BackgroundColor = on ? Color.FromArgb("#E94560") : Color.FromArgb("#16213E");
            buttons[i].TextColor = on ? Colors.White : Color.FromArgb("#8888AA");
        }

        (RowsView.ItemsSource, StatColumnHeader.Text) = _category switch
        {
            1 => (_boards.TopAssists, "AST"),
            2 => (_boards.TopSaves, "SV%"),
            3 => (_boards.TopRated, "AVG"),
            _ => ((IReadOnlyList<LeaderboardRow>)_boards.TopScorers, "GLS")
        };
    }
}
