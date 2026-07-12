using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class SupercupDetailPage : ContentPage
{
	public SupercupDetailPage(SupercupDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
