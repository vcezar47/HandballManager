# Making a career fast

The complaint was not that the game was slow. It was that it *got* slow — a day took about five
seconds early in a season and twenty to twenty-five by the end of one, and worse the longer a
career ran. That shape says the cost is proportional to something that accumulates, which is a
much more useful starting point than "optimise the simulation".

## Measuring first

`SimulationProfiler` lives in the test project and is inert unless `HM_PROFILE=1` is set. It
plays a career headless — no UI — and times each phase of each simulated day separately, so the
output is not "a day costs 947 ms" but where those milliseconds went.

The first run, over 330 days:

| phase | total ms | share |
|---|---:|---:|
| daily progression | 260,366 | **64%** |
| league matches | 67,296 | 17% |
| cup matches | 44,221 | 11% |
| trailing saves | 32,458 | 8% |

Player progression, not match simulation, was two thirds of a day. That was not the guess.

## The first two fixes

**The change tracker never let go.** EF Core tracks every entity it has loaded or written, and
`SaveChanges` walks all of them. Match rows were being written all season and never released, so
by June the context was tracking 161,372 entities and every save was paying for the entire
season's history. Detaching finished match rows fixed it.

**Training ran daily over everybody.** Player attribute drift was recalculating for all ~2,700
players in the world every single day, to produce a change small enough that it is invisible at
that resolution. It now runs weekly, on the same cursor the wage cycle uses.

| | before | after |
|---|---:|---:|
| 400 simulated days | 449.8 s | 166.1 s |
| avg day, first 20 | 947 ms | 322 ms |
| avg day, last 20 | 1,276 ms | 355 ms |
| tracked entities by June | 161,372 | 11,805 |

2.7x overall, and — more importantly — the gap between an early day and a late one went from
329 ms to 33 ms. The curve was flat.

## Then the world doubled

Five more countries went in, and match simulation became 70% of a day. A second profiler,
`ProfileMatchInternals`, timed the candidates inside a single match against a database with 120
days already in it:

| squad load (×2 per match) | form lookup (×2) | **`DetectChanges`** |
|---:|---:|---:|
| 1.37 ms | 0.33 ms | **23.7 ms per call** |

`DetectChanges` runs on every `SaveChanges` and is O(tracked entities). The tracked set had
crept back up, and the bulk of it was append-only rows — `Transaction` and `NewsItem` — that are
written and never read back through the tracker. Wages alone append one transaction per club per
week, across 145 clubs. Detaching those cut another 21% and brought tracking to 4,697.

A third pass found the same lesson in a different disguise: **`SaveChanges` costs the full
`DetectChanges` even when it writes nothing.** Four callers on the daily path saved
unconditionally on days with nothing to save, which was 3.5 pointless saves a day at ~33 ms each.

| | before | after |
|---|---:|---:|
| saves per day | 6.2 | 2.7 |
| implied `DetectChanges` per day | 207 ms | 103 ms |

The rule that came out of it, written down because it is not discoverable from the code: anything
added to the daily path must guard its save on having actual work.

## Two hypotheses that were wrong

Worth recording, because the reasoning was sound and the result still came back negative.

**The missing index.** `MatchRecords` is scanned twice per match to compute form, and had no
index — its team columns are plain integers with no navigation property, so EF's foreign-key
convention never covered them. Obvious win. Measured: 128,670 ms → 125,424 ms, pure noise.
SQLite scans a few thousand rows faster than it costs to care. The indexes were kept, but for
the dashboard queries that actually needed them, not for this.

**Storing less detail.** European competitions cost ~19% of a simulated day, and the natural
theory was the volume of detail being written — match events and per-player statistics for
~3,000 matches a season, around 80 rows each. A mechanism was built to keep only results for
matches outside the player's club, and measured: 1197 ms → 1112 ms, about 7%, inside run-to-run
noise. It was reverted. **The cost is deciding what happened, not writing it down** — the
possession loop, not the insert. Only reducing fidelity would move it, and full fidelity in
every league is the point of the game.

## What is left

`DetectChanges` still costs ~38 ms, and what it is walking now is mostly the ~2,700 `Player`
rows, which genuinely are read back. Detaching the unchanged ones at a day boundary would cut it
hard and is not obviously safe: anything holding a `Player` reference across the boundary would
lose its writes silently. That is a bad trade against a bug class nobody would ever reproduce, so
it stays on the list rather than in the code.

---

*All figures are headless. Real devices run roughly 20x slower and are dominated by UI redraw, so
treat these as a measure of engine work rather than as wall-clock times.*
