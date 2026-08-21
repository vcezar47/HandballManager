# Changelog

Dated update history. Downloadable builds are on the [Releases](../../releases) page.

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
