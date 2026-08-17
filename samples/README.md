# Code samples

Two pieces of Handball Manager, lifted out of the engine and given their tests, so that a
reader can see the actual code rather than only descriptions of it. The rest of the source is
closed; **this directory is MIT licensed** (see [LICENSE](LICENSE)).

They are extracts, not toys. The scheduling and allocation logic is what runs in the shipped
game — what has been removed is the EF Core persistence, the competition metadata and the
season calendar, none of which is interesting to read.

| File | What it does |
|---|---|
| [`RoundRobinScheduler.cs`](RoundRobinScheduler.cs) | Builds a season's fixture list: any number of clubs, any number of meetings, byes where the division is odd |
| [`RoundRobinSchedulerTests.cs`](RoundRobinSchedulerTests.cs) | 9 test methods, 15 cases, several of which exist because of a specific bug |
| [`EuropeanPlaceAllocator.cs`](EuropeanPlaceAllocator.cs) | Splits 16 Champions League and 32 European League entries between countries by coefficient ranking |
| [`EuropeanPlaceAllocatorTests.cs`](EuropeanPlaceAllocatorTests.cs) | 10 test methods, 16 cases, mostly one property asked of a world that keeps changing shape |

## Why these two

Both are small enough to read in one sitting and both have a non-obvious requirement hiding in
them, which is the only reason a code sample is worth anyone's time.

**The scheduler** looks like a solved problem — the circle method is textbook. The parts that
took real work are the ones the textbook does not cover. A division with an odd number of clubs
needs a bye placeholder, and the consequence is that thirteen clubs take *thirteen* match days
while playing twelve games each; deriving the round count from `clubs - 1` drops a whole match
day and every other part of the game then believes it. And a league playing an odd number of
meetings cannot balance home and away — somebody gets the extra home game. Deciding that per
pass rather than per tie hands the same half of the division an extra home game every season,
which no fixture-counting test will ever catch. `TheExtraHomeGameIsNotAlwaysTheSameClub` is
that test.

**The allocator** is the opposite shape: trivial-looking arithmetic where the requirement is
the interesting part. Two competitions hold exactly 16 and 32 clubs, and that has to stay exact
however the coefficient ranking moves and however many countries exist in the game. Writing it
as a lookup table would pass every test on the twelve countries that ship today and break
silently on the thirteenth — a country missing from the table reads as a country with no
European place, which is not an error, just a nation whose champion never gets a game. Written
as bands plus a correction, the totals hold at any size, and the real consequence surfaces:
adding a country does not add places, it takes one off somebody else.

## Running them

The tests are xUnit and depend on nothing else:

```bash
dotnet new xunit -o SamplesTests
cp *.cs SamplesTests/
cd SamplesTests && dotnet test
```

All 31 cases pass on .NET 10.
