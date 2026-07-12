using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class SetupPage : ContentPage
{
	public SetupPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		BindingContext = GameSession.Current?.StartVM;
	}
}
