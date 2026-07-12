using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class MatchDetailPage : ContentPage
{
	public MatchDetailPage(MatchDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
