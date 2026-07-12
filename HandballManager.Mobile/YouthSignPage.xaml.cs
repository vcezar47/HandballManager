using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class YouthSignPage : ContentPage
{
	public YouthSignPage(YouthSignViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
