namespace HandballManager.Models;

public class SupercupFixture
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;

    public int HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }
    public int AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }

    public DateTime ScheduledDate { get; set; }
    public bool IsPlayed { get; set; }

    /// <summary>"SemiFinal", "ThirdPlace", "Final"</summary>
    public string Round { get; set; } = "SemiFinal";

    /// <summary>Neutral venue for Final Four fixtures.</summary>
    public string? VenueName { get; set; }

    /// <summary>Links to the full match record for the match detail view.</summary>
    public int? MatchRecordId { get; set; }
    public MatchRecord? MatchRecord { get; set; }

    /// <summary>Display text for scores in the knockout bracket.</summary>
    public string ScoreDisplay => IsPlayed ? $"{HomeGoals} : {AwayGoals}" : "vs";
}
