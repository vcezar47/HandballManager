using HandballManager.Services;
using HandballManager.Views.Dialogs;

namespace HandballManager.Platform;

/// <summary>Routes core-layer notifications to the app-styled dialog.</summary>
public sealed class AppDialogNotifier : IUserNotifier
{
    public void Warn(string title, string message) => AppDialog.Info(title, message);
}
