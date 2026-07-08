namespace HandballManager.Services;

/// <summary>
/// Per-frame callback source driving the live-match clock. The WPF host implements this
/// with CompositionTarget.Rendering; other platforms supply their own render/timer loop.
/// </summary>
public interface IFrameTicker
{
    event EventHandler? Tick;
    void Start();
    void Stop();
}
