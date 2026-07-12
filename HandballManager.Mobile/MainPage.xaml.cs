using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		// LOAD GAME is only tappable when any of the three slots holds a career.
		bool hasSave = await SaveSlots.AnyOccupiedAsync();
		ContinueBtn.IsEnabled = hasSave;
		ContinueBtn.Opacity = hasSave ? 1.0 : 0.4;
	}

	private async void OnNewGameClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new SaveSlotsPage(SaveSlotsPage.Mode.New));

	private async void OnContinueClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new SaveSlotsPage(SaveSlotsPage.Mode.Load));
}
