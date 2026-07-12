using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class TransfersPage : ContentPage
{
	public TransfersPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.TransfersVM;
		await session.TransfersVM.InitializeAsync();
	}
}
