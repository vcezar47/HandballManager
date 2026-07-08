using HandballManager.Models;

namespace HandballManager.Services;

/// <summary>
/// Handles all player attribute progression/regression logic.
/// - ProcessDailyProgression: called every day, applies age-based natural drift.
/// - ProcessMatchProgression: called after each match, applies performance bonuses.
/// </summary>
public class PlayerProgressionService
{
    private readonly Random _rng = new();

    // ── Attribute category lists ───────────────────────────────────────

    private static readonly HashSet<string> PhysicalAttrs = new()
    {
        "Acceleration", "Agility", "Balance", "JumpingReach",
        "NaturalFitness", "Pace", "Stamina", "Strength"
    };

    private static readonly HashSet<string> TechnicalAttrs = new()
    {
        "Dribbling", "Finishing", "LongThrows", "Marking",
        "SevenMeterTaking", "Tackling", "Technique", "Receiving", "Passing"
    };

    private static readonly HashSet<string> MentalAttrs = new()
    {
        "Aggression", "Anticipation", "Composure", "Concentration",
        "Decisions", "Determination", "Flair", "Leadership",
        "OffTheBall", "Positioning", "Teamwork", "Vision"
    };

    private static readonly HashSet<string> GkTechnicalAttrs = new()
    {
        "AerialReach", "Communication", "Eccentricity", "Handling",
        "Throwing", "OneOnOnes", "Reflexes"
    };

    // All growable attributes for small background drift
    private static readonly string[] AllAttributes = PhysicalAttrs
        .Concat(TechnicalAttrs)
        .Concat(MentalAttrs)
        .Concat(GkTechnicalAttrs)
        .ToArray();

    // ── Match stat → attribute influence ───────────────────────────────

    private static readonly string[] GoalAttrs =
        { "Finishing", "Composure", "SevenMeterTaking", "Technique" };

    private static readonly string[] AssistAttrs =
        { "Passing", "Vision", "Decisions", "Teamwork" };

    private static readonly string[] SaveAttrs =
        { "Reflexes", "OneOnOnes", "Handling", "Positioning", "Anticipation" };

    // ── Position-specific primary attrs ───────────────────────────────

    private static readonly Dictionary<string, string[]> PositionPrimaryAttrs = new()
    {
        { "GK",    new[] { "Reflexes", "OneOnOnes", "Positioning", "Agility", "Concentration", "Anticipation" } },
        { "LW",    new[] { "Finishing", "Acceleration", "Agility", "Flair", "Technique", "Balance" } },
        { "RW",    new[] { "Finishing", "Acceleration", "Agility", "Flair", "Technique", "Balance" } },
        { "LB",    new[] { "Finishing", "JumpingReach", "Strength", "Passing", "Decisions", "Marking" } },
        { "RB",    new[] { "Finishing", "JumpingReach", "Strength", "Passing", "Decisions", "Marking" } },
        { "CB",    new[] { "Passing", "Vision", "Decisions", "Teamwork", "Agility", "Technique" } },
        { "Pivot", new[] { "Strength", "Balance", "Finishing", "Positioning", "Marking", "Tackling" } },
    };

    // ── Age bracket definitions ───────────────────────────────────────

    private enum AgeBracket { Development, PeakGrowth, Prime, LatePrime, Decline, Veteran }

    private static AgeBracket GetAgeBracket(int age) => age switch
    {
        <= 20 => AgeBracket.Development,
        <= 24 => AgeBracket.PeakGrowth,
        <= 28 => AgeBracket.Prime,
        <= 31 => AgeBracket.LatePrime,
        <= 34 => AgeBracket.Decline,
        _ => AgeBracket.Veteran,
    };

