using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		BindingContext = TabBarState.Instance;
		Navigated += OnShellNavigated;
	}

	/// <summary>
	/// MAUI Shell's native TabBar fires no event at all when the user re-taps the tab they're
	/// already on (verified directly — nothing to hook there), so a deep-pushed page can't be
	/// popped by intercepting that exact gesture. The next best thing: whenever the user genuinely
	/// switches tabs, silently reset every OTHER tab back to its root, so returning to any tab
	/// later always shows its first/initial page instead of wherever it was left mid-navigation.
	/// </summary>
	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		// Entirely best-effort background tidying — nothing here may ever throw past this
		// method, since an uncaught exception in a Shell event handler takes the app down.
		try
		{
			if (e.Source != ShellNavigationSource.ShellSectionChanged) return;

			var gameItem = Items.FirstOrDefault(i => i.Route == "game");
			if (gameItem == null) return;

			var toReset = gameItem.Items
				.Where(s => !ReferenceEquals(s, gameItem.CurrentItem) && s.Navigation.NavigationStack.Count > 1)
				.ToList();
			if (toReset.Count == 0) return;

			// Deferred: popping a background tab's stack synchronously from inside its sibling's
			// Navigated handler can collide with Shell's own in-flight navigation transaction.
			Dispatcher.Dispatch(async () =>
			{
				foreach (var section in toReset)
				{
					try { await section.Navigation.PopToRootAsync(animated: false); }
					catch { /* best-effort background cleanup */ }
				}
			});
		}
		catch { /* best-effort background cleanup */ }
	}
}
