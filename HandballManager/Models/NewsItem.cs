namespace HandballManager.Models;

public class NewsItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string NewsType { get; set; } = "General"; // RetirementAnnouncement, Transfer, YouthIntake, General
    public bool IsRead { get; set; } = false;
}