using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public record HonourRow(string Season, string TeamName, string? Detail)
{
	public bool HasDetail => !string.IsNullOrEmpty(Detail);
}

/// <summary>
/// Shared past-winners page backing the league, cup, and supercup history screens
/// (desktop LeagueHistoryView / CupHistoryView / SupercupHistoryView).
/// </summary>
public partial class HonoursPage : ContentPage
{
	public HonoursPage(string kicker, string title, IEnumerable<HonourRow> rollOfHonour, IEnumerable<TeamTitleSummary> medalTable)
	{
		InitializeComponent();
		KickerLabel.Text = kicker;
		TitleLabel.Text = title;
		BindableLayout.SetItemsSource(RollHost, rollOfHonour.ToList());
		BindableLayout.SetItemsSource(MedalHost, medalTable.ToList());
	}
}
