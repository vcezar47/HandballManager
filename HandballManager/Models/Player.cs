using System.Text.Json;

namespace HandballManager.Models;

public class Player
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Computed full name — not stored in DB
    public string Name => $"{FirstName} {LastName}".Trim();

    public int ShirtNumber { get; set; }
    public DateTime Birthdate { get; set; }

    // Computed age — not stored in DB. 
    // Uses a global date for simplicity in UI bindings.
    public static DateTime GlobalGameDate { get; set; } = new DateTime(2025, 7, 1);
    public int Age
    {
        get
        {
            var today = GlobalGameDate;
            var age = today.Year - Birthdate.Year;
            if (Birthdate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    public decimal MonthlyWage { get; set; }
    public decimal WeeklyWage => Math.Round((MonthlyWage * 12m) / 52m, 0);

    public DateTime ContractEndDate { get; set; }

    /// <summary>Months remaining on contract as of the given date (0 if already expired).</summary>
    public int ContractMonthsRemaining(DateTime asOfDate)
    {
        if (ContractEndDate.Date <= asOfDate.Date) return 0;
        var months = (ContractEndDate.Year - asOfDate.Year) * 12 + (ContractEndDate.Month - asOfDate.Month);
        if (ContractEndDate.Day < asOfDate.Day) months--;
        return Math.Max(0, months);
    }

    public bool HasSixMonthsOrLessOnContract(DateTime asOfDate) => ContractMonthsRemaining(asOfDate) <= 6 && ContractMonthsRemaining(asOfDate) >= 0;
    public string Position { get; set; } = string.Empty; // GK, LW, RW, LB, RB, CB, Pivot
    public string Nationality { get; set; } = "ROU"; // ISO 3-letter code
    public int Height { get; set; } // in cm
    public int Weight { get; set; } // in kg

    // --- Outfielder Technical Attributes (1-20) ---
    public int Dribbling { get; set; }
    public int Finishing { get; set; }
    public int LongThrows { get; set; }
    public int Marking { get; set; }
    public int SevenMeterTaking { get; set; }
    public int Tackling { get; set; }
    public int Technique { get; set; }

    // --- Shared Technical Attributes (1-20) ---
    public int Receiving { get; set; } // First touch
    public int Passing { get; set; }

    // --- Goalkeeper Specific Technical (1-20) ---
    public int AerialReach { get; set; }
    public int Communication { get; set; }
    public int Eccentricity { get; set; }
    public int Handling { get; set; }
    public int Throwing { get; set; }
    public int OneOnOnes { get; set; }
    public int Reflexes { get; set; }

    // --- Mental Attributes (1-20) ---
    public int Aggression { get; set; }
    public int Anticipation { get; set; }
    public int Composure { get; set; }
    public int Concentration { get; set; }
    public int Decisions { get; set; }
    public int Determination { get; set; }
    public int Flair { get; set; }
    public int Leadership { get; set; }
    public int OffTheBall { get; set; }
    public int Positioning { get; set; }
    public int Teamwork { get; set; }
    public int Vision { get; set; }

    public bool IsInjured { get; set; }
    public bool IsRetiringAtEndOfSeason { get; set; }
    public bool IsRetired { get; set; }
    /// <summary>Set to true when a transfer completes; reset at end of season. Prevents a player moving twice in one season.</summary>
    public bool TransferredThisSeason { get; set; } = false;

    // --- Progression System ---
    public ProgressionPhase ProgressionPhase { get; set; } = ProgressionPhase.Neutral;

    /// <summary>
    /// Tracks net integer attribute changes over recent matchweeks to determine the displayed phase.
    /// Reset each time the phase is recalculated (every ~4 matchweeks).
    /// </summary>
    public int RecentAttributeChanges { get; set; }
    public int LastPhaseCheckMatchweek { get; set; }

    /// <summary>
    /// Hidden floating-point accumulators for each attribute.
    /// When an accumulator crosses ±1.0, the integer attribute ticks up/down.
    /// Serialised to DB as JSON text.
    /// </summary>
    public string GrowthAccumulatorsJson { get; set; } = "{}";

    // Helper to get/set the dictionary form — not stored directly in DB
    private Dictionary<string, double>? _growthCache;
    public Dictionary<string, double> GrowthAccumulators
    {
        get
        {
            _growthCache ??= string.IsNullOrEmpty(GrowthAccumulatorsJson)
                ? new Dictionary<string, double>()
                : JsonSerializer.Deserialize<Dictionary<string, double>>(GrowthAccumulatorsJson)
                  ?? new Dictionary<string, double>();
            return _growthCache;
        }
        set
        {
            _growthCache = value;
            GrowthAccumulatorsJson = JsonSerializer.Serialize(value);
        }
    }

    /// <summary>Flush any pending accumulator changes back to the JSON column.</summary>
    public void SaveAccumulators()
    {
        if (_growthCache != null)
            GrowthAccumulatorsJson = JsonSerializer.Serialize(_growthCache);
    }

    /// <summary>
    /// Tracks total integer changes per attribute over the season.
    /// E.g. { "Finishing": +2, "Pace": -1 } means Finishing rose by 2, Pace dropped by 1.
    /// Used by the UI to display per-attribute progression arrows.
    /// </summary>
    public string SeasonAttributeChangesJson { get; set; } = "{}";

    private Dictionary<string, int>? _seasonChangesCache;
    public Dictionary<string, int> SeasonAttributeChanges
    {
        get
        {
            _seasonChangesCache ??= string.IsNullOrEmpty(SeasonAttributeChangesJson)
                ? new Dictionary<string, int>()
                : JsonSerializer.Deserialize<Dictionary<string, int>>(SeasonAttributeChangesJson)
                  ?? new Dictionary<string, int>();
            return _seasonChangesCache;
        }
        set
        {
            _seasonChangesCache = value;
            SeasonAttributeChangesJson = JsonSerializer.Serialize(value);
        }
    }

    public void SaveSeasonChanges()
    {
        if (_seasonChangesCache != null)
            SeasonAttributeChangesJson = JsonSerializer.Serialize(_seasonChangesCache);
    }

    // Seasonal Stats
    public int SeasonGoals { get; set; }
    public int SeasonAssists { get; set; }
    public int SeasonSaves { get; set; }
    public int MatchesPlayed { get; set; }
    public double SeasonRatingSum { get; set; }
    public double AverageRating => MatchesPlayed > 0 ? SeasonRatingSum / MatchesPlayed : 0;

    // Career Stats
    public int CareerGoals { get; set; }
    public int CareerAssists { get; set; }
    public int CareerSaves { get; set; }
    public int CareerMatchesPlayed { get; set; }

    // Last Season Stats (for display after reset)
    public int LastSeasonGoals { get; set; }
    public int LastSeasonAssists { get; set; }
    public int LastSeasonSaves { get; set; }
    public int LastSeasonMatchesPlayed { get; set; }
    public double LastSeasonAverageRating { get; set; }

    // --- Physical Attributes (1-20) ---
    public int Acceleration { get; set; }
    public int Agility { get; set; }
    public int Balance { get; set; }
    public int JumpingReach { get; set; }
    public int NaturalFitness { get; set; }
    public int Pace { get; set; }
    public int Stamina { get; set; }
    public int Strength { get; set; }

    // Foreign key
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    // Derived overall rating (weighted average — not stored in DB)
    public int Overall => (int)Math.Round(CalculateRawOverall());

    public int Overall100 => Math.Clamp((int)Math.Round(CalculateRawOverall() * 4 + 24), 10, 99);

    // Estimated buyout fee — capped at €250k for women's handball market
    // Formula: base fee grows with OVR and peaks for players aged 22-26
    public decimal BuyoutFee
    {
        get
        {
            double ovr = Overall100;
            double ageFactor = Age is >= 22 and <= 26
                ? 1.0
                : Age < 22
                    ? 0.7 + (Age - 18) * 0.075   // younger players valued less
                    : 1.0 - (Age - 26) * 0.06;    // older players decline
            ageFactor = Math.Max(ageFactor, 0.15);

            // Scale: OVR 50 ≈ €10k, OVR 99 ≈ €250k
            // Prevent negative base in Math.Pow by maxing with 0
            double effectiveOvr = Math.Max(0, ovr - 40);
            double raw = Math.Pow(effectiveOvr / 59.0, 2.2) * 250_000 * ageFactor;

            // Handle edge cases where raw becomes NaN or Infinity
            if (double.IsNaN(raw) || double.IsInfinity(raw))
                return 1000m;

            // Safely clamp before casting to decimal
            double clampedRaw = Math.Clamp(raw, 1000, 250_000);
            decimal fee = (decimal)Math.Round(clampedRaw / 1000.0) * 1000m;
            return fee;
        }
    }

    private double CalculateRawOverall()
    {
        return Position switch
        {
            "GK" => CalculateGkRating(),
            "LW" or "RW" => CalculateWingRating(),
            "LB" or "RB" => CalculateBackRating(),
            "CB" => CalculatePlaymakerRating(),
            "Pivot" => CalculatePivotRating(),
            _ => CalculateGenericRating()
        };
    }

    private double CalculateGkRating()
    {
        double avgAll = (AerialReach + Communication + Eccentricity + Handling + Throwing + OneOnOnes + Reflexes +
                        Aggression + Anticipation + Composure + Concentration + Decisions + Determination +
                        Flair + Leadership + OffTheBall + Positioning + Teamwork + Vision +
                        Acceleration + Agility + Balance + JumpingReach + NaturalFitness + Pace + Stamina + Strength) / 27.0;

        // Primary: Reflexes, One on Ones, Positioning, Agility, Concentration, Anticipation
        double primaryAvg = (Reflexes + OneOnOnes + Positioning + Agility + Concentration + Anticipation) / 6.0;
        // Secondary: Throwing, Communication, Handling, Aerial Reach, Passing, Balance
        double secondaryAvg = (Throwing + Communication + Handling + AerialReach + Passing + Balance) / 6.0;

        return (avgAll * 0.2) + (primaryAvg * 0.5) + (secondaryAvg * 0.3);
    }

    private double CalculateWingRating()
    {
        double avgAll = (Dribbling + Finishing + LongThrows + Marking + SevenMeterTaking + Tackling + Technique + Receiving + Passing +
                        Aggression + Anticipation + Composure + Concentration + Decisions + Determination + Flair + Leadership + OffTheBall + Positioning + Teamwork + Vision +
                        Acceleration + Agility + Balance + JumpingReach + NaturalFitness + Pace + Stamina + Strength) / 29.0;

        // Primary: Finishing, Acceleration, Agility, Flair, Technique, Balance
        double primaryAvg = (Finishing + Acceleration + Agility + Flair + Technique + Balance) / 6.0;
        // Secondary: Pace, Receiving, Marking, Stamina, Anticipation, Dribbling
        double secondaryAvg = (Pace + Receiving + Marking + Stamina + Anticipation + Dribbling) / 6.0;

        return (avgAll * 0.2) + (primaryAvg * 0.5) + (secondaryAvg * 0.3);
    }

    private double CalculateBackRating()
    {
        double avgAll = (Dribbling + Finishing + LongThrows + Marking + SevenMeterTaking + Tackling + Technique + Receiving + Passing +
                        Aggression + Anticipation + Composure + Concentration + Decisions + Determination + Flair + Leadership + OffTheBall + Positioning + Teamwork + Vision +
                        Acceleration + Agility + Balance + JumpingReach + NaturalFitness + Pace + Stamina + Strength) / 29.0;

        // Primary: Finishing, Jumping Reach, Strength, Passing, Decisions, Marking
        double primaryAvg = (Finishing + JumpingReach + Strength + Passing + Decisions + Marking) / 6.0;
        // Secondary: Vision, Technique, Long Throws, Stamina, Aggression, Composure
        double secondaryAvg = (Vision + Technique + LongThrows + Stamina + Aggression + Composure) / 6.0;

        return (avgAll * 0.2) + (primaryAvg * 0.5) + (secondaryAvg * 0.3);
    }

    private double CalculatePlaymakerRating()
    {
        double avgAll = (Dribbling + Finishing + LongThrows + Marking + SevenMeterTaking + Tackling + Technique + Receiving + Passing +
                        Aggression + Anticipation + Composure + Concentration + Decisions + Determination + Flair + Leadership + OffTheBall + Positioning + Teamwork + Vision +
                        Acceleration + Agility + Balance + JumpingReach + NaturalFitness + Pace + Stamina + Strength) / 29.0;

        // Primary: Passing, Vision, Decisions, Teamwork, Agility, Technique
        double primaryAvg = (Passing + Vision + Decisions + Teamwork + Agility + Technique) / 6.0;
        // Secondary: Finishing, Dribbling, Composure, Acceleration, Anticipation, Leadership
        double secondaryAvg = (Finishing + Dribbling + Composure + Acceleration + Anticipation + Leadership) / 6.0;

        return (avgAll * 0.2) + (primaryAvg * 0.5) + (secondaryAvg * 0.3);
    }

    private double CalculatePivotRating()
    {
        double avgAll = (Dribbling + Finishing + LongThrows + Marking + SevenMeterTaking + Tackling + Technique + Receiving + Passing +
                        Aggression + Anticipation + Composure + Concentration + Decisions + Determination + Flair + Leadership + OffTheBall + Positioning + Teamwork + Vision +
                        Acceleration + Agility + Balance + JumpingReach + NaturalFitness + Pace + Stamina + Strength) / 29.0;

        // Primary: Strength, Balance, Finishing, Positioning, Marking, Tackling
        double primaryAvg = (Strength + Balance + Finishing + Positioning + Marking + Tackling) / 6.0;
        // Secondary: Aggression, Determination, Agility, Teamwork, Stamina, Concentration
        double secondaryAvg = (Aggression + Determination + Agility + Teamwork + Stamina + Concentration) / 6.0;

        return (avgAll * 0.2) + (primaryAvg * 0.5) + (secondaryAvg * 0.3);
    }

    private double CalculateGenericRating()
    {
        return (Technique + Passing + Decisions + Stamina) / 4.0;
    }
}