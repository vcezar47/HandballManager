using HandballManager.Models;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class CupDetailPage : ContentPage
{
	private readonly CupDetailViewModel _vm;

	public CupDetailPage(CupDetailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
		ApplyEmptyStates();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ApplyEmptyStates();
	}

	private async void OnStatsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new StatsPage(_vm.CompetitionName, CompetitionType.Cup));

	private async void OnAwardsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new AwardsPage(_vm.CompetitionName, CompetitionType.Cup));

	private void ApplyEmptyStates()
	{
		// Mirrors the desktop bracket: Hungary has no quarter-finals,
		// Coupe de France has no 3rd-place game.
		QuarterFinalsSection.IsVisible = !_vm.IsHungarianCup;
		ThirdPlaceSection.IsVisible = _vm.IsThirdPlaceGameEnabled;
		QfEmptyLabel.IsVisible = _vm.QuarterFinals.Count == 0;
		SfEmptyLabel.IsVisible = _vm.SemiFinals.Count == 0;
		FinalEmptyLabel.IsVisible = _vm.FinalMatch == null;
	}
}
