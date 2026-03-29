using System.Text.Json;

namespace HandballManager.Models;

public class Manager
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Name => $"{FirstName} {LastName}".Trim();

    public DateTime Birthdate { get; set; }

    public int Age
    {
        get
        {
            var today = Player.GlobalGameDate;
            var age = today.Year - Birthdate.Year;
            if (Birthdate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    public string PlaceOfBirth { get; set; } = string.Empty;
    public string Nationality { get; set; } = "ROU";

    public CoachLicense License { get; set; } = CoachLicense.Level3;
    public ReputationLevel Reputation { get; set; } = ReputationLevel.National;

    // Attributes (1-20)
    public int Motivation { get; set; } = 10;
    public int YouthDevelopment { get; set; } = 10;
    public int Discipline { get; set; } = 10;
    public int Adaptability { get; set; } = 10;
    public int TimeoutTalks { get; set; } = 10;

    // Career Stats
    public int GamesWon { get; set; }
    public int GamesDrawn { get; set; }
    public int GamesLost { get; set; }
    public int TrophiesWon { get; set; }

    // Club History — serialised as JSON
    public string ClubHistoryJson { get; set; } = "[]";

    private List<ManagerClubHistory>? _clubHistoryCache;
    public List<ManagerClubHistory> ClubHistory
    {
        get
        {
            _clubHistoryCache ??= string.IsNullOrEmpty(ClubHistoryJson)
                ? new List<ManagerClubHistory>()
                : JsonSerializer.Deserialize<List<ManagerClubHistory>>(ClubHistoryJson)
                  ?? new List<ManagerClubHistory>();
            return _clubHistoryCache;
        }
        set
        {
            _clubHistoryCache = value;
            ClubHistoryJson = JsonSerializer.Serialize(value);
        }
    }

    public void SaveClubHistory()
    {
        if (_clubHistoryCache != null)
            ClubHistoryJson = JsonSerializer.Serialize(_clubHistoryCache);
    }

    // Relationships
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public bool IsPlayerManager { get; set; }
}

public class ManagerClubHistory
{
    public string ClubName { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}
