using System.Collections.Specialized;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class LiveMatchPage : ContentPage
{
	private readonly LiveMatchViewModel _vm;

	public LiveMatchPage(LiveMatchViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		_vm = vm;
		_vm.EventFeed.CollectionChanged += OnEventFeedChanged;
	}

	// The match must be finished (or skipped) through the on-screen controls;
	// hardware/gesture back would leave an orphaned engine mid-game.
	protected override bool OnBackButtonPressed() => true;

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_vm.EventFeed.CollectionChanged -= OnEventFeedChanged;
	}

	private void OnEventFeedChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// Newest events are inserted at index 0 — keep the feed pinned to the top
		// instead of leaving the user wherever they last scrolled.
		if (e.Action != NotifyCollectionChangedAction.Add) return;
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_vm.EventFeed.Count > 0)
				FeedView.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
		});
	}

	private void OnCourtTabClicked(object? sender, EventArgs e) => SwitchTab(court: true);

	private void OnFeedTabClicked(object? sender, EventArgs e) => SwitchTab(court: false);

	private void SwitchTab(bool court)
	{
		CourtView.IsVisible = court;
		FeedView.IsVisible = !court;
		CourtTabBtn.BackgroundColor = court ? Color.FromArgb("#E94560") : Color.FromArgb("#16213E");
		CourtTabBtn.TextColor = court ? Colors.White : Color.FromArgb("#8888AA");
		FeedTabBtn.BackgroundColor = court ? Color.FromArgb("#16213E") : Color.FromArgb("#E94560");
		FeedTabBtn.TextColor = court ? Color.FromArgb("#8888AA") : Colors.White;
	}
}
