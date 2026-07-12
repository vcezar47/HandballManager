namespace HandballManager.Mobile;

public partial class LoadingPage : ContentPage
{
	private bool _navigated;

	public LoadingPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_navigated) return;
		_navigated = true;

		// Fill the bar, then hand off to the main menu.
		await Bar.ProgressTo(1.0, 1600, Easing.CubicInOut);
		await Shell.Current.GoToAsync("//menu");
	}
}
