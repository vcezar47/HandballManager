using System.Windows;
using HandballManager.Data;
using HandballManager.Services;
using HandballManager.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HandballManager;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Bootstrap DB
            var db = new HandballDbContext();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            DatabaseSeeder.Seed(db);

            var teamCount = await db.Teams.CountAsync();
            // Optional: Uncomment for debugging
            // MessageBox.Show($"Database initialized on {db.DbPath}. Loaded {teamCount} teams.");

            // Build services
            var leagueService = new LeagueService(db);
            var progressionService = new PlayerProgressionService();
            var transferService = new TransferService(db);
            var youthIntakeService = new YouthIntakeService(db);
            var cupService = new CupService(db);
            var simulationEngine = new SimulationEngine(db, progressionService, transferService, youthIntakeService, cupService);
            var clock = new GameClock(LeagueService.GameSeasonStartDate);
            var scoutingService = new ScoutingService(clock);

            // Generate initial cup draw
            await cupService.GenerateCupAsync();

            // Build main VM and window
            var mainVm = new MainViewModel(db, leagueService, simulationEngine, clock, scoutingService, transferService, youthIntakeService, cupService);
            await mainVm.InitializeAsync();

            var mainWindow = new MainWindow { DataContext = mainVm };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
