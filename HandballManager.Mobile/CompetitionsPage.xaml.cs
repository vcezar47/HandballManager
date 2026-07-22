using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class CompetitionsPage : ContentPage
{
	public CompetitionsPage()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Reloads on every arrival. Shell raises this for tab switches and for pops back to
	/// the tab root, where OnAppearing is unreliable on Android — the tables used to sit
	/// on whatever the standings were the first time the tab was opened.
	/// </summary>
	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		await RefreshAsync();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		if (GameSession.Current is not { } session) return;

		var vm = session.CompetitionsVM;
		BindingContext = vm;
		await vm.InitializeAsync();

		// Only Romania and Denmark run a supercup; the card mirrors the desktop split view.
		SupercupCard.IsVisible = vm.IsRomanianLeague || vm.IsDanishLeague;
		if (vm.IsDanishLeague)
		{
			SupercupTitle.Text = "Bambuni Supercup";
			SupercupLogo.Source = MauiImages.Get("bambunisupercup.png");
		}
		else if (vm.IsRomanianLeague)
		{
			SupercupTitle.Text = "Supercupa României";
			SupercupLogo.Source = MauiImages.Get("supercuparomaniei.png");
		}
	}

	private async void OnWorldLeaguesTapped(object? sender, TappedEventArgs e)
		=> await Navigation.PushAsync(new WorldLeaguesPage());
}
