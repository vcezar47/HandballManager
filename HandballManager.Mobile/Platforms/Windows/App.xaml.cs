using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HandballManager.Mobile.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// UI-thread exceptions surface here on WinUI, not on AppDomain.UnhandledException.
		this.UnhandledException += (_, e) =>
		{
			try
			{
				var path = System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "crash.log");
				System.IO.File.AppendAllText(path, $"[{DateTime.Now:O}] WinUI: {e.Exception}\n\n");
			}
			catch
			{
				// never let crash logging crash anything else
			}
		};

		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
