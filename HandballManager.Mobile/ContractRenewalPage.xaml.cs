using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class ContractRenewalPage : ContentPage
{
	public ContractRenewalPage(ContractRenewalViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
