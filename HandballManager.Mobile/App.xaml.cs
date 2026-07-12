namespace HandballManager.Mobile;

public partial class App : Application
{
	public App()
	{
		// Crash breadcrumbs: mobile apps have no console, so keep the last
		// unhandled exception in app-data where it can be retrieved.
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			WriteCrashLog("AppDomain", e.ExceptionObject as Exception);
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			WriteCrashLog("UnobservedTask", e.Exception);
			e.SetObserved();
		};

		InitializeComponent();
	}

	private static void WriteCrashLog(string source, Exception? ex)
	{
		try
		{
			var path = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
			File.AppendAllText(path, $"[{DateTime.Now:O}] {source}: {ex}\n\n");
		}
		catch
		{
			// never let crash logging crash anything else
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
