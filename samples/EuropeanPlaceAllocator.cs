// Copyright (c) 2026 Vieru-Potecu Cezar-Mihai. MIT licensed — see LICENSE.
//
// Extracted from Handball Manager's coefficient service. The version in the game reads the
// ranking out of the database and awards the places to actual clubs; this is the allocation.

namespace HandballManager.Samples;

/// <summary>
/// Splits the two European competitions' entries between countries, given how those countries
/// are currently ranked.
/// </summary>
/// <remarks>
/// <para>
/// The competitions hold a fixed number of clubs — sixteen and thirty-two — and that total has
/// to come out exact however the ranking moves and however many countries exist. Written as
/// rules plus a correction rather than as a lookup table for precisely that reason: a table
/// with twelve rows in it is wrong the day a thirteenth country is added, and wrong silently,
/// because a country missing from the table reads as a country with no European place at all.
/// </para>
/// <para>
/// The consequence is worth stating out loud, because it surprised me: adding a country does
/// not add places, it takes one off somebody else. There is no arrangement in which everyone
/// keeps what they had.
/// </para>
/// </remarks>
public static class EuropeanPlaceAllocator
{
    public const int ChampionsLeaguePlaces = 16;
    public const int EuropeanLeaguePlaces = 32;

    /// <summary>
    /// How the sixteen Champions League places are split.
    /// </summary>
    /// <param name="ranking">Countries, strongest first, by Champions League coefficient.</param>
    /// <param name="europeanLeagueLeader">
    /// The country topping the *other* ranking, which earns one extra place. May be a country
    /// outside the guaranteed nine, and may be null if there is no such ranking yet.
    /// </param>
    /// <remarks>
    /// The real allocation is nine guaranteed places, one incentive place for whoever leads the
    /// European League ranking, and six wild cards awarded on criteria including television
    /// interest and arena quality — things a simulation has no model of. The nine and the
    /// incentive are kept exactly; the wild cards go back down the ranking from the top, which
    /// is roughly where they end up in practice.
    /// </remarks>
    public static IReadOnlyDictionary<string, int> AllocateChampionsLeague(
        IReadOnlyList<string> ranking, string? europeanLeagueLeader = null)
    {
        ArgumentNullException.ThrowIfNull(ranking);
        if (ranking.Count == 0) return new Dictionary<string, int>();

        var places = ranking.ToDictionary(nation => nation, _ => 0);

        foreach (var nation in ranking.Take(9)) places[nation] = 1;

        if (europeanLeagueLeader != null && places.ContainsKey(europeanLeagueLeader))
            places[europeanLeagueLeader]++;

        // Whatever is left goes back down the ranking, one country at a time, wrapping if the
        // world is smaller than the competition.
        int remaining = ChampionsLeaguePlaces - places.Values.Sum();
        for (int i = 0; remaining > 0; i++)
        {
            places[ranking[i % ranking.Count]]++;
            remaining--;
        }

        return places;
    }

    /// <summary>
    /// How the thirty-two European League places are split.
    /// </summary>
    /// <param name="ranking">Countries, strongest first, by European League coefficient.</param>
    /// <remarks>
    /// The EHF's band shape: the top two countries send four clubs, the next seven send three,
    /// everyone else sends two. Across twelve countries those bands come to thirty-five for a
    /// competition holding thirty-two, so the surplus is taken off the weakest countries first
    /// — but never below one. A country with no European place has nothing to play for below
    /// its champion, and with a small world the same two countries would be that country every
    /// season, since their one strong club is always in the Champions League and nothing it
    /// does there counts towards this ranking.
    /// </remarks>
    public static IReadOnlyDictionary<string, int> AllocateEuropeanLeague(IReadOnlyList<string> ranking)
    {
        ArgumentNullException.ThrowIfNull(ranking);
        if (ranking.Count == 0) return new Dictionary<string, int>();

        var places = new Dictionary<string, int>();
        for (int i = 0; i < ranking.Count; i++)
            places[ranking[i]] = i switch { < 2 => 4, < 9 => 3, _ => 2 };

        int surplus = places.Values.Sum() - EuropeanLeaguePlaces;

        // Take the surplus off the bottom of the table upwards, repeatedly, never below one.
        // Bounded rather than while(surplus > 0) so a ranking too small to absorb it stops
        // instead of spinning.
        for (int pass = 0; surplus > 0 && pass < EuropeanLeaguePlaces; pass++)
        {
            for (int i = ranking.Count - 1; i >= 0 && surplus > 0; i--)
            {
                if (places[ranking[i]] <= 1) continue;
                places[ranking[i]]--;
                surplus--;
            }
        }

        // A shortfall — few enough countries that the bands do not fill the competition — goes
        // to the strongest, wrapping.
        for (int i = 0; surplus < 0; i++)
        {
            places[ranking[i % ranking.Count]]++;
            surplus++;
        }

        return places;
    }
}
