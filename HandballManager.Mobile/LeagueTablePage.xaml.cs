using HandballManager.Mobile.Session;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.Mobile;

/// <summary>
/// Standings for one competition, plus the entry points to that competition's player
/// stats, awards and honours. Reached from the Comps tab, which is why the competition
/// is passed in rather than assumed to be the player's own.
/// </summary>
public partial class LeagueTablePage : ContentPage
{
	private readonly string? _competitionOverride;

	private string _competition = "";

	/// <summary>Defaults to the player's own league (used when no competition is supplied).</summary>
	public LeagueTablePage()
	{
		InitializeComponent();
	}

	public LeagueTablePage(string competition)
	{
		InitializeComponent();
		_competitionOverride = competition;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		// Standings change as days advance, so refresh the header + table on every visit.
		BindingContext = session.CompetitionsVM;
		await session.CompetitionsVM.InitializeAsync();

		_competition = _competitionOverride ?? session.CompetitionsVM.CompetitionName;
		CompetitionTitle.Text = _competition;

		await LoadStandingsAsync(_competition);
	}

	private async void OnStatsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new StatsPage(_competition, CompetitionType.League));

	private async void OnAwardsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new AwardsPage(_competition, CompetitionType.League));

	private async void OnHistoryClicked(object? sender, EventArgs e)
	{
		if (GameSession.Current is { } session)
			await session.OpenLeagueHistoryAsync(_competition);
	}

	/// <summary>
	/// Goes through the session's LeagueService rather than opening a context of its own,
	/// so this page and the Comps tab read the same reconciled table.
	/// </summary>
	private async Task LoadStandingsAsync(string competition)
	{
		if (GameSession.Current is not { } session) return;

		var entries = competition == LeagueService.KvindeligaenCompetition
			? await session.Leagues.GetKvindeligaenComputedRegularStandingsAsync()
			: await session.Leagues.GetStandingsAsync(competition);

		StandingsView.ItemsSource = entries
			.Select((x, i) => new StandingRow(i + 1, x.TeamId, x.Team?.Name ?? "?", x.Team?.LogoPath ?? "",
				x.Played, x.GoalDifference, x.Points))
			.ToList();
	}

	private async void OnStandingSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is StandingRow row
			&& GameSession.Current is { } session)
		{
			await session.OpenClubInfoByIdAsync(row.TeamId);
		}
		StandingsView.SelectedItem = null;
	}
}

public record StandingRow(int Rank, int TeamId, string Team, string LogoPath, int Played, int GoalDifference, int Points)
{
	public string GdText => GoalDifference > 0 ? $"+{GoalDifference}" : GoalDifference.ToString();
}
