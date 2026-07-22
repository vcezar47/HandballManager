namespace HandballManager.Services;

public enum GameNotificationKind
{
    Trophy,
    Transfer,
    YouthIntake,
    TransferWindow
}

/// <summary>
/// Logical destinations a toast can deep-link to. The host maps these to its own
/// navigation — Shell routes on mobile, view switches on desktop — because the two
/// front ends do not share a navigation model.
/// </summary>
public static class NotificationRoutes
{
    public const string Transfers = "transfers";
    public const string Youth = "youth";
    public const string News = "news";
    public const string Honours = "honours";
}

/// <summary>
/// A single toast-worthy event. <see cref="Route"/> is one of <see cref="NotificationRoutes"/>,
/// or null when the toast is purely informational.
/// </summary>
public sealed record GameNotification(
    GameNotificationKind Kind,
    string Title,
    string Message,
    string? Route = null);

/// <summary>
/// Non-blocking counterpart to <see cref="IUserNotifier"/>. Game logic posts here
/// and the host renders a toast; nothing waits on the player acknowledging it.
/// Registered as a singleton so any service can post and the shell can listen.
/// </summary>
public sealed class GameNotificationService
{
    public event Action<GameNotification>? Posted;

    public void Post(GameNotification notification) => Posted?.Invoke(notification);

    public void Post(GameNotificationKind kind, string title, string message, string? route = null)
        => Post(new GameNotification(kind, title, message, route));
}
