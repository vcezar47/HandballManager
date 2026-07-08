namespace HandballManager.Models;

public class SupercupWinnerRecord
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int? TeamId { get; set; }

    /// <summary>Source league for this supercup, e.g. Liga Florilor or Kvindeligaen.</summary>
    public string CompetitionName { get; set; } = "Liga Florilor";
}
