# One core, three heads

Handball Manager runs as a Windows desktop app, an Android app, and a Windows build of the
mobile UI used for testing. All three are thin. Everything that decides anything lives in one
project, `HandballManager.Core`, which references no UI framework at all.

| Project | Lines | Files | Contents |
|---|---:|---:|---|
| `HandballManager.Core` | 36,400 | 130 | 26 services, 39 entity types, **and every view model** |
| `HandballManager` | 14,100 | 91 | WPF views |
| `HandballManager.Mobile` | 8,800 | 96 | MAUI views, session plumbing, Android packaging |
| `HandballManager.Tests` | 5,400 | 24 | xUnit |

## The decision that mattered

The view models are in Core, not in either front end.

The desktop app was built first. When the mobile port started, the obvious plan was to write a
MAUI app against the same services — reuse the engine, write new UI and new view models for a
touch interface. That is the plan that produces two codebases which drift, because a view model
is where most of the small decisions live: what "next match" means when the season is over,
which button is disabled mid-simulation, what a split league's table shows.

Instead, the view models moved into Core with the services, and both heads bind to the same
objects. `HomeViewModel` is one class; the WPF window and the MAUI page are two different sets
of markup over it. A feature is wired once. When European competitions were added, the entire
Champions League and European League implementation — services, view models, qualification
logic — was written and tested before either UI existed, and adding it to mobile was one XAML
page.

The cost is real and worth naming: Core takes a dependency on `CommunityToolkit.Mvvm`, so it is
not a pure domain library, and a "model layer" that contains `ObservableProperty` attributes
will offend some readers. In exchange, two front ends stay in step with no synchronisation
effort, and 36,000 of the 60,000 lines are covered by tests that never open a window.

The split also made the test suite possible in its current form. Season-completion tests play
an entire season by calling the same commands the buttons call — `AdvanceDay`, `SimulateMatch`,
`ProcessEndOfSeason` — with no UI in the process at all. The tests exercise the real paths
rather than a headless approximation of them, because there is no separate headless path.

## Persistence

EF Core 9 over SQLite. Each save slot is its own database file, selected by setting
`HandballDbContext.DatabaseFileName` before the context is built, which is the whole of the
three-slot save system — no export format, no serialisation layer, and a save is a file the
player could copy.

The database is created with `EnsureCreated`, not migrations. That was the right call for a
single-player game with no server and no schema authority, and it has one consequence that has
to be handled forever: **a save file keeps the schema it was born with.** A table added to the
model after release simply does not exist in an older save, and querying it throws "no such
table" rather than returning nothing.

So `SchemaUpgrader` runs before the first query on any loaded save: idempotent
`CREATE TABLE IF NOT EXISTS` for every table added after a public release, and a helper for
columns, since SQLite has no `ADD COLUMN IF NOT EXISTS`. Every new column also needs a defensible
value for rows written before it existed — shots-faced defaults to 0, which reads as "no shots
faced" rather than as a bogus 100% save percentage.

## What a simulated day is

The engine advances the world one day at a time. On any date it plays every fixture in every
competition in the game — not just the player's — then ages and trains ~2,700 players, runs the
AI transfer market, pays wages, sells tickets, and writes the news and statistics behind all of
it. Twelve domestic leagues, twelve cups, five supercups and two European competitions are all
live at once, so a Tuesday in October is a real amount of work. What that costs, and what it
cost before it was profiled, is in [Making a career fast](performance.md).

One structural rule came out of that: a single `DbContext` is shared for the session, so
nothing may re-enter the engine while a day is in flight. The recurring bug shape was an
`async void` event handler — subscribing an async lambda to the game clock or a service event
means a second day can start on the same context while the first is awaiting, and the failure
surfaces later and elsewhere as a corrupted change tracker. Handlers are synchronous and queue
work; the simulation is behind a gate.

## Competition shape is data

Twelve countries do not play the same sport, administratively. Montenegro's eight clubs meet
three times. Slovenia's thirteen meet once — with a bye every round — then split into two groups
that carry points down by finishing position. Türkiye splits and carries each club's points in
full, so a group table where a club that won nine of twelve finishes second is correct. Poland
pays three points for a win and sends every drawn match to a shoot-out that leaves the scoreline
a draw. Sweden's play-offs are best-of-five and its cup quarter-finals are two-legged on
aggregate.

The engine is competition-agnostic where you would expect — fixtures, match simulation,
standings — and was hard-coded in a surprising number of places you would not. Wiring the twelfth
country was much faster than the fourth, and the difference is almost entirely predicates that
used to be `if (competition == Romania || competition == Denmark)` and are now lookups. That
particular pattern is worth grepping for in any codebase that grew a feature at a time: six
screens still tested for the two countries that existed when each was written, and every one of
them failed silently for the eight added since.
