namespace HandballManager.Models;

public class LiveMatchEvent
{
    public int Minute { get; set; }
    public int Second { get; set; }
    public string EventType { get; set; } = string.Empty; 
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public int? AssistPlayerId { get; set; }
    public string? AssistPlayerName { get; set; }
    public int TeamId { get; set; }
    public string Description { get; set; } = string.Empty;
}
