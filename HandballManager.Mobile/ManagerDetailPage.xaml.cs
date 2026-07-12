using HandballManager.Mobile.Infrastructure;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class ManagerDetailPage : ContentPage
{
	public ManagerDetailPage(ManagerDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		var m = vm.Manager;
		AttributesHost.Content = AttributeGridBuilder.Build(new (string, int)[]
		{
			("Motivation", m.Motivation),
			("Youth Development", m.YouthDevelopment),
			("Discipline", m.Discipline),
			("Adaptability", m.Adaptability),
			("Timeout Talks", m.TimeoutTalks),
		});
	}
}
