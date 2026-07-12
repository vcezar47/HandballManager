using HandballManager.Mobile.Session;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.Mobile;

public partial class FinancesPage : ContentPage
{
	public FinancesPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		var team = await session.Db.Teams.FirstOrDefaultAsync(t => t.IsPlayerTeam);
		if (team == null) return;

		session.FinancesVM.Initialize(team);
		BindingContext = session.FinancesVM;
	}
}
