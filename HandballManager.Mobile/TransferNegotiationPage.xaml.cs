using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class TransferNegotiationPage : ContentPage
{
	public TransferNegotiationPage(TransferNegotiationViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
