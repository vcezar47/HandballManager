using HandballManager.Models;

namespace HandballManager.Services;

/// <summary>
/// Gives the mental attributes an actual job in the match engines. Composure,
/// Concentration, Determination, Flair, Leadership, OffTheBall and Teamwork were
/// stored, displayed and trained but read by neither engine.
///
/// Every modifier here is centred on <see cref="Baseline"/>, so a squad of average
/// mentals comes out at exactly 1.0 and overall scoring is unchanged. Only squads
/// that are notably strong or weak mentally move the numbers.
/// </summary>
public static class TeamIntangibles
{
    /// <summary>A typical senior attribute. Modifiers are neutral at this value.</summary>
    private const double Baseline = 13.0;

    /// <summary>Deviation from baseline, normalised to roughly -1..+1 across the 1-20 range.</summary>
    private static double Deviation(double attribute) => (attribute - Baseline) / 7.0;

    private static double AverageOr(IEnumerable<Player> players, Func<Player, int> selector)
    {
        var list = players as IList<Player> ?? players.ToList();
        return list.Count == 0 ? Baseline : list.Average(p => (double)selector(p));
    }

    /// <summary>
    /// Squad cohesion from Teamwork and Leadership, as a multiplier on attack and
    /// defence ratings. Range is roughly 0.94–1.06.
    /// </summary>
    public static double Cohesion(IEnumerable<Player> outfield)
    {
        var list = outfield as IList<Player> ?? outfield.ToList();
        if (list.Count == 0) return 1.0;

        double teamwork = AverageOr(list, p => p.Teamwork);
        // One strong voice lifts a side more than an even spread of captains does.
        double leadership = list.Max(p => (double)p.Leadership);

        return 1.0 + Deviation(teamwork) * 0.045 + Deviation(leadership) * 0.02;
    }

    /// <summary>
    /// Concentration's effect on holding onto the ball. Applied to shot chance, so
    /// a sloppy side turns possession over more often. Roughly 0.96–1.04.
    /// </summary>
    public static double PossessionSecurity(IEnumerable<Player> attackers)
        => 1.0 + Deviation(AverageOr(attackers, p => p.Concentration)) * 0.035;

    /// <summary>
    /// Off-the-ball movement creates the opening. Multiplier on shot chance,
    /// roughly 0.96–1.04.
    /// </summary>
    public static double ChanceCreation(IEnumerable<Player> attackers)
        => 1.0 + Deviation(AverageOr(attackers, p => p.OffTheBall)) * 0.04;

    /// <summary>
    /// Composure under pressure, as an additive shift to goal chance. Only bites in
    /// the closing minutes of a tight game; zero at all other times.
    /// </summary>
    public static double ClutchShift(Player shooter, bool isClutchMoment)
        => isClutchMoment ? Deviation(shooter.Composure) * 0.06 : 0.0;

    /// <summary>
    /// Determination when chasing a deficit, as an additive shift to shot chance.
    /// Scales with how far behind the side is and caps out at a three-goal gap.
    /// Only the live engine can use this — the quick sim plays one side's possessions
    /// through before the other's, so it has no running score to read.
    /// </summary>
    public static double ComebackShift(IEnumerable<Player> attackers, int goalDeficit)
    {
        if (goalDeficit <= 0) return 0.0;
        double intensity = Math.Min(goalDeficit, 3) / 3.0;
        return Deviation(AverageOr(attackers, p => p.Determination)) * 0.05 * intensity;
    }

    /// <summary>
    /// Determination as a steady multiplier on shot chance, for the quick sim where
    /// no running score exists. A driven side works more possessions into a shot.
    /// Roughly 0.97–1.03.
    /// </summary>
    public static double Drive(IEnumerable<Player> attackers)
        => 1.0 + Deviation(AverageOr(attackers, p => p.Determination)) * 0.03;

    /// <summary>
    /// Flair's chance of producing something the keeper cannot read — a spin shot,
    /// an unexpected angle. Returns the probability of an outright unstoppable effort.
    /// </summary>
    public static double MomentOfBrillianceChance(Player shooter)
        => Math.Clamp(0.015 + Deviation(shooter.Flair) * 0.02, 0.0, 0.06);
}
