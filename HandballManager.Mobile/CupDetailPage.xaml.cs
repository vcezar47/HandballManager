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
