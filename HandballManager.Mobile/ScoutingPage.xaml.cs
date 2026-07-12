using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class ScoutingPage : ContentPage
{
	public ScoutingPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.ScoutingVM;
		await session.ScoutingVM.InitializeAsync();
	}
}
