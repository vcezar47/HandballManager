using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class YouthPage : ContentPage
{
	public YouthPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.YouthVM;
		await session.YouthVM.InitializeAsync();
	}
}
