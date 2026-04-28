namespace HandballManager.Models;

public class CupGroup
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty; // "A", "B", "C", "D"
    public string Season { get; set; } = string.Empty;    // "2025/2026"
    public string CompetitionName { get; set; } = "Liga Florilor"; // "Liga Florilor" or "NB I"

    public List<CupGroupEntry> Entries { get; set; } = [];
    public List<CupFixture> Fixtures { get; set; } = [];
}
