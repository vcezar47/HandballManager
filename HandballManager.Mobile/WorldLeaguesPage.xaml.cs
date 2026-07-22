using System.ComponentModel;
using HandballManager.Mobile.Session;
using HandballManager.Models;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class WorldLeaguesPage : ContentPage
{
	private WorldLeaguesViewModel? _vm;

	public WorldLeaguesPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		if (_vm == null)
		{
			_vm = session.WorldLeaguesVM;
			_vm.PropertyChanged += OnVmPropertyChanged;
			BindingContext = _vm;
		}
		await _vm.InitializeAsync();
		UpdateSupercupCard();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
		_vm = null;
	}

	// The cup and supercup cards below carry their own stats/awards buttons, so these
	// two are about the league itself.
	private async void OnStatsTapped(object? sender, TappedEventArgs e)
	{
		if (_vm == null) return;
		await Navigation.PushAsync(new StatsPage(_vm.SelectedCompetition, CompetitionType.League));
	}

	private async void OnAwardsTapped(object? sender, TappedEventArgs e)
	{
		if (_vm == null) return;
		await Navigation.PushAsync(new AwardsPage(_vm.SelectedCompetition, CompetitionType.League));
	}

	private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// SupercupFixtures is the last property set when a country tab loads.
		if (e.PropertyName == nameof(WorldLeaguesViewModel.SupercupFixtures))
			UpdateSupercupCard();
	}

	private void UpdateSupercupCard()
	{
		if (_vm == null) return;
		SupercupCard.IsVisible = _vm.IsRomanianLeague || _vm.IsDanishLeague;
		if (_vm.IsDanishLeague)
		{
			SupercupTitle.Text = "Bambuni Supercup";
			SupercupLogo.Source = MauiImages.Get("bambunisupercup.png");
		}
		else
		{
			SupercupTitle.Text = "Supercupa României";
			SupercupLogo.Source = MauiImages.Get("supercuparomaniei.png");
		}
	}
}
