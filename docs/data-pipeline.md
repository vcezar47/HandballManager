# Building a world from federation data

Handball Manager ships 145 clubs and 2,698 players with real names, birthdates, positions,
shirt numbers, heights, nationalities and head coaches, across twelve countries. Women's club
handball has no equivalent of a football data provider, so all of it was assembled from national
federations and the EHF, and almost none of it was offered as a download.

The fetching is the easy half. The half that took the work is deciding what to believe.

## Where it comes from

Most federations run a public site over an API they never advertised. The Turkish federation
serves unauthenticated JSON behind an Angular front end — fixtures, tables, full match-day squads
with shirt numbers, benches including head coaches, and every goal and suspension — and it 403s
on a default Python user agent and works fine with a browser one. The German federation serves
squads and per-match statistics as static JSON behind a Vue front end that renders nothing to a
plain fetch. Several Balkan federations share one back end that publishes a per-player shot-zone
breakdown. The Croatian federation returns a JavaScript object literal that is *nearly* JSON and
will not parse as it. Official match reports arrive as base64-encoded PDFs, which is where hall
capacity and attendance live.

The EHF's own club API is the best-shaped source of all — exact playing position, dual
nationalities as an array, birthdate, height, shirt number — and covers only clubs that played
in Europe that season, so it is a partial source by construction.

## Four ways the data lies

**It drifts to the present.** The game is pinned to the 2025/26 season and real websites are
not. A squad list fetched in August 2026 is the *2026/27* squad; one club's roster contained
three players who had spent the modelled season elsewhere. The rule that came out of this: take
only season-invariant fields — birthdate, height — from an undated source, and never club
membership. A player's position is usually safe from an undated page; her employer never is.

**A published table can be years stale with nothing saying so.** One federation's own page for
its women's first division rendered the 2023/24 table, two seasons old, with no date anywhere on
it. The live data was there under a different competition id. Two seasons is enough to change
which clubs are in the division: the team that had won it unbeaten no longer existed, and two
clubs in the real division never appeared on the page at all. Check a date before trusting a
table, and if a federation exposes ids, scan them rather than assuming the page links to the
current season.

**Registration data is not the team that played.** One reigning champion enters its domestic
league under a first-team name and fields a side born entirely between 2007 and 2009, keeping
its internationals for Europe; the senior team appears only in the two-legged final. Taking the
league statistics at face value would have seeded the champions as an under-18 side. The tell is
a squad whose birth years cluster inside three seasons.

**Federations register impossible ages.** One league had two players carrying a 2024 birth year
across three separate files, and a second-choice goalkeeper recorded as born in 2015 with eleven
appearances. In a league whose median age is genuinely seventeen there is no way to separate the
typos from the very young, so the rule is a hard floor enforced by the test suite, plus a check
that dropping those players does not leave a club with fewer than two goalkeepers — which it did
in one case, where the honest fix was promoting the third-choice keeper who had actually played,
not inventing a birthdate.

## Inferring what is not published

Many federations record a generic "back" rather than left, centre or right, and some record no
position at all. Where a source publishes shot locations, position can be recovered from the
mix — a wing shoots from the wing, a pivot from six metres with no nine-metre attempts, a back
from nine — and classifying on that scored **100%** against squads whose positions were known.

Where only goals, shots and suspensions exist, the best classifier managed **45%** against a 35%
baseline. That is the more useful of the two results: it says do not attempt inference at all
without zone data, and it is the kind of negative number worth measuring before building on an
assumption.

Even with zone data, the classifier must not set the squad *shape*. Its thresholds are calibrated
on clubs that play well; at the bottom of the table nobody works the ball inside, everybody
shoots from distance, and an entire squad comes back classified as backs. Players are ranked by
how well they fit each position and then fitted to a realistic shape instead.

## The validation suite

Seed data is checked by tests rather than by review, because it is 145 files and reviewing them
does not scale. The seed tests enumerate the embedded club data and assert that:

- every club has a plausible squad shape, and goalkeepers carry keeper attributes while
  outfielders carry outfield ones — mixing the blocks is not a compile error and looks fine;
- every club has a crest, an arena photo, a flag and facility levels, and every competition has
  a prize-money tier, with the champion out-earning fifth and bottom-half clubs still paid;
- every seeded nation has its own youth name pool, because a missing one silently falls back to
  Romanian names and the country's academies quietly produce Popescus forever;
- every club has a real head coach — a club with no coach in its file gets an invented one, which
  looks like a finished league and is how twelve Swedish clubs shipped with fictional benches;
- no seeded honour or club description narrates a season the player is about to play. Squads are
  the real 2025/26 squads, but a new career starts in June 2025, and forty-eight club profiles
  had cheerfully described who would win the league and who would go down, in the past tense,
  before a ball was thrown.

Every one of those assertions exists because the corresponding mistake shipped first.

## A note on tooling

Bulk-editing the club files with PowerShell 5.1 double-encodes UTF-8 and turns every diacritic
into mojibake across all 145 files at once — a single-character corruption in club and player
names in twelve languages. Edits go through Python with an explicit encoding, and the check
afterwards is grepping for `Ã`. Similarly, never reserialise the JSON to make a small change: the
files disagree about line endings, so a round-trip rewrites every one of them and buries the
actual edit in a 145-file diff.
