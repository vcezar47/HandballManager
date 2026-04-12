using System.ComponentModel.DataAnnotations.Schema;

namespace HandballManager.Models;

public class MatchPlayerStat
{
    public int Id { get; set; }
    public int MatchRecordId { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TeamId { get; set; }
    
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Saves { get; set; }

    [NotMapped]
    public int Shots { get; set; }

    [NotMapped]
    public int GoalsAgainst { get; set; }

    public double Rating { get; set; }

    public MatchRecord? MatchRecord { get; set; }
    public Player? Player { get; set; }
}
