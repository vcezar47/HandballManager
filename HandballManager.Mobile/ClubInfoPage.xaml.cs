using HandballManager.Mobile.Session;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class ClubInfoPage : ContentPage
{
	/// <summary>True for the Information tab, which resolves the player's own club itself.</summary>
	private readonly bool _isOwnClubTab;

	/// <summary>Tab usage: resolves the player's own club on appearing.</summary>
	public ClubInfoPage()
	{
		InitializeComponent();
		_isOwnClubTab = true;
	}

	/// <summary>Pushed usage: shows a specific club (league table taps, scouting, etc.).</summary>
	public ClubInfoPage(ClubInfoViewModel vm)
	{
		InitializeComponent();
		Apply(vm);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// A pushed page already has its club; only the tab needs to look one up. Refreshed
		// on every visit so budget, facilities and the trophy cabinet stay current.
		if (!_isOwnClubTab) return;
		if (GameSession.Current is not { } session) return;

		var own = await session.CreateOwnClubInfoViewModelAsync();
		if (own != null) Apply(own);
	}

	private void Apply(ClubInfoViewModel vm)
	{
		BindingContext = vm;
		TrophySection.IsVisible = vm.Trophies.Count > 0;
	}
}
