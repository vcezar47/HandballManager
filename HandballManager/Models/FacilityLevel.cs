namespace HandballManager.Models;

/// <summary>
/// Static data for facility levels — labels, upgrade costs, effect multipliers.
/// Levels are 0 (Low standard) through 6 (Fantastic).
/// </summary>
public static class FacilityLevel
{
    public const int MinLevel = 0;
    public const int MaxLevel = 6;

    public static readonly string[] Labels =
    [
        "Low standard",
        "Below average",
        "Average",
        "Adequate",
        "Modern",
        "High standard",
        "Fantastic"
    ];

    /// <summary>Cost to upgrade Training Facilities from level[i] to level[i+1].</summary>
    public static readonly decimal[] TrainingUpgradeCosts =
    [
        30_000m,
        75_000m,
        150_000m,
        300_000m,
        600_000m,
        1_200_000m
    ];

    /// <summary>Cost to upgrade Youth Academy from level[i] to level[i+1].</summary>
    public static readonly decimal[] YouthUpgradeCosts =
    [
        20_000m,
        50_000m,
        100_000m,
        200_000m,
        500_000m,
        900_000m
    ];

    /// <summary>
    /// Training facility multiplier applied to the daily player progression rate.
    /// Index = facility level (0–6).
    /// </summary>
    public static readonly double[] TrainingMultipliers =
    [
        0.80,   // 0 – Low standard
        0.88,   // 1 – Below average
        1.00,   // 2 – Average (baseline)
        1.08,   // 3 – Adequate
        1.16,   // 4 – Modern
        1.24,   // 5 – High standard
        1.35    // 6 – Fantastic
    ];

    /// <summary>
    /// Average attribute offset applied when generating youth intake players.
    /// Index = youth facility level (0–6).
    /// </summary>
    public static readonly int[] YouthQualityOffset =
    [
        -2,     // 0 – Low standard
        -1,     // 1 – Below average
        0,      // 2 – Average (baseline)
        1,      // 3 – Adequate
        2,      // 4 – Modern
        3,      // 5 – High standard
        4       // 6 – Fantastic
    ];

    /// <summary>Colour hex string for a given level — used for UI colour coding.</summary>
    public static string GetColour(int level) => level switch
    {
        0 => "#E94560",   // Red-pink  – Low standard
        1 => "#F97316",   // Orange    – Below average
        2 => "#EAB308",   // Yellow    – Average
        3 => "#84CC16",   // Lime      – Adequate
        4 => "#22C55E",   // Green     – Modern
        5 => "#38BDF8",   // Sky blue  – High standard
        6 => "#A78BFA",   // Violet    – Fantastic
        _ => "#8888AA"
    };

    /// <summary>Short description for each level shown in the Facilities tab.</summary>
    public static readonly string[] TrainingDescriptions =
    [
        "Outdated equipment and limited facilities. Player development is severely hampered.",
        "Bare-bones training setup. Marginal progression for the squad.",
        "Standard facilities that meet league requirements. Adequate for steady development.",
        "Well-maintained facilities with quality coaching staff. Players show consistent growth.",
        "State-of-the-art gym and pitch surfaces. A clear boost to all aspects of performance.",
        "Elite-level infrastructure rivalling top European clubs. Exceptional player development.",
        "World-class facilities at the absolute pinnacle. Players reach their potential faster."
    ];

    public static readonly string[] YouthDescriptions =
    [
        "Minimal youth scouting network. Intake players are consistently raw and weak.",
        "Limited youth infrastructure. Occasional promising talent, but mostly underwhelming.",
        "Functional youth system. Produces a reliable stream of average-quality prospects.",
        "Structured youth programme with dedicated scouts. Better-than-average intake quality.",
        "Excellent youth pipeline with wide scouting reach. Regular high-potential prospects.",
        "Premium academy with top-tier coaches and facilities. Intake quality is outstanding.",
        "World-renowned academy. Consistently produces elite-level youth talent every cycle."
    ];

    public static string GetLabel(int level) =>
        level is >= MinLevel and <= MaxLevel ? Labels[level] : "Unknown";

    public static decimal GetTrainingCost(int currentLevel) =>
        currentLevel is >= 0 and < MaxLevel ? TrainingUpgradeCosts[currentLevel] : 0;

    public static decimal GetYouthCost(int currentLevel) =>
        currentLevel is >= 0 and < MaxLevel ? YouthUpgradeCosts[currentLevel] : 0;
}
