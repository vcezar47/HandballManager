namespace HandballManager.Models;

/// <summary>
/// A transfer agreed outside the window or as free agent (effective when window opens or contract expires).
/// </summary>
public class PendingTransfer
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int FromTeamId { get; set; }
    public int ToTeamId { get; set; }
    public decimal AgreedMonthlyWage { get; set; }
    public DateTime ContractEndDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string TransferType { get; set; } = "Buyout"; // "Buyout" or "FreeContract"
    public DateTime CreatedAt { get; set; }

    public Player? Player { get; set; }
    public Team? FromTeam { get; set; }
    public Team? ToTeam { get; set; }
}
