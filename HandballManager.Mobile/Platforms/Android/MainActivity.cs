using Android.App;
using Android.Content.PM;
using Android.OS;

namespace HandballManager.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Paint the status + navigation bars the game's background colour so the notification
        // bar blends seamlessly into the app instead of showing the .NET purple.
        var color = Android.Graphics.Color.ParseColor("#1A1A2E");
        if (Window != null)
        {
            Window.SetStatusBarColor(color);
            Window.SetNavigationBarColor(color);
        }
    }
}