    /// <summary>
    /// Base growth/decline rate PER DAY.
    /// Rates boosted to ensure visible changes for almost every player.
    /// Development: ~4-6 points per core attribute per season.
    /// Prime: ~1-2 points per core attribute.
    /// Veteran: ~4-8 points regression per season.
    /// </summary>
    private (double min, double max) GetDailyRate(AgeBracket bracket, string attrCategory)
    {
        if (attrCategory == "Physical")
        {
            return bracket switch
            {
                AgeBracket.Development => (0.007, 0.017), // Robust growth
                AgeBracket.PeakGrowth => (0.003, 0.009),
                AgeBracket.Prime => (-0.003, 0.004), // Stability
                AgeBracket.LatePrime => (-0.009, -0.004), // Early decline
                AgeBracket.Decline => (-0.017, -0.009), // Strong decline
                AgeBracket.Veteran => (-0.028, -0.017), // Heavy decline
                _ => (0, 0)
            };
        }

        if (attrCategory == "Technical" || attrCategory == "GkTechnical")
        {
            return bracket switch
            {
                AgeBracket.Development => (0.006, 0.014),
                AgeBracket.PeakGrowth => (0.004, 0.009),
                AgeBracket.Prime => (0.001, 0.006),
                AgeBracket.LatePrime => (0.000, 0.004),
                AgeBracket.Decline => (-0.007, -0.002),
                AgeBracket.Veteran => (-0.013, -0.006),
                _ => (0, 0)
            };
        }

        // Mental — lasts longest
        return bracket switch
        {
            AgeBracket.Development => (0.005, 0.011),
            AgeBracket.PeakGrowth => (0.003, 0.009),
            AgeBracket.Prime => (0.002, 0.007),
            AgeBracket.LatePrime => (0.001, 0.005),
            AgeBracket.Decline => (0.000, 0.004), // Mentals barely drop
            AgeBracket.Veteran => (-0.004, 0.001),
            _ => (0, 0)
        };
    }

    private string GetCategory(string attr)
    {
        if (PhysicalAttrs.Contains(attr)) return "Physical";
        if (TechnicalAttrs.Contains(attr)) return "Technical";
        if (MentalAttrs.Contains(attr)) return "Mental";
        if (GkTechnicalAttrs.Contains(attr)) return "GkTechnical";
        return "Unknown";
    }

    // ── Public API ────────────────────────────────────────────────────

    public void ProcessDailyProgression(Player player, int numDays, double youthDevFactor = 1.0)
    {
        if (numDays <= 0) return;

        var bracket = GetAgeBracket(player.Age);
        var accumulators = player.GrowthAccumulators;
        var seasonChanges = player.SeasonAttributeChanges;

        // Apply youth development factor for under-21s
        double effectiveYouthFactor = (bracket == AgeBracket.Development) ? youthDevFactor : 1.0;

        // Core drift for relevant attributes
        var relevantAttrs = GetRelevantAttributes(player.Position);
        foreach (var attr in relevantAttrs)
        {
            if (_rng.NextDouble() > 0.45) continue; // Only ~45% of attributes mutate

            var category = GetCategory(attr);
            var (min, max) = GetDailyRate(bracket, category);

            double dailyDelta = min + _rng.NextDouble() * (max - min);
            double totalDelta = dailyDelta * (numDays * 0.8) * effectiveYouthFactor; // Added factor

            if (!accumulators.ContainsKey(attr)) accumulators[attr] = 0;
            accumulators[attr] += totalDelta;
            ApplyAccumulatorTicks(player, attr, accumulators, seasonChanges);
        }

        // Background drift for all attributes (even non-primary)
        foreach (var attr in AllAttributes)
        {
            if (relevantAttrs.Contains(attr)) continue;

            if (_rng.NextDouble() > 0.25) continue; // Only ~25% of attributes mutate

            double tinyDrift = (_rng.NextDouble() - 0.4) * 0.004; // Slightly biased towards growth
            if (bracket >= AgeBracket.Decline) tinyDrift -= 0.005; // Biased towards regression for vets

            if (!accumulators.ContainsKey(attr)) accumulators[attr] = 0;
            accumulators[attr] += tinyDrift * numDays * effectiveYouthFactor; // Added factor
            ApplyAccumulatorTicks(player, attr, accumulators, seasonChanges);
        }

        player.SaveAccumulators();
        player.SaveSeasonChanges();
    }

