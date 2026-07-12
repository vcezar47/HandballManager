using HandballManager.Mobile.Session;
using HandballManager.Models;

namespace HandballManager.Mobile;

public partial class RosterPage : ContentPage
{
	public RosterPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.RosterVM;
		await session.RosterVM.InitializeAsync();
	}

	private void OnPlayerSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is Player player
			&& GameSession.Current is { } session)
		{
			session.RosterVM.SelectPlayerCommand.Execute(player);
		}
		PlayersView.SelectedItem = null;
	}
}
