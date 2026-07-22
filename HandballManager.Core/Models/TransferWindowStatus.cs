namespace HandballManager.Models;

public enum TransferWindowKind
{
    None,
    Summer,
    Winter
}

/// <summary>Drives the colour of the home-screen transfer window badge.</summary>
public enum TransferWindowUrgency
{
    None,
    /// <summary>More than a week to go.</summary>
    Open,
    /// <summary>Seven days or fewer.</summary>
    ClosingSoon,
    /// <summary>Closes tonight.</summary>
    LastDay
}

/// <summary>
/// Snapshot of the transfer window on a given day: which one is open, and how
/// many days of it are left. <see cref="DaysRemaining"/> counts the current day,
/// so the final day of a window reads as 1.
/// </summary>
public readonly record struct TransferWindowStatus(TransferWindowKind Kind, DateTime Start, DateTime End, DateTime Today)
{
    public static TransferWindowStatus Closed(DateTime today)
        => new(TransferWindowKind.None, default, default, today.Date);

    public bool IsOpen => Kind != TransferWindowKind.None;

    /// <summary>True on the day the window opened.</summary>
    public bool IsOpeningDay => IsOpen && Today.Date == Start.Date;

    /// <summary>True on the last day the window is open.</summary>
    public bool IsFinalDay => IsOpen && Today.Date == End.Date;

    public int DaysRemaining => IsOpen ? (End.Date - Today.Date).Days + 1 : 0;

    /// <summary>
    /// How urgent the window is, for colour coding: green with time to spare,
    /// amber inside the last week, red on the closing day.
    /// </summary>
    public TransferWindowUrgency Urgency => !IsOpen
        ? TransferWindowUrgency.None
        : DaysRemaining <= 1 ? TransferWindowUrgency.LastDay
        : DaysRemaining <= 7 ? TransferWindowUrgency.ClosingSoon
        : TransferWindowUrgency.Open;

    public string Name => Kind switch
    {
        TransferWindowKind.Summer => "Summer transfer window",
        TransferWindowKind.Winter => "Winter transfer window",
        _ => "Transfer window closed"
    };

    /// <summary>Compact text for the home screen badge.</summary>
    public string BadgeText => Kind switch
    {
        TransferWindowKind.None => string.Empty,
        _ => DaysRemaining == 1
            ? $"{ShortName} window · final day"
            : $"{ShortName} window · {DaysRemaining} days left"
    };

    private string ShortName => Kind == TransferWindowKind.Summer ? "Summer" : "Winter";
}
