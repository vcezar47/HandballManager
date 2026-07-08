namespace HandballManager.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal TransferBudget { get; set; }
    public decimal WageBudget { get; set; }
    public decimal ClubBalance { get; set; }
    public int FoundedYear { get; set; }
    public string StadiumName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPlayerTeam { get; set; }
    public string LogoPath { get; set; } = string.Empty;
    public string Nation { get; set; } = string.Empty;
    public string CompetitionName { get; set; } = "Liga Florilor";

    public Manager? Manager { get; set; }
    public ReputationLevel ClubReputation { get; set; } = ReputationLevel.Local;
    public int StadiumCapacity { get; set; } = 2000;
    public string StadiumImage { get; set; } = string.Empty;

    // ── Club Facilities ────────────────────────────────────────────────────────
    /// <summary>Training Facilities level (0 = Low standard … 6 = Fantastic).</summary>
    public int TrainingFacilityLevel { get; set; } = 2;

    /// <summary>Youth Academy level (0 = Low standard … 6 = Fantastic).</summary>
    public int YouthFacilityLevel { get; set; } = 2;

    /// <summary>
    /// When set, a training facility upgrade is in progress and will complete on this date.
    /// Null = no upgrade in progress.
    /// </summary>
    public DateTime? TrainingFacilityUpgradeCompleteDate { get; set; }

    /// <summary>
    /// When set, a youth academy upgrade is in progress and will complete on this date.
    /// Null = no upgrade in progress.
    /// </summary>
    public DateTime? YouthFacilityUpgradeCompleteDate { get; set; }

    public List<Player> Players { get; set; } = [];
    public LeagueEntry? LeagueEntry { get; set; }
    public List<Transaction> Transactions { get; set; } = [];
}
