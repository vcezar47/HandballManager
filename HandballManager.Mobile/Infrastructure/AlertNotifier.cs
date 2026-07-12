using HandballManager.Services;

namespace HandballManager.Mobile.Infrastructure;

/// <summary>Surfaces core-layer notifications as platform alerts.</summary>
public sealed class AlertNotifier : IUserNotifier
{
    public void Warn(string title, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            _ = page?.DisplayAlertAsync(title, message, "OK");
        });
    }
}
