using HandballManager.Data;
using HandballManager.Mobile.Session;
using Microsoft.Extensions.Logging;

namespace HandballManager.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// The working database must live in the app's writable data directory
		// (the install package itself is read-only on Android/iOS).
		HandballDbContext.DatabaseDirectory = FileSystem.AppDataDirectory;

		// Carry a pre-slots career (single handball.db) into slot 1 on first launch of this version.
		SaveSlots.MigrateLegacyIfNeeded();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
