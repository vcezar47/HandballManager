using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class NewsPage : ContentPage
{
	public NewsPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.NewsVM;
		await session.NewsVM.InitializeAsync();
	}
}
