# The 2D match engine

The engine that plays a manager's own matches in Handball Manager 4.5, published as it ships.
About 9,600 lines of C#: the simulation in [`Core/`](Core/), the court renderer and match screen
in [`Mobile/`](Mobile/), and the tests that hold it to the rest of the game in [`Tests/`](Tests/).

> **Source-visible, not open source.** This folder is here to be read. It is not licensed for
> reuse — see [LICENSE](LICENSE). It also does not build on its own: it uses the game's domain
> model (`Team`, `Player`, `SquadSelection`, `MatchTactics`) and four small services
> (`MatchRating`, `TeamIntangibles`, `ManagerEffects`, `ConditionService`) from the closed core.
> The full source is available to reviewers on request.

## What it is

An agent-based simulation, not an animation of a dice roll. Every player is an `Agent` that
perceives, decides and moves ten times a second (`PhysicsMatch.Dt = 0.1`), and everything that
happens in a match — a pass picked off, a shot blocked, a keeper beaten at the near post, two
minutes for a late arm — comes out of geometry and attributes meeting on the court.

| | |
|---|---|
| Movement | Pace, acceleration, agility and stamina; every sprint costs energy, and energy is what the bench rotates on |
| Shots | Geometry: release point and height, aim error from finishing, long-throw and composure under pressure, blocks by arms actually in the path, and a keeper's reach against the ball's flight time |
| Passes | Lead, error, interceptions, contested catches and fumbles; a loose ball is chased |
| Decisions | The carrier prices shooting, passing, driving and holding the ball in expected goals, with noise fixed per possession so a player does not dither tick to tick |
| Defence | 6-0, 5-1, 3-2-1 and man-marking systems that slide, step out, help and close the gap behind a defender who left it |
| Rules | Referees who swap roles at each change of possession; free throws, two minutes and red cards, seven-metres for clear chances destroyed, passive play, substitutions through the zone, team time-outs, extra time and the seven-metre shoot-out |
| The other bench | An AI coach that reads the score and the clock: tempo, aggression, the defensive system against whatever is hurting it, time-outs to stop a run, and seven against six only when the game is close |

## Reading guide

`PhysicsMatch` is one class split across partial files by concern:

| File | What it holds |
|---|---|
| [`PhysicsMatch.cs`](Core/PhysicsMatch.cs) | Setup, the tick loop, restarts, time-outs, substitutions, the clock and the periods |
| [`PhysicsMatch.Attack.cs`](Core/PhysicsMatch.Attack.cs) | The carrier's decision and the 3:3 attack around her |
| [`PhysicsMatch.Defence.cs`](Core/PhysicsMatch.Defence.cs) | The four defensive systems and the keeper's positioning |
| [`PhysicsMatch.Shots.cs`](Core/PhysicsMatch.Shots.cs) | Aim, blocks, the keeper's reaction and reach, screening |
| [`PhysicsMatch.Ball.cs`](Core/PhysicsMatch.Ball.cs) | Flight, passes, catches, interceptions, goals |
| [`PhysicsMatch.Contact.cs`](Core/PhysicsMatch.Contact.cs) | Movement, collisions, duels and fouls |
| [`PhysicsMatch.Coach.cs`](Core/PhysicsMatch.Coach.cs) | The AI bench |
| [`Tuning.cs`](Core/Tuning.cs) | Every physical constant the engine is calibrated with, as an instance so a calibration run can try a variant beside the default |
| [`Match2DViewModel.cs`](Core/Match2DViewModel.cs) | The match screen: speeds, the replay of key moments at 16× and 32×, the feed, the bench controls |
| [`CourtView.cs`](Mobile/CourtView.cs), [`PlayersDrawable.cs`](Mobile/PlayersDrawable.cs) | The court and the players, drawn with `Microsoft.Maui.Graphics`, interpolated between ticks |
| [`ScoreboardStyle.cs`](Core/ScoreboardStyle.cs), [`TeamCodes.cs`](Core/TeamCodes.cs) | Each competition's TV-style scoreboard — four shapes, a palette per league, cup and supercup, the EHF's own — and the three-letter codes on it |
| [`ScoreboardView.cs`](Mobile/ScoreboardView.cs), [`BreakOverlay.cs`](Mobile/BreakOverlay.cs) | The board in the top bar, and the half-time and full-time screen it rises into with the clubs' full names |

## The problem that mattered: agreeing with the rest of the world

Only the manager's matches run on this engine. The thousands of other fixtures in a season are
simulated by the game's statistical engine, because a physics match takes a second or two to play and a
season has thousands of them. So the two engines have to play the same handball: if watched
matches were higher-scoring, or upsets rarer, the manager's league would be a different sport
from everyone else's.

The engine was first calibrated over hundreds of matches against the statistical one. Both were
then re-anchored to real women's handball, from seven competition-seasons of results, and the
physics is pinned there by a test (`TheEngineStaysCalibrated`). Over 300 matches in 4.5:

| per match | real women's handball | 2D engine |
|---|---:|---:|
| Goals | 55–59 | 56.6 |
| Shots from nine metres and beyond scored | about 38–42% | 40% |
| Keeper saves, of shots on target | about 30–35% | 33% |
| Seven-metres scored / saved | about 74% / 20% | 74% / 21% |
| Two-minute suspensions per team | 2–4 | 2.6 |

The harder target was the curve, not the averages: the home side's share of the points at every
gap in squad strength. The physics matched the averages early and the curve only late. The
stronger side won too reliably, because nothing in a deterministic physics model has an off
night. It matched once each match drew a day form for every player, every keeper and each team.
Keepers got the widest spread, as on a real court.

A later lesson came from the keepers. Testers saw backs score almost every nine-metre shot, and
keepers standing still. Giving keepers more speed to the ball did make long shots harder, but the
shooters, who price every option in expected goals, simply stopped taking them and kept only the
sure ones, which still went in half the time. Real backs shoot from distance because they back
themselves. So shooters now judge a keeper by the old, slower speed (`KeeperLimbSpeedAsRead`)
while the shot is resolved against the real one: long shots stay as common, and fail as often as
they should. At a seven-metre, where the ball arrives about as fast as a keeper can react, she
reads the taker and goes early, right or wrong.

## Things worth knowing

- **Fast-forwarding without missing anything.** Every tick is recorded into a ring buffer
  (`FrameShot`). At 16× and 32× the screen plays a key moment back at normal speed from a few
  seconds before it, then catches up. The view model guarantees the scoreboard and the feed
  never show a goal before the court does; `FastForwardStopsForTheMoments` checks it.
- **Skip to result is the same engine, headless.** `RunToEnd` plays the rest of the match with
  nothing drawn, so a skipped match and a watched one are the same match.
- **A bug that only showed in a scoreline.** INSTANT SIM once handed this engine the classic
  engine's tactics as they stood at *full time*. By then an AI side that was losing late had
  pulled its keeper, so it played all sixty minutes with an empty goal: 71–35, with the other
  keeper scoring ten. The engine was fine; its input was wrong. Starting sheets are now
  snapshotted at kick-off, and the AI bench ignores any standing order to empty its goal.
- **Determinism.** A seed reproduces a match exactly (`TheSameSeedPlaysTheSameMatch`), which is
  what makes the calibration runs and every regression above reproducible.

## Tools

- [`tools/replay_viewer.py`](tools/replay_viewer.py) turns a replay exported by the
  `ExportReplay` test into a self-contained HTML player, for looking at a match without a phone.
- [`tools/kit_colours.py`](tools/kit_colours.py) derives each club's shirt and number colours
  from its crest, with clash handling, and writes the generated table the engine reads (the
  generated file itself is left out here).
