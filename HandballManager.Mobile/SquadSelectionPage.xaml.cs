using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class SquadSelectionPage : ContentPage
{
	public SquadSelectionPage(SquadSelectionViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
