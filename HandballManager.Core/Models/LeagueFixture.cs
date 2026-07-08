namespace HandballManager.Models;

public class LeagueFixture
{
    public int Id { get; set; }
    public string Season { get; set; } = string.Empty;
    public int Round { get; set; } // Matchweek number
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public bool IsPlayed { get; set; }
    public int? MatchRecordId { get; set; }

    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public string CompetitionName { get; set; } = "Liga Florilor";

    /// <summary>Regular, ChampGroupA, ChampGroupB, Relegation, or KvBo3 (best-of-3 legs).</summary>
    public string Phase { get; set; } = "Regular";

    /// <summary>For Bo3: SF1, SF2, Third, Fin — groups legs of the same tie.</summary>
    public string? PlayoffSeriesId { get; set; }

    /// <summary>Leg 1–3 within a Bo3 series.</summary>
    public int PlayoffLeg { get; set; }

    public MatchRecord? MatchRecord { get; set; }
}
