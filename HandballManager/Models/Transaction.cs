using System;

namespace HandballManager.Models;

public class Transaction
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // e.g., "PrizeMoney", "Transfer"

    public Team? Team { get; set; }
}
