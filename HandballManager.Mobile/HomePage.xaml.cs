using HandballManager.Mobile.Session;
using HandballManager.Models;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class HomePage : ContentPage
{
	public HomePage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		BindingContext = session.HomeVM;
		await session.HomeVM.InitializeAsync();
	}

	private void OnResultSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is MatchRecord { IsUnplayedPlaceholder: false, Id: > 0 } record
			&& BindingContext is HomeViewModel vm)
		{
			vm.ViewMatchDetailCommand.Execute(record.Id);
		}
		ResultsView.SelectedItem = null;
	}
}
