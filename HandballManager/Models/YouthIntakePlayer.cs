namespace HandballManager.Models;

/// <summary>
/// A youth player offered to a club on March 20. Stored as JSON snapshot; when signed, a real Player is created.
/// </summary>
public class YouthIntakePlayer
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public int IntakeYear { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime Birthdate { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Nationality { get; set; } = "ROU";
    public int Height { get; set; }
    public int Weight { get; set; }
    /// <summary>JSON-serialized full player attributes. When signing we deserialize and create Player.</summary>
    public string PlayerDataJson { get; set; } = "{}";
    public int SuggestedShirtNumber { get; set; }

    public Team? Club { get; set; }

    public string Name => $"{FirstName} {LastName}".Trim();
}
