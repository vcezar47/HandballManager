// Copyright (c) 2026 Vieru-Potecu Cezar-Mihai. MIT licensed — see LICENSE.

using Xunit;

namespace HandballManager.Samples.Tests;

/// <summary>
/// The assertions that actually caught bugs. Counting fixtures does not: a fixture list can have
/// the right number of games in it and still be wrong about who is at home, how many match days
/// a season takes, or whether anybody sat out.
/// </summary>
public class RoundRobinSchedulerTests
{
    private static IReadOnlyList<int> Clubs(int n) => Enumerable.Range(1, n).ToList();

    [Theory]
    [InlineData(14, 2, 26)]  // the usual home-and-away season
    [InlineData(12, 2, 22)]
    [InlineData(8, 3, 21)]   // Montenegro: eight clubs meeting three times
    [InlineData(13, 1, 13)]  // Slovenia: thirteen clubs, one bye every round
    [InlineData(4, 4, 12)]   // a split group playing a quadruple round robin
    public void SeasonTakesTheRightNumberOfMatchDays(int clubs, int meetings, int expectedRounds)
    {
        var season = RoundRobinScheduler.Schedule(Clubs(clubs), meetings, new Random(1));

        Assert.Equal(expectedRounds, season.Count);
    }

    /// <summary>
    /// The odd-entry case. Thirteen clubs take thirteen match days, not twelve: one club is idle
    /// each round. Deriving the round count from "clubs - 1" silently drops a whole match day.
    /// </summary>
    [Fact]
    public void AnOddDivisionLeavesExactlyOneClubIdleEachRound()
    {
        var season = RoundRobinScheduler.Schedule(Clubs(13), 1, new Random(1));

        Assert.All(season, round =>
        {
            Assert.Equal(6, round.Count);
            var playing = round.SelectMany(f => new[] { f.Home, f.Away }).ToList();
            Assert.Equal(12, playing.Distinct().Count());
        });

        // And every club sits out exactly once across the thirteen rounds.
        foreach (int club in Clubs(13))
            Assert.Equal(12, season.Count(round => round.Any(f => f.Home == club || f.Away == club)));
    }

    [Fact]
    public void NobodyPlaysTwiceOnTheSameDay()
    {
        var season = RoundRobinScheduler.Schedule(Clubs(14), 2, new Random(7));

        Assert.All(season, round =>
        {
            var playing = round.SelectMany(f => new[] { f.Home, f.Away }).ToList();
            Assert.Equal(playing.Count, playing.Distinct().Count());
        });
    }

    [Theory]
    [InlineData(14, 2)]
    [InlineData(8, 3)]
    [InlineData(13, 1)]
    public void EveryPairMeetsTheAgreedNumberOfTimes(int clubs, int meetings)
    {
        var season = RoundRobinScheduler.Schedule(Clubs(clubs), meetings, new Random(3));

        var counts = new Dictionary<(int, int), int>();
        foreach (var f in season.SelectMany(r => r))
        {
            var tie = f.Home < f.Away ? (f.Home, f.Away) : (f.Away, f.Home);
            counts[tie] = counts.GetValueOrDefault(tie) + 1;
        }

        int expectedTies = clubs * (clubs - 1) / 2;
        Assert.Equal(expectedTies, counts.Count);
        Assert.All(counts.Values, c => Assert.Equal(meetings, c));
    }

    /// <summary>
    /// An even number of meetings has to be exactly balanced — one game at each ground per tie.
    /// </summary>
    [Fact]
    public void AnEvenSeasonGivesEveryTieTheSameNumberOfGamesAtEachGround()
    {
        var season = RoundRobinScheduler.Schedule(Clubs(12), 2, new Random(11));

        var homeCounts = new Dictionary<(int, int), int>();
        foreach (var f in season.SelectMany(r => r))
        {
            var tie = f.Home < f.Away ? (f.Home, f.Away) : (f.Away, f.Home);
            homeCounts[tie] = homeCounts.GetValueOrDefault(tie) + (f.Home == tie.Item1 ? 1 : 0);
        }

        Assert.All(homeCounts.Values, c => Assert.Equal(1, c));
    }

    /// <summary>
    /// The regression this class exists for. On an odd number of meetings somebody must get the
    /// extra home game, and the wrong fix — reversing the whole third pass — hands it to the same
    /// four clubs every season. Every club must be capable of drawing it.
    /// </summary>
    [Fact]
    public void TheExtraHomeGameIsNotAlwaysTheSameClub()
    {
        var everGotTheExtraHomeGame = new HashSet<int>();

        for (int seed = 0; seed < 50; seed++)
        {
            var season = RoundRobinScheduler.Schedule(Clubs(8), 3, new Random(seed));

            var homeGames = new Dictionary<int, int>();
            foreach (var f in season.SelectMany(r => r))
                homeGames[f.Home] = homeGames.GetValueOrDefault(f.Home) + 1;

            // Each club plays 21 games, so a balanced club would host 10.5 — the extra host is
            // whoever is above that in this draw.
            foreach (var (club, hosted) in homeGames)
                if (hosted > 10) everGotTheExtraHomeGame.Add(club);
        }

        Assert.Equal(8, everGotTheExtraHomeGame.Count);
    }

    [Fact]
    public void AFixtureListIsReproducibleFromItsSeed()
    {
        var a = RoundRobinScheduler.Schedule(Clubs(8), 3, new Random(42));
        var b = RoundRobinScheduler.Schedule(Clubs(8), 3, new Random(42));

        Assert.Equal(a.SelectMany(r => r), b.SelectMany(r => r));
    }

    [Fact]
    public void ADivisionTooSmallToPlayProducesNoFixtures()
    {
        Assert.Empty(RoundRobinScheduler.Schedule(Clubs(1), 2));
        Assert.Empty(RoundRobinScheduler.Schedule([], 2));
    }

    [Fact]
    public void TheSameClubTwiceIsRejectedRatherThanScheduled()
    {
        Assert.Throws<ArgumentException>(() => RoundRobinScheduler.Schedule([1, 2, 2], 2));
    }
}
