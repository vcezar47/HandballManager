using CommunityToolkit.Mvvm.Input;

namespace HandballManager.ViewModels;

public partial class MainMenuViewModel : BaseViewModel
{
    private readonly Func<Task> _onNewGame;
    private readonly Func<Task> _onLoadGame;
    private readonly Action _onQuit;

    public MainMenuViewModel(Func<Task> onNewGame, Func<Task> onLoadGame, Action onQuit)
    {
        Title = "Main Menu";
        _onNewGame = onNewGame;
        _onLoadGame = onLoadGame;
        _onQuit = onQuit;
    }

    [RelayCommand]
    private async Task NewGame() => await _onNewGame();

    [RelayCommand]
    private async Task LoadGame() => await _onLoadGame();

    [RelayCommand]
    private void QuitGame() => _onQuit();
}
