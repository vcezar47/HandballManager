using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class ContractsPage : ContentPage
{
	public ContractsPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.ContractsVM;
		await session.ContractsVM.InitializeAsync();
	}
}
