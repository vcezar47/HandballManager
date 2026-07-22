using System.ComponentModel;
using HandballManager.Mobile.Session;
using HandballManager.Models;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class HomePage : ContentPage
{
	private HomeViewModel? _vm;

	public HomePage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (GameSession.Current is not { } session) return;

		if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
		_vm = session.HomeVM;
		_vm.PropertyChanged += OnVmPropertyChanged;

		BindingContext = _vm;
		Toasts.Attach(session.Notifications, session.OpenNotificationRoute);
		ApplyAdvanceLock();
		await _vm.InitializeAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		Toasts.Detach();
		if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
		_vm = null;
	}

	private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(HomeViewModel.IsAutoAdvancing)
			or nameof(HomeViewModel.IsRollingOverSeason))
			ApplyAdvanceLock();
	}

	/// <summary>
	/// The in-page overlay cannot cover the Shell tab bar, so while days are rolling —
	/// or the season is turning over — the tab bar is hidden outright. During continuous
	/// advance that leaves STOP as the only thing to tap; during the rollover it keeps
	/// another screen from touching the database mid-operation.
	/// </summary>
	private void ApplyAdvanceLock()
		=> Shell.SetTabBarIsVisible(this,
			_vm is not { IsAutoAdvancing: true } and not { IsRollingOverSeason: true });

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
