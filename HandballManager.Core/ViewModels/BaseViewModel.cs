using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.ViewModels;

/// <summary>Base ViewModel providing INotifyPropertyChanged via CommunityToolkit.Mvvm.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;
}
