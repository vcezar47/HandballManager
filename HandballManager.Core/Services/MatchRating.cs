namespace HandballManager.Services;

/// <summary>
/// A player's mark out of ten for a single match.
///
/// Both the quick sim and the live engine need this, and each used to carry its own copy
/// of the arithmetic — three copies in total, which is how they drifted.
/// </summary>
public static class MatchRating
{
    public const double Floor = 3.0;
    public const double Ceiling = 10.0;

    /// <summary>
    /// Built from the save rate, with a little credit for sheer volume of work. A 25%
    /// save rate on an ordinary workload sits at 6.0.
    /// </summary>
    public static double ForGoalkeeper(int saves, int goalsAgainst)
    {
        int shotsFaced = saves + goalsAgainst;
        double savePct = shotsFaced > 0 ? (double)saves / shotsFaced : 0.25;
        return Math.Clamp(6.0 + (savePct - 0.25) * 15.0 + saves * 0.05, Floor, Ceiling);
    }

    /// <summary>
    /// Attacking output on a curve rather than a straight line.
    /// </summary>
    /// <remarks>
    /// The old formula added a flat 0.5 per goal, so a 14-goal game asked for 12.5 and
    /// was clipped to a flat 10.00 — near one in twenty performances maxed out, and a
    /// Team of the Week routinely showed several players tied on a perfect score. The
    /// bonus now approaches 4.5 without reaching it: a 14-goal game reads about 9.8,
    /// and a perfect 10 needs roughly 18 goals-plus-assists, which is an outlier rather
    /// than a weekly occurrence.
    ///
    /// The floor is effectively out of reach because the sim has no off-target shots —
    /// every attempt is a goal or a save — so nobody accumulates enough misses to fall
    /// that far. That is an engine trait, not something to fix by inflating the penalty.
    /// </remarks>
    public static double ForOutfield(int goals, int assists, int shots)
    {
        double contribution = goals + assists * 0.6;
        double bonus = 4.5 * (1.0 - Math.Exp(-contribution / 6.5));
        int misses = Math.Max(0, shots - goals);

        return Math.Clamp(5.9 + bonus - misses * 0.45, Floor, Ceiling);
    }
}
