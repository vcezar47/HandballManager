using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.Mobile.Session;

/// <summary>
/// Mirrors the desktop MainViewModel's nav badges (pending transfer offers, unread news,
/// unsigned youth intake prospects) for the mobile Club tab hub.
/// </summary>
public partial class ClubBadgeCounts : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(HasTransfers))]
    [NotifyPropertyChangedFor(nameof(HasAny))]
    private int _transfersCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(HasNews))]
    [NotifyPropertyChangedFor(nameof(HasAny))]
    private int _newsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(HasYouth))]
    [NotifyPropertyChangedFor(nameof(HasAny))]
    private int _youthCount;

    public int TotalCount => TransfersCount + NewsCount + YouthCount;
    public bool HasTransfers => TransfersCount > 0;
    public bool HasNews => NewsCount > 0;
    public bool HasYouth => YouthCount > 0;
    public bool HasAny => TotalCount > 0;
}
