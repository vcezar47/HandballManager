using System.ComponentModel;
using System.Windows;
using HandballManager.ViewModels;

namespace HandballManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Warn about unsaved progress before the window (and app) closes.
        if (DataContext is MainViewModel vm && !vm.ConfirmQuit())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
