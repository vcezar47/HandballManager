# Levelling twelve leagues against each other

Every club in the game has real players with rated attributes, and those ratings were assigned
country by country as each league went in. That produces twelve internally sensible leagues with
no agreed scale between them. A 15 in Croatia and a 15 in France were set by different judgement
calls months apart, and nothing in the game had ever asked them the same question.

Nothing *could*, until European competitions existed. A domestic table cannot measure one league
against another — it only ever compares clubs that already share a scale. Cross-border matches
are the only measurement instrument, which means adding the Champions League and the European
League was not just a feature, it was the tooling for the balance problem.

## The measurement

`BalanceReportTests` plays five full 2025/26 seasons with transfers switched off — so squads
cannot drift and every season asks the same question — and reports **only cross-border European
results** per country. It is gated behind `HM_BALANCE=1` and takes about five minutes a season,
which is fine for something run deliberately and never in CI.

Transfers off matters more than it sounds. With the market live, a strong league buys the good
players out of a weak one and the measurement starts describing the transfer model instead of the
ratings.

## The target

The intended tiers, cross-checked against the EHF's published country coefficients:

| tier | countries |
|---|---|
| strongest | Hungary, France, Romania, Denmark |
| better than average | Sweden, Norway, Germany |
| average | Slovenia |
| lower | Poland, Montenegro, Croatia, Türkiye |

With one deliberate divergence, recorded so it does not read as an error: the EHF ranks **Norway
first**, but that coefficient is very largely one club, Vipers Kristiansand, who have since
folded. Calibrating Norway to its published rank would model a league that no longer exists, so
it sits a tier lower.

## The lever

Every attribute enters the overall rating linearly, so adding *N* to all of a player's attributes
adds exactly *N* to the raw overall — which makes the correction arithmetic rather than a search.
Targets are set on the raw 1–20 scale and measured as the top-16 mean per club, sixteen being the
squad a club actually dresses.

Two things went wrong here that are worth having found.

**A constant delta rounds identically for every value**, so naively adding a float and rounding
gives a minimum effective step of one whole attribute point — four points of overall rating,
which is far too coarse to land a target. Carrying the rounding error forward between attributes
lets a fractional target actually be hit.

**Top-16 mean is the right measure for Europe and the wrong one for a title race.** The match
engine runs possessions through the starting seven, so results follow the seven, and the gap
between a club's seven and its sixteen ranged from 0.20 to 1.35 across European entrants. A
top-heavy squad beats its own squad rating badly — one club took four European League titles from
five seasons off a thoroughly mid-table squad rating, because its starting seven was the third
best in the field.

## Five seasons cannot resolve a title race

The most useful negative result. Between two runs **with no rating change at all**, one league
went from three different champions to the same club winning five out of five. A ×5 sweep is not
evidence of a broken league; it is what a close league looks like at n=5.

Eight or more seasons are needed before concluding anything about domestic dominance. Some
single-champion leagues are real — three countries have a genuinely dominant club and the
simulation is right to reproduce it — and the only way to tell those apart from noise is more
seasons, not more tuning.

## Result

All twelve countries land in their intended band. Leagues with a single repeat champion dropped
from 9 of 12 to 6 of 12. European trophies went from one club winning all five Champions Leagues
in a five-season run to eight different clubs winning across ten competition-seasons.

The verification step is worth naming too: after a bulk edit across 61 club files in 11
countries, the check is not reading the diff — a JSON diff realigns inside similar player blocks
and looks alarming when nothing structural changed. It is parsing both versions and comparing
player identities, key sets and club fields programmatically.
