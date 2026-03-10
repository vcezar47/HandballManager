namespace HandballManager.Models;

/// <summary>
/// An offer from another club to sign one of our players (shown in Transfers tab).
/// </summary>
public class TransferOffer
{
    public int Id { get; set; }
    public int FromTeamId { get; set; }
    public int ForPlayerId { get; set; }
    public string OfferType { get; set; } = "Buyout"; // "Buyout" or "FreeContract"
    public decimal OfferAmount { get; set; }
    public decimal ProposedMonthlyWage { get; set; }
    public int ProposedContractYears { get; set; }
    public DateTime OfferedAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

    public Team? FromTeam { get; set; }
    public Player? ForPlayer { get; set; }
}
