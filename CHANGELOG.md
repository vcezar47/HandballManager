# Changelog

Dated update history. Downloadable builds are on the [Releases](../../releases) page.

## 3.1 — 31 August 2026 · The manager career, sound, and European nights

The largest release since launch, and the first one where the player is a person rather than a
club.

* **Coaching licences.** Five courses, Level 1 up to IHF Master, each costing months of the season
  and a fee out of your own balance. A licence gates which jobs will consider you
* **A manager contract of your own** — a wage paid monthly by the club, a personal balance, and
  career earnings that follow you from job to job
* **A board**, in its own tab on the Club screen. It sets objectives at the start of a season,
  grades you A+ to F every month against what a club of that size and squad should be doing, and
  sacks you if the grade stays bad long enough. You can ask for an extension; whether you get one
  depends on the same grade
* **A job market.** Vacancies open across all twelve countries as AI boards make their own
  decisions. Applications are answered in three days, a club waits five before looking elsewhere,
  and each summer moves coaches between clubs whether or not you are involved
* **Being out of work is still a career.** The squad and club screens go quiet, a Jobs tab appears,
  and continuous advance stops on the day a job you could take opens
* **Sound**, for the first time: a crowd atmosphere loop under a live match, three goal reactions,
  and a sting when a timeout is called. Off in one tap for anyone who would rather not
* **European nights.** The whole app — tab bar, cards, buttons, backgrounds — takes on Champions
  League or European League colours for as long as the club's next fixture is a European one, and
  drops back to its everyday palette the moment it is not
* **An intro film per competition**, played once a season before the club's first tie in it
* Existing saves gain all of it untouched: every column and table goes through the schema upgrader,
  and a career already in progress simply acquires a contract dated from the day it is loaded

Most of the work after 3.0 was in making the presentation behave on a real device rather than on
the bench. Three things were wrong at once and each hid the next: the film was recorded as shown
before it was displayed, so any failure spent it for the season; the navigation lock that stops a
tab opening mid-simulation was silently refusing the film's own attempt to close itself; and the
theme was being applied from the thread a save loads on, which on Android half-updates the screen
and leaves a career sitting on a background with nothing laid out over it.

## 2.7 — 24 August 2026 · Ads and an attendance fix

Monetisation, and a bug it surfaced along the way.

* Ads now play at two more natural breaks: the interval of a live match, and while a new career's
  world is being built. A single marked story in the news feed carries a third
* A one-time purchase removes every ad for good, and restores automatically on a reinstall or a
  new device
* Fixed: the pause after a live match read as the ad being slow to load. It never was — the ad is
  a few hundred kilobytes shown from cache in milliseconds. What actually took several seconds was
  the rest of that day's fixtures simulating behind it, which used to run *after* the ad rather
  than underneath it
* Fixed: a live match's attendance and home advantage were guessed separately from a simulated
  one's — a flat 80% of capacity for the crowd, a bare constant for the advantage — and could show
  more people in the stands than the stadium holds. Both now come from the same model a simulated
  match uses, rolled once before kick-off, and a watched match now pays gate receipts at all,
  which it never had before

## 2.2 — 22 August 2026 · Post-launch update

The first round of feedback after release, and what it turned up.

* Fixed: a "Match Result" alert could appear over the match report after a live match. The result
  had already been recorded — what failed was a *second* attempt to record it, from a CONTINUE
  button that stayed enabled through several seconds of work and a home screen reload nobody
  awaited. Both routes are now gated on the shared `DbContext`
* Fixed: contract renewals and academy signings accepted any wage, including nothing at all.
  Contract talks had only ever been wired into the transfer negotiation screen, so re-signing
  your own squad asked the player nothing
* Fixed: a club with fewer than fourteen fit players could never reach the live match — entering
  the arena demanded seven substitutes as well as the starting seven. Four seeded clubs were
  affected, and could only ever use instant result
* Wage demands move with form: this season's average match rating once there are five games to
  read it from, last season's completed record before that
* What a player asks now depends on who is asking. The gap is measured against the higher of the
  club she already plays for and what her ability entitles her to, so both being asked to drop a
  level and being too good for the buyer cost the same premium
