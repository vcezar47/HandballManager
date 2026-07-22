using HandballManager.Models;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class SupercupDetailPage : ContentPage
{
	private readonly SupercupDetailViewModel _vm;

	public SupercupDetailPage(SupercupDetailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	private async void OnStatsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new StatsPage(_vm.ActiveCompetition, CompetitionType.Supercup));

	private async void OnAwardsClicked(object? sender, EventArgs e)
		=> await Navigation.PushAsync(new AwardsPage(_vm.ActiveCompetition, CompetitionType.Supercup));
}
