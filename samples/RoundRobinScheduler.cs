// Copyright (c) 2026 Vieru-Potecu Cezar-Mihai. MIT licensed — see LICENSE.
//
// Extracted from Handball Manager's fixture generator. The version in the game writes rows
// through EF Core and knows about competition phases; this one is the scheduling itself,
// with nothing else attached.

namespace HandballManager.Samples;

/// <summary>One scheduled match. Home and away are team ids, in the orientation they will be played.</summary>
public readonly record struct Fixture(int Home, int Away);

/// <summary>
/// Builds a round-robin fixture list where every club meets every other club a fixed number of
/// times, and no club plays twice on the same match day.
/// </summary>
/// <remarks>
/// <para>
/// Written because twelve national leagues do not agree on what a season is. Most play each
/// opponent twice. Montenegro's eight clubs meet three times. Slovenia's thirteen meet once
/// before splitting, which means one club is idle every round — a league whose fixture count
/// was derived from "rounds = clubs - 1" quietly drops a whole match day.
/// </para>
/// <para>
/// The circle method underneath is standard: fix one club, rotate the rest. The parts worth
/// reading are the bye handling for odd entries, and the home/away balancing on an odd number
/// of meetings.
/// </para>
/// </remarks>
public static class RoundRobinScheduler
{
    /// <summary>Stands in for the absent club when the entry list is odd. Never emitted.</summary>
    private const int Bye = -1;

    /// <summary>
    /// Schedules <paramref name="meetings"/> meetings between every pair of clubs.
    /// </summary>
    /// <param name="teamIds">The clubs. Order is respected, so shuffle beforehand if the caller wants a random draw.</param>
    /// <param name="meetings">1 for a single round robin, 2 for the usual home-and-away season, 3 or more for the leagues that go round again.</param>
    /// <param name="rng">
    /// Used only to orient the ties in an odd final pass. Pass a seeded instance for a
    /// reproducible fixture list; the two-meeting case never touches it.
    /// </param>
    /// <returns>One list of fixtures per match day, in playing order.</returns>
    public static IReadOnlyList<IReadOnlyList<Fixture>> Schedule(
        IReadOnlyList<int> teamIds, int meetings, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        if (meetings < 1) throw new ArgumentOutOfRangeException(nameof(meetings), "A season is at least one meeting.");
        if (teamIds.Count < 2) return [];
        if (teamIds.Distinct().Count() != teamIds.Count)
            throw new ArgumentException("The same club appears twice.", nameof(teamIds));

        var single = SingleRoundRobin(teamIds);
        var season = new List<IReadOnlyList<Fixture>>();

        // Meetings come in mirrored pairs: one pass as drawn, one with every tie reversed, which
        // balances home and away exactly. Any odd meeting left over is handled below.
        for (int pair = 0; pair < meetings / 2; pair++)
        {
            season.AddRange(single);
            season.AddRange(single.Select(Reversed));
        }

        if (meetings % 2 == 1)
        {
            // An odd pass cannot be balanced, only spread. Reversing the whole pass would hand
            // the same half of the division an extra home game every single season, so the
            // orientation is decided per tie instead. A test that only counts fixtures passes
            // happily while every pairing is played at one ground.
            rng ??= new Random();
            season.AddRange(single.Select(round =>
                (IReadOnlyList<Fixture>)round
                    .Select(f => rng.Next(2) == 0 ? f : new Fixture(f.Away, f.Home))
                    .ToList()));
        }

        return season;
    }

    /// <summary>
    /// One pass of the circle method: every club meets every other club exactly once.
    /// </summary>
    /// <remarks>
    /// An odd entry list is padded with a bye placeholder, which is what makes the round count
    /// correct rather than the fixture count correct. Thirteen clubs take thirteen match days
    /// and play twelve games each; the fourteenth slot each round is the club sitting out.
    /// </remarks>
    private static List<IReadOnlyList<Fixture>> SingleRoundRobin(IReadOnlyList<int> teamIds)
    {
        var teams = new List<int>(teamIds);
        if (teams.Count % 2 != 0) teams.Add(Bye);

        int n = teams.Count;
        var rotating = teams.Skip(1).ToList();
        var rounds = new List<IReadOnlyList<Fixture>>(n - 1);

        for (int round = 0; round < n - 1; round++)
        {
            var current = new List<int>(n) { teams[0] };
            current.AddRange(rotating);

            var matches = new List<Fixture>(n / 2);
            for (int i = 0; i < n / 2; i++)
            {
                int home = current[i], away = current[n - 1 - i];
                if (home != Bye && away != Bye) matches.Add(new Fixture(home, away));
            }
            rounds.Add(matches);

            // Rotate everything except the fixed first club.
            var last = rotating[^1];
            rotating.RemoveAt(rotating.Count - 1);
            rotating.Insert(0, last);
        }

        return rounds;
    }

    private static IReadOnlyList<Fixture> Reversed(IReadOnlyList<Fixture> round) =>
        round.Select(f => new Fixture(f.Away, f.Home)).ToList();
}
