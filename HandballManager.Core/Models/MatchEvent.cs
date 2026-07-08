namespace HandballManager.Models;

public class MatchEvent
{
    public int Id { get; set; }
    public int MatchRecordId { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // Goal, Assist
    public int Minute { get; set; }
    public int Second { get; set; }
    public int TeamId { get; set; }
    public string Description { get; set; } = string.Empty;

    public MatchRecord? MatchRecord { get; set; }
}
