// Copyright (c) 2026 Vieru-Potecu Cezar-Mihai. MIT licensed — see LICENSE.

using Xunit;

namespace HandballManager.Samples.Tests;

/// <summary>
/// The competitions hold sixteen and thirty-two clubs. Almost every test here is that one
/// sentence, asked of a world that keeps changing shape.
/// </summary>
public class EuropeanPlaceAllocatorTests
{
    private static IReadOnlyList<string> Nations(int n) =>
        Enumerable.Range(1, n).Select(i => $"Country{i:00}").ToList();

    /// <summary>The twelve countries the game actually ships with.</summary>
    private static readonly IReadOnlyList<string> Twelve =
    [
        "Hungary", "France", "Romania", "Denmark", "Norway", "Germany",
        "Sweden", "Slovenia", "Poland", "Croatia", "Montenegro", "Türkiye"
    ];

    [Fact]
    public void TheChampionsLeagueIsFilledExactly()
    {
        var places = EuropeanPlaceAllocator.AllocateChampionsLeague(Twelve, "Sweden");

        Assert.Equal(EuropeanPlaceAllocator.ChampionsLeaguePlaces, places.Values.Sum());
    }

    [Fact]
    public void TheEuropeanLeagueIsFilledExactly()
    {
        var places = EuropeanPlaceAllocator.AllocateEuropeanLeague(Twelve);

        Assert.Equal(EuropeanPlaceAllocator.EuropeanLeaguePlaces, places.Values.Sum());
    }

    /// <summary>
    /// The property that matters more than any single allocation: both competitions stay full
    /// whatever size the world is. A lookup table passes at twelve and fails at thirteen.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(20)]
    public void BothCompetitionsStayFullAtEveryWorldSize(int countries)
    {
        var ranking = Nations(countries);

        Assert.Equal(16, EuropeanPlaceAllocator.AllocateChampionsLeague(ranking, ranking[0]).Values.Sum());
        Assert.Equal(32, EuropeanPlaceAllocator.AllocateEuropeanLeague(ranking).Values.Sum());
    }

    /// <summary>
    /// Adding a country does not add places — it takes one off somebody. Worth a test because
    /// it is the kind of thing a reader assumes is a bug when they first see it happen.
    /// </summary>
    [Fact]
    public void AThirteenthCountryCostsAnExistingOneAPlace()
    {
        var before = EuropeanPlaceAllocator.AllocateEuropeanLeague(Nations(12));
        var after = EuropeanPlaceAllocator.AllocateEuropeanLeague(Nations(13));

        Assert.Equal(32, after.Values.Sum());
        Assert.Contains(before.Keys, nation => after[nation] < before[nation]);
    }

    /// <summary>
    /// Nobody is left with nothing. A country with no European place has nothing to play for
    /// below its champion, which is a worse outcome than an imperfectly weighted table.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(20)]
    public void NoCountryIsLeftOutOfTheEuropeanLeagueEntirely(int countries)
    {
        var places = EuropeanPlaceAllocator.AllocateEuropeanLeague(Nations(countries));

        Assert.All(places.Values, p => Assert.True(p >= 1));
    }

    [Fact]
    public void TheStrongestCountriesSendTheMostClubs()
    {
        var places = EuropeanPlaceAllocator.AllocateEuropeanLeague(Nations(12));
        var ranking = Nations(12);

        // Monotonic down the table: no country outranks one above it.
        for (int i = 1; i < ranking.Count; i++)
            Assert.True(places[ranking[i]] <= places[ranking[i - 1]]);
    }

    [Fact]
    public void TheTopNineCountriesAreGuaranteedAChampionsLeagueClub()
    {
        var ranking = Nations(12);
        var places = EuropeanPlaceAllocator.AllocateChampionsLeague(ranking, ranking[11]);

        Assert.All(ranking.Take(9), nation => Assert.True(places[nation] >= 1));
    }

    /// <summary>
    /// The incentive place. It can land on a country outside the guaranteed nine, which is the
    /// whole point of it — winning the second competition is worth something in the first.
    /// </summary>
    [Fact]
    public void LeadingTheEuropeanLeagueRankingEarnsAnExtraChampionsLeaguePlace()
    {
        var ranking = Nations(12);
        var withoutLeader = EuropeanPlaceAllocator.AllocateChampionsLeague(ranking);
        var withLeader = EuropeanPlaceAllocator.AllocateChampionsLeague(ranking, "Country11");

        Assert.True(withLeader["Country11"] > withoutLeader["Country11"]);
        Assert.Equal(16, withLeader.Values.Sum());
    }

    [Fact]
    public void AnUnknownLeaderIsIgnoredRatherThanCreatingAPlaceForNobody()
    {
        var places = EuropeanPlaceAllocator.AllocateChampionsLeague(Nations(12), "Atlantis");

        Assert.Equal(16, places.Values.Sum());
        Assert.DoesNotContain("Atlantis", places.Keys);
    }

    [Fact]
    public void AnEmptyWorldAllocatesNothingInsteadOfLoopingForever()
    {
        Assert.Empty(EuropeanPlaceAllocator.AllocateChampionsLeague([]));
        Assert.Empty(EuropeanPlaceAllocator.AllocateEuropeanLeague([]));
    }
}
