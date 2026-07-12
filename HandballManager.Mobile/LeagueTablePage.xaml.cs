using HandballManager.Data;
using HandballManager.Mobile.Session;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Mobile;

public partial class LeagueTablePage : ContentPage
{
	public LeagueTablePage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		// Standings change as days advance, so refresh the header + table on every visit.
		BindingContext = session.CompetitionsVM;
		await session.CompetitionsVM.InitializeAsync();
		await LoadStandingsAsync(session.CompetitionsVM.CompetitionName);
	}

	private async void OnHistoryClicked(object? sender, EventArgs e)
	{
		if (GameSession.Current is { } session)
			await session.OpenLeagueHistoryAsync(session.CompetitionsVM.CompetitionName);
	}

	private async Task LoadStandingsAsync(string competition)
	{
		var rows = await Task.Run(() =>
		{
			using var db = new HandballDbContext();
			return db.LeagueEntries
				.Include(x => x.Team)
				.Where(x => x.CompetitionName == competition)
				.AsEnumerable()
				.OrderByDescending(x => x.Points)
				.ThenByDescending(x => x.GoalDifference)
				.ThenByDescending(x => x.GoalsFor)
				.ThenBy(x => x.Team!.Name)
				.Select((x, i) => new StandingRow(i + 1, x.TeamId, x.Team?.Name ?? "?", x.Team?.LogoPath ?? "", x.Played, x.GoalDifference, x.Points))
				.ToList();
		});

		StandingsView.ItemsSource = rows;
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