    public void ProcessMatchProgression(Player player, MatchPlayerStat stat, int currentMatchweek)
    {
        var accumulators = player.GrowthAccumulators;
        var seasonChanges = player.SeasonAttributeChanges;
        var relevantAttrs = GetRelevantAttributes(player.Position);

        // Update Career Stats
        player.CareerGoals += stat.Goals;
        player.CareerAssists += stat.Assists;
        player.CareerSaves += stat.Saves;
        player.CareerMatchesPlayed++;

        foreach (var attr in relevantAttrs)
        {
            if (_rng.NextDouble() > 0.45) continue; // Only ~45% of attributes mutate

            double bonus = CalculateMatchBonus(attr, player, stat);
            if (Math.Abs(bonus) < 0.0001) continue;

            if (!accumulators.ContainsKey(attr)) accumulators[attr] = 0;
            accumulators[attr] += bonus;
            ApplyAccumulatorTicks(player, attr, accumulators, seasonChanges);
        }

        player.SaveAccumulators();
        player.SaveSeasonChanges();

        if (currentMatchweek - player.LastPhaseCheckMatchweek >= 4 || player.LastPhaseCheckMatchweek == 0)
        {
            RecalculatePhase(player);
            player.LastPhaseCheckMatchweek = currentMatchweek;
        }
    }

