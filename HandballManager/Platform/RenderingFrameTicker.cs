using System.Windows.Media;
using HandballManager.Services;

namespace HandballManager.Platform;

/// <summary>Drives <see cref="IFrameTicker.Tick"/> from WPF's CompositionTarget.Rendering.</summary>
public sealed class RenderingFrameTicker : IFrameTicker
{
    public event EventHandler? Tick;

    private bool _running;

    public void Start()
    {
        if (_running) return;
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e) => Tick?.Invoke(this, e);
}
