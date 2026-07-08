using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Models;

namespace HandballManager.ViewModels;

public partial class ManagerDetailViewModel : BaseViewModel
{
    [ObservableProperty]
    private Manager _manager;

    public string FullName => Manager.Name;
    public string LicenseDisplay => Manager.License.DisplayName();
    public string ReputationDisplay => Manager.Reputation.ToString();

    public ManagerDetailViewModel(Manager manager)
    {
        Title = "Manager Profile";
        _manager = manager;
    }
}
