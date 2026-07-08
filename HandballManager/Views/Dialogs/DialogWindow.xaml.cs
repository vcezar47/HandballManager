using System.Windows;
using System.Windows.Input;

namespace HandballManager.Views.Dialogs;

public partial class DialogWindow : Window
{
    public DialogWindow(string title, string message, string confirmText, string? cancelText, bool showCancel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (showCancel && cancelText != null)
            CancelButton.Content = cancelText;
        else
            CancelButton.Visibility = Visibility.Collapsed;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}

/// <summary>Static helpers for showing app-styled modal dialogs.</summary>
public static class AppDialog
{
    /// <summary>Yes/No style confirmation. Returns true if the user confirmed.</summary>
    public static bool Confirm(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        var dlg = new DialogWindow(title, message, confirmText, cancelText, showCancel: true)
        {
            Owner = Application.Current?.MainWindow
        };
        return dlg.ShowDialog() == true;
    }

    /// <summary>Single-button informational/error dialog.</summary>
    public static void Info(string title, string message, string okText = "OK")
    {
        var dlg = new DialogWindow(title, message, okText, null, showCancel: false)
        {
            Owner = Application.Current?.MainWindow
        };
        dlg.ShowDialog();
    }
}
