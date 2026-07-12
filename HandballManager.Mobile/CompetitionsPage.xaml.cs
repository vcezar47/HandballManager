using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class CompetitionsPage : ContentPage
{
	public CompetitionsPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.CompetitionsVM;
		await session.CompetitionsVM.InitializeAsync();

		// Only Romania and Denmark run a supercup; the card mirrors the desktop split view.
		var vm = session.CompetitionsVM;
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
