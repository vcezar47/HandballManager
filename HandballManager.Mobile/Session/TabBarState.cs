using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Dispatching;

namespace HandballManager.Mobile.Session;

/// <summary>
/// Long-lived (survives across GameSession instances) state for the Shell TabBar's Club tab,
/// refreshed whenever GameSession recomputes its Club badges. AppShell binds to this directly
/// since it's constructed once at app startup, before any GameSession exists.
///
/// The "something's new" signal lives on the tab's Title text, not its Icon: MAUI Shell's native
/// tab bar repaints icons with its own flat platform tint (verified — custom RGB baked into the
/// icon was discarded) and a corner badge shape baked into the icon didn't survive Shell's own
/// image handling either (verified — identical file bytes on disk, but the pixels never rendered
/// in the live tab bar). Title text has neither problem, so a small breathing bullet next to
/// "Club" gives the continuous animated cue instead.
/// </summary>
public partial class TabBarState : ObservableObject
{
    public static TabBarState Instance { get; } = new();

    private static readonly string[] Dots = ["·", "•", "●", "•"];

    [ObservableProperty] private bool _hasNewActivity;
    [ObservableProperty] private bool _isYouthIntakeActive;
    [ObservableProperty] private string _clubTabTitle = "Club";

    private IDispatcherTimer? _timer;
    private int _frame;

    partial void OnHasNewActivityChanged(bool value) => UpdateAnimation();
    partial void OnIsYouthIntakeActiveChanged(bool value) => UpdateAnimation();

    private void UpdateAnimation()
    {
        bool active = HasNewActivity || IsYouthIntakeActive;
        if (active)
        {
            if (_timer != null) return; // already breathing
            _frame = 0;
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(400);
            _timer.Tick += (_, _) => AdvanceFrame();
            AdvanceFrame();
            _timer.Start();
        }
        else if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
            ClubTabTitle = "Club";
        }
    }

    private void AdvanceFrame()
    {
        ClubTabTitle = $"Club {Dots[_frame]}";
        _frame = (_frame + 1) % Dots.Length;
    }

    public void Reset()
    {
        HasNewActivity = false;
        IsYouthIntakeActive = false;
    }
}
