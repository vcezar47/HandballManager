using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class ClubInfoPage : ContentPage
{
	private readonly ClubInfoViewModel _vm;

	public ClubInfoPage(ClubInfoViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
		TrophySection.IsVisible = vm.Trophies.Count > 0;
	}
}
