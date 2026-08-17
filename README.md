# Handball Manager <img src="HandballManager/Assets/gamelogo/colorpng.png" height="30">

A women's handball management simulation for Android and Windows, live on Google Play. Twelve
national leagues and two European competitions run inside one world model — every club in every
country plays its own season, whether or not the player ever looks at it.

Built solo, end to end: simulation engine, persistence, two front ends, the test suite, the data
collection, and the store release.

[**Google Play**](https://play.google.com/store/apps/details?id=com.handballmanager.mobile) ·
[Releases (APK / Windows)](../../releases) ·
[Engineering notes](#engineering-notes) ·
[Code samples](samples/)

> The engine is closed source ahead of a commercial release. This repository holds the builds,
> the write-ups, and two MIT-licensed extracts in [`samples/`](samples/).

---

## Stack

| | |
|---|---|
| Language / runtime | C# 13, .NET 10 |
| Mobile | .NET MAUI (Android, API 24+) |
| Desktop | WPF (Windows) |
| Persistence | EF Core 9 over SQLite, `SQLitePCLRaw` bundled for Android |
| UI pattern | MVVM, `CommunityToolkit.Mvvm` source generators |
| Tests | xUnit |

## Shape of the codebase

| Project | Lines | Files | What it is |
|---|---:|---:|---|
| `HandballManager.Core` | 36,400 | 130 | Simulation, persistence, and **every view model**. References no UI framework. |
| `HandballManager` | 14,100 | 91 | WPF desktop head — views only |
| `HandballManager.Mobile` | 8,800 | 96 | .NET MAUI head — views, session plumbing, Android packaging |
| `HandballManager.Tests` | 5,400 | 24 | xUnit; 84 tests, most of them full-season integration runs |

26 services and 39 entity types in Core. Putting the view models there rather than in either
front end is the decision the whole project rests on: both heads bind to the same
`HomeViewModel`, so a feature is wired once instead of twice, and the desktop app became a mobile
port without the model layer being touched. The trade-offs are in
[Architecture](docs/architecture.md).

## The simulation

The world advances one day at a time. On any date the engine plays every fixture in every
competition, ages and trains ~2,700 players, runs the AI transfer market, pays wages, sells
tickets, and writes the news and statistics behind all of it.

Three problems that took the most work:

- **Competition shape is data, not code paths.** Twelve countries do not play the same game
  administratively. Montenegro's eight clubs meet three times. Slovenia's thirteen meet once —
  one club idle every round — then split into two groups carrying points down by finishing
  position. Türkiye splits and carries each club's points in full, so a group table where a club
  that won nine of twelve finishes second is correct. Poland pays three points a win and settles
  every drawn match with a shoot-out that leaves the scoreline a draw.
- **The default failure mode is silence.** An eleven-club league generates no fixtures rather
  than throwing. A competition with no prize-money tier pays nothing to anyone, forever. A country
  with no youth name pool quietly produces Romanian teenagers. Most of the work on each new
  country goes into turning that class of bug into a test that fails loudly instead.
- **European qualification has to stay exact.** Sixteen and thirty-two clubs, however the
  three-season coefficient ranking moves and however many countries exist — nine guaranteed
  Champions League places plus an incentive place and wild cards walked back down the ranking,
  and banded European League places with the surplus taken off the weakest countries but never
  below one. Written as rules plus a correction rather than as a lookup table, which is why the
  totals hold when a country is added — and why adding one costs an existing country a place.
  That allocator is in [`samples/`](samples/EuropeanPlaceAllocator.cs) with its tests.

## Performance

Simulating a day is the hot path, and it degraded over a career rather than being slow up front —
the more interesting kind of problem. `SimulationProfiler` (in the test project, inert unless
`HM_PROFILE=1`) times each phase of a day against a database with real mileage on it.

| | before | after |
|---|---:|---:|
| 400 simulated days | 449.8 s | 166.1 s |
| avg day, first 20 | 947 ms | 322 ms |
| avg day, last 20 | 1,276 ms | 355 ms |
| EF-tracked entities by June | 161,372 | 11,805 |

The causes were an EF change tracker that never released finished match rows, so every save
walked a graph that grew all season, and player training running daily across every player in the
world instead of weekly. Later passes found `DetectChanges` costing 23.7 ms per call — against
1.37 ms for a squad load — and four callers saving unconditionally on days with nothing to save.

Two plausible hypotheses measured *negative* and were dropped: indexing the match table (noise),
and storing less match detail for other countries' games (~7%, inside run-to-run variance — the
cost is deciding what happened, not writing it down). Full write-up:
[Making a career fast](docs/performance.md).

## Testing

84 xUnit tests, deliberately weighted towards integration:

- **Season-completion tests** hand the player a random club, play a whole season through the real
  view-model commands, then assert that *nothing anywhere in the world* is left unplayed. It
  exists because the world-simulation loop once looked only for the player's own supercup,
  leaving two other countries' finals unplayed all season with no error anywhere.
- **Seed-data tests** enumerate the embedded club data and check squad shape, position-correct
  attribute blocks, present crests and arena photos, prize tiers for every competition, real head
  coaches, and that nothing seeded narrates a season the player is about to play.
- **Fixture-clash tests** prove, without simulating anything, that no competition can schedule a
  knockout tie onto a date its own league already uses.
- **Balance-report tests** produce the cross-league comparison described below.

The full suite plays roughly twenty seasons of a twelve-country world and runs over an hour,
which is its own design constraint — the seed-data subset runs in about 20 seconds for the
iteration loop.

## Data

145 clubs and 2,698 players with real squads, birthdates, positions, shirt numbers, heights and
head coaches, assembled from twelve national federations plus the EHF. Very little of it was
offered as a download; most came out of undocumented JSON behind Angular and Vue front ends,
JavaScript object literals that are not quite JSON, and base64 PDF match reports.

Fetching it is the easy half. Federation sites drift to the current season while the game is
pinned to 2025/26; one public table was two seasons stale with nothing on the page saying so;
one federation registers a goalkeeper born in 2015 with eleven appearances; one reigning champion
enters its league with an under-18 side and fields its internationals only in the final. Where
positions are unpublished, shot-zone distributions classify them at 100% — but only where zone
data exists, since on goals and shots alone the best classifier managed 45% against a 35%
baseline. Full write-up: [Building a world from federation data](docs/data-pipeline.md).

## Balance

Twelve leagues rated country by country over several months have no agreed scale between them,
and a domestic table cannot measure one league against another. Cross-border European matches are
the only instrument, so the European competitions doubled as the tooling for the balance problem:
five seasons with transfers off, cross-border results only, targets set on the raw attribute
scale.

The most useful result was negative — between two runs *with no rating change at all*, one league
went from three different champions to the same club winning five from five. Five seasons cannot
resolve a title race, and a clean sweep is not evidence of a broken league.
[Levelling twelve leagues](docs/balance-calibration.md).

## Engineering notes

- [**One core, three heads**](docs/architecture.md) — the project split, why the view models live
  in Core, `EnsureCreated` versus migrations and what that costs forever, and the re-entrancy rule
  a shared `DbContext` imposes.
- [**Making a career fast**](docs/performance.md) — profiling a simulated day, the fixes worth
  2.7x, and two well-reasoned hypotheses that measured as noise.
- [**Building a world from federation data**](docs/data-pipeline.md) — sourcing 2,698 players,
  the four ways the data lies, and the validation suite that rejects it.
- [**Levelling twelve leagues**](docs/balance-calibration.md) — measuring leagues against each
  other, and how many seasons it takes before a result means anything.

## Code samples

Two pieces of the engine, extracted with their tests and MIT licensed — [`samples/`](samples/):

- [`RoundRobinScheduler`](samples/RoundRobinScheduler.cs) — a season's fixture list for any number
  of clubs and any number of meetings, with byes where the division is odd
- [`EuropeanPlaceAllocator`](samples/EuropeanPlaceAllocator.cs) — splitting 16 and 32 European
  entries between countries by coefficient ranking

## The game itself

<details>
<summary>Twelve countries, 31 competitions</summary>

| | Country | Competitions |
|---|---|---|
| <img src="HandballManager/Assets/flags/romania.png" height="14"> | Romania | Liga Florilor · Cupa României · Supercupa României |
| <img src="HandballManager/Assets/flags/hungary.png" height="14"> | Hungary | NB I · Magyar Kupa |
| <img src="HandballManager/Assets/flags/france.png" height="14"> | France | Ligue Butagaz Energie · Coupe de France |
| <img src="HandballManager/Assets/flags/denmark.png" height="14"> | Denmark | Kvindeligaen · Landspokalturnering · SuperCup |
| <img src="HandballManager/Assets/flags/norway.png" height="14"> | Norway | REMA 1000-ligaen · Norgesmesterskapet |
| <img src="HandballManager/Assets/flags/germany.png" height="14"> | Germany | 1. Bundesliga Frauen · DHB-Pokal · DHB Supercup |
| <img src="HandballManager/Assets/flags/slovenia.png" height="14"> | Slovenia | 1. SRL ženske · Pokal Slovenije |
| <img src="HandballManager/Assets/flags/croatia.png" height="14"> | Croatia | 1. HRL Žene · Hrvatski kup |
| <img src="HandballManager/Assets/flags/montenegro.png" height="14"> | Montenegro | Prva liga · Kup Crne Gore |
| <img src="HandballManager/Assets/flags/sweden.png" height="14"> | Sweden | Handbollsligan · Svenska Cupen |
| <img src="HandballManager/Assets/flags/turkey.png" height="14"> | Türkiye | Kadınlar Süper Lig · Kadınlar Kupası · Süper Kupa |
| <img src="HandballManager/Assets/flags/poland.png" height="14"> | Poland | ORLEN Superliga Kobiet · Puchar Polski · Superpuchar |
| <img src="HandballManager/Assets/flags/europe.png" height="14"> | Europe | EHF Champions League · EHF European League |

</details>

Squad selection and in-match management, contract and transfer negotiation, scouting, youth
intake and development, club facilities, finances down to gate receipts, dynamic attendance, and
a full honours history for every club. Version history is in [CHANGELOG.md](CHANGELOG.md).

## License & copyright

**© 2026 Vieru-Potecu Cezar-Mihai. All rights reserved.** No redistribution, no commercial use;
all rights to the match engine, simulation logic and branding are reserved. The `samples/`
directory is MIT licensed — see [samples/LICENSE](samples/LICENSE).

## Disclaimer

Handball Manager is a fan-made project and is not affiliated with, endorsed by or connected to
the EHF, any national handball federation or league, or any club or player represented in it.
