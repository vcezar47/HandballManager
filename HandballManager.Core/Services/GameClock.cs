using CommunityToolkit.Mvvm.ComponentModel;

namespace HandballManager.Services;

public partial class GameClock : ObservableObject
{
    [ObservableProperty]
    private DateTime _currentDate;

    public GameClock(DateTime startDate)
    {
        _currentDate = startDate.Date;
        HandballManager.Models.Player.GlobalGameDate = startDate.Date;
    }

    partial void OnCurrentDateChanged(DateTime value)
    {
        HandballManager.Models.Player.GlobalGameDate = value;
    }

    public void AdvanceDay()
    {
        CurrentDate = CurrentDate.AddDays(1).Date;
    }
}