    public void ProcessEndOfSeason(List<Player> players)
    {
        foreach (var player in players)
        {
            RecalculatePhase(player);

            // Store Last Season Stats
            player.LastSeasonGoals = player.SeasonGoals;
            player.LastSeasonAssists = player.SeasonAssists;
            player.LastSeasonSaves = player.SeasonSaves;
            player.LastSeasonMatchesPlayed = player.MatchesPlayed;
            player.LastSeasonAverageRating = player.AverageRating;

            // Reset Seasonal Stats
            player.SeasonGoals = 0;
            player.SeasonAssists = 0;
            player.SeasonSaves = 0;
            player.MatchesPlayed = 0;
            player.SeasonRatingSum = 0;
            player.LastPhaseCheckMatchweek = 0;
            player.ProgressionPhase = ProgressionPhase.Neutral;
            player.SeasonAttributeChanges.Clear();
            player.TransferredThisSeason = false;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    private static void ApplyAccumulatorTicks(Player player, string attr,
        Dictionary<string, double> accumulators, Dictionary<string, int> seasonChanges)
    {
        int currentVal = GetAttributeValue(player, attr);
        int netChange = 0;

        while (accumulators[attr] >= 1.0)
        {
            int newVal = Math.Min(currentVal + 1, 20);
            if (newVal == currentVal) { accumulators[attr] = 0.99; break; }
            SetAttributeValue(player, attr, newVal);
            currentVal = newVal;
            netChange++;
            accumulators[attr] -= 1.0;
        }

        while (accumulators[attr] <= -1.0)
        {
            int newVal = Math.Max(currentVal - 1, 1);
            if (newVal == currentVal) { accumulators[attr] = -0.99; break; }
            SetAttributeValue(player, attr, newVal);
            currentVal = newVal;
            netChange--;
            accumulators[attr] += 1.0;
        }

        if (netChange != 0)
        {
            seasonChanges[attr] = seasonChanges.GetValueOrDefault(attr) + netChange;
            player.RecentAttributeChanges += netChange;
        }
    }

    private void RecalculatePhase(Player player)
    {
        int net = player.RecentAttributeChanges;
        player.ProgressionPhase = net switch
        {
            >= 3 => ProgressionPhase.FastProgression,
            >= 1 => ProgressionPhase.SlightProgression,
            <= -3 => ProgressionPhase.FastRegression,
            <= -1 => ProgressionPhase.SlightRegression,
            _ => ProgressionPhase.Neutral,
        };
        player.RecentAttributeChanges = 0;
    }

    private double CalculateMatchBonus(string attr, Player player, MatchPlayerStat stat)
    {
        double bonus = 0;
        if (stat.Goals > 0 && GoalAttrs.Contains(attr)) bonus += stat.Goals * 0.017;
        if (stat.Assists > 0 && AssistAttrs.Contains(attr)) bonus += stat.Assists * 0.017;
        if (stat.Saves > 0 && SaveAttrs.Contains(attr)) bonus += stat.Saves * 0.008;

        if (stat.Rating >= 7.5)
        {
            if (PositionPrimaryAttrs.TryGetValue(player.Position, out var primaries) && primaries.Contains(attr))
                bonus += 0.018;
            if (attr == "Determination") bonus += 0.010;
        }

        if (stat.Rating <= 5.0 && stat.Rating > 0)
        {
            if (attr == "Composure") bonus -= 0.010;
            if (attr == "Concentration") bonus -= 0.008;
        }
        return bonus;
    }

    private static HashSet<string> GetRelevantAttributes(string position)
    {
        var attrs = new HashSet<string>(MentalAttrs);
        attrs.UnionWith(PhysicalAttrs);
        if (position == "GK") { attrs.UnionWith(GkTechnicalAttrs); attrs.Add("Passing"); }
        else { attrs.UnionWith(TechnicalAttrs); }
        return attrs;
    }

    private static int GetAttributeValue(Player p, string attr) => attr switch
    {
        "Acceleration" => p.Acceleration,
        "Agility" => p.Agility,
        "Balance" => p.Balance,
        "JumpingReach" => p.JumpingReach,
        "NaturalFitness" => p.NaturalFitness,
        "Pace" => p.Pace,
        "Stamina" => p.Stamina,
        "Strength" => p.Strength,
        "Dribbling" => p.Dribbling,
        "Finishing" => p.Finishing,
        "LongThrows" => p.LongThrows,
        "Marking" => p.Marking,
        "SevenMeterTaking" => p.SevenMeterTaking,
        "Tackling" => p.Tackling,
        "Technique" => p.Technique,
        "Receiving" => p.Receiving,
        "Passing" => p.Passing,
        "AerialReach" => p.AerialReach,
        "Communication" => p.Communication,
        "Eccentricity" => p.Eccentricity,
        "Handling" => p.Handling,
        "Throwing" => p.Throwing,
        "OneOnOnes" => p.OneOnOnes,
        "Reflexes" => p.Reflexes,
        "Aggression" => p.Aggression,
        "Anticipation" => p.Anticipation,
        "Composure" => p.Composure,
        "Concentration" => p.Concentration,
        "Decisions" => p.Decisions,
        "Determination" => p.Determination,
        "Flair" => p.Flair,
        "Leadership" => p.Leadership,
        "OffTheBall" => p.OffTheBall,
        "Positioning" => p.Positioning,
        "Teamwork" => p.Teamwork,
        "Vision" => p.Vision,
        _ => 10
    };

    private static void SetAttributeValue(Player p, string attr, int val)
    {
        switch (attr)
        {
            case "Acceleration": p.Acceleration = val; break;
            case "Agility": p.Agility = val; break;
            case "Balance": p.Balance = val; break;
            case "JumpingReach": p.JumpingReach = val; break;
            case "NaturalFitness": p.NaturalFitness = val; break;
            case "Pace": p.Pace = val; break;
            case "Stamina": p.Stamina = val; break;
            case "Strength": p.Strength = val; break;
            case "Dribbling": p.Dribbling = val; break;
            case "Finishing": p.Finishing = val; break;
            case "LongThrows": p.LongThrows = val; break;
            case "Marking": p.Marking = val; break;
            case "SevenMeterTaking": p.SevenMeterTaking = val; break;
            case "Tackling": p.Tackling = val; break;
            case "Technique": p.Technique = val; break;
            case "Receiving": p.Receiving = val; break;
            case "Passing": p.Passing = val; break;
            case "AerialReach": p.AerialReach = val; break;
            case "Communication": p.Communication = val; break;
            case "Eccentricity": p.Eccentricity = val; break;
            case "Handling": p.Handling = val; break;
            case "Throwing": p.Throwing = val; break;
            case "OneOnOnes": p.OneOnOnes = val; break;
            case "Reflexes": p.Reflexes = val; break;
            case "Aggression": p.Aggression = val; break;
            case "Anticipation": p.Anticipation = val; break;
            case "Composure": p.Composure = val; break;
            case "Concentration": p.Concentration = val; break;
            case "Decisions": p.Decisions = val; break;
            case "Determination": p.Determination = val; break;
            case "Flair": p.Flair = val; break;
            case "Leadership": p.Leadership = val; break;
            case "OffTheBall": p.OffTheBall = val; break;
            case "Positioning": p.Positioning = val; break;
            case "Teamwork": p.Teamwork = val; break;
            case "Vision": p.Vision = val; break;
        }
    }
}