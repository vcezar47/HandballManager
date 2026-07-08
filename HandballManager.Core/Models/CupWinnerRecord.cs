namespace HandballManager.Models;

public class CupWinnerRecord
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string CompetitionName { get; set; } = "Liga Florilor"; // "Liga Florilor" or "NB I"
}
