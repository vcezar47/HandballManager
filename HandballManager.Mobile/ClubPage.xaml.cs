using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class ClubPage : ContentPage
{
	public ClubPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.Badges;
		bool hadAny = session.Badges.HasAny;
		await session.RefreshBadgesAsync();

		if (session.Badges.HasAny && !hadAny)
		{
			// "Jump up" cue: a small bounce when fresh activity appears.
			TotalBadge.Scale = 0.6;
			TotalBadge.Opacity = 0;
			await Task.WhenAll(
				TotalBadge.ScaleToAsync(1.0, 220, Easing.SpringOut),
				TotalBadge.FadeToAsync(1.0, 150));
		}
	}

	private async void OnInformationTapped(object? sender, TappedEventArgs e)
	{
		if (GameSession.Current is { } session) await session.OpenOwnClubInfoAsync();
	}

	private async void OnTransfersTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new TransfersPage());

	private async void OnYouthTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new YouthPage());

	private async void OnFinancesTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new FinancesPage());

	private async void OnScoutingTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new ScoutingPage());

	private async void OnContractsTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new ContractsPage());

	private async void OnNewsTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new NewsPage());
}
