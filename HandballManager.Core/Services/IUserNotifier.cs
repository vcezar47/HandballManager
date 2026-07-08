namespace HandballManager.Services;

/// <summary>
/// Platform-neutral way for view models to surface a message to the player.
/// The WPF host shows the app-styled dialog; other platforms show their own.
/// </summary>
public interface IUserNotifier
{
    void Warn(string title, string message);
}
