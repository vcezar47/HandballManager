using System.Windows;
using HandballManager.ViewModels;

namespace HandballManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // The shell opens on the main menu. The working database is only created/
            // seeded when the player chooses New Game, and replaced when they Load.
            var mainVm = new MainViewModel();
            var mainWindow = new MainWindow { DataContext = mainVm };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += $"\n\nInner: {ex.InnerException.Message}";

            MessageBox.Show($"Startup Error: {msg}\n\n{ex.StackTrace}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