* Wages, gate receipts, prize money and every club's budgets drift up 3% a season, so the numbers
  grow without the economy moving underneath them. Contracts already signed are never re-priced,
  which is what makes a long deal for a good player worth having
* AI clubs pay the demands the player is quoted, rather than a random share of the old estimate
  that took no account of form, inflation or their own standing
* The wage budget is enforced when signing or renewing — it had been a number on the finances
  screen that nothing checked

## 2.1 — 18 August 2026 · Advertising

* Interstitial ads, placed between the final whistle and the match report rather than interrupting
  play
* Privacy policy published for the Play listing

## 2.0 — 16 August 2026 · Launch update

Out of Google Play open testing and fully released.

* European competitions: EHF Champions League and EHF European League — group stage, knockout
  rounds, neutral-venue finals
* EHF country coefficient rankings deciding each nation's places
* The twelve leagues levelled against each other on cross-border results, so a tie between two
  countries lands where it should
* Club reputation now derived each summer from honours and recent standing instead of being fixed
  at seed time
* Contract talks hold their own state: a player fixes what she will sign for and loses patience
  with every offer that comes in short
* One-move-a-season enforced everywhere, so an AI club can no longer bid for a player who has just
  signed and have the offer accepted with nothing happening
* Free agent pool no longer grows without bound — youth intake was outrunning retirement
* Fixed: the home screen offering a match that had just been played
* Fixed: the wage budget being cuttable below the squad's own wage bill
* Fixed: an empty trophy cabinet vanishing instead of saying so
* Fixed: AI managers showing a blank birthplace
* Tablets and Chromebooks get a centred content column

## 1.4 — 10 August 2026 · Google Play open testing

* Poland competitions added (ORLEN Superliga Kobiet, Puchar Polski, Superpuchar)
* Türkiye competitions added (Kadınlar Süper Lig, Kadınlar Kupası, Süper Kupa)
* Fixed the app occasionally reporting as "not responding" during simulation
* Scouting assignments and shortlist now survive a crash or app restart

## 4 August 2026 · Four new countries

* Slovenia, Croatia, Montenegro and Sweden competitions added (league and cup each)
* Youth name pools doubled to 40 first names and 40 surnames for every country
* Ticket sales now earn money — attendance was simulated but never paid out
* Finances tab: gate receipts, wages and a season summary by category
* Squad numbers can be handed out by hand from a player's profile, and new signings and youth
  graduates get a number that suits their position instead of the lowest one free
* Every league now archives a full 1st / 2nd / 3rd for seasons you play
* Fixed: cup finals could be scheduled on the same day as a league round
* Fixed: a new Slovenian season started with the previous season's split points still on the table
  (existing saves repair themselves)

## 28 July 2026 · Germany

* Germany competitions added (Bundesliga, DHB-Pokal, DHB Supercup)
* Supercup winners now earn prize money
* German and Norwegian youth intakes now produce German and Norwegian names

## 26 July 2026 · Norway

* Norway competitions added (REMA 1000-ligaen, Norgesmesterskapet)
* Dedicated play-off tab for the Norwegian league
* Nationality flags in squad and player views
* Attributes highlighted by player position
* Fixed: missing Positioning attribute, drawn cup knockout ties

## 12 July 2026 · Mobile port

* Full .NET MAUI port for Android, sharing the entire model and view-model layer with the desktop
  build

## 8 July 2026 · July update

* Club facilities — training and youth
* Competitions award prize money by position and trophies won
* Save / load game
* Bug fixes

## 10 May 2026 · Denmark

* Denmark competitions added (Kvindeligaen, Landspokalturnering, SuperCup)

## 3 May 2026 · France

* France competitions added (Ligue Butagaz Energie, Coupe de France)
* Realistic budgets for all clubs

## 29 April 2026 · Hungary

* Hungary competitions added (NB I, Magyar Kupa)

## 12 April 2026 · April update

* Squad selection before games
* In-depth match simulation — time-outs, substitutions
* UI improvements

## 29 March 2026 · Major update

* Managers and manager creation
* Dynamic stadium attendance, based on club form, competition and game importance
* Updated club info UI
* Personal manager profile tab
* Neutral venue logic for cup and supercup final fours
