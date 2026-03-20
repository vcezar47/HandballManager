using System.Windows.Controls;
using HandballManager.ViewModels;

namespace HandballManager.Views;

public partial class MatchDetailView : UserControl
{
    public MatchDetailView()
    {
        InitializeComponent();
    }

    private void HomeTeam_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MatchDetailViewModel vm)
        {
            vm.NavigateToHomeTeam();
        }
    }

    private void AwayTeam_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MatchDetailViewModel vm)
        {
            vm.NavigateToAwayTeam();
        }
    }
}
