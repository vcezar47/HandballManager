using HandballManager.Services;

namespace HandballManager.Mobile.Infrastructure;

/// <summary>
/// Drives <see cref="IFrameTicker.Tick"/> from a MAUI dispatcher timer (~30 fps).
/// Counterpart of the WPF RenderingFrameTicker for the live-match clock.
/// </summary>
public sealed class DispatcherFrameTicker : IFrameTicker
{
    public event EventHandler? Tick;

    private IDispatcherTimer? _timer;

    public void Start()
    {
        if (_timer != null) return;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.GetForCurrentThread();
        if (dispatcher == null) return;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
    }

    private void OnTimerTick(object? sender, EventArgs e) => Tick?.Invoke(this, e);
}
