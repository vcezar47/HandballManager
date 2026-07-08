using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace HandballManager.ViewModels;

public partial class MainMenuViewModel : BaseViewModel
{
    private readonly Func<Task> _onNewGame;
    private readonly Func<Task> _onLoadGame;

    public MainMenuViewModel(Func<Task> onNewGame, Func<Task> onLoadGame)
    {
        Title = "Main Menu";
        _onNewGame = onNewGame;
        _onLoadGame = onLoadGame;
    }

    [RelayCommand]
    private async Task NewGame() => await _onNewGame();

    [RelayCommand]
    private async Task LoadGame() => await _onLoadGame();

    [RelayCommand]
    private void QuitGame()
    {
        if (Application.Current?.MainWindow != null)
        {
            Application.Current.MainWindow.Close();
        }
    }
}
