# Attack multiplier review — preview.11

Installed target: v0.107.1. Ineligible observations return multiplier 1.
All seven rules require a powered attack.

| Power | Additional eligibility | Result |
| --- | --- | --- |
| Double Damage | Card exists; dealer is owner or appears in owner's Pets | 2 |
| Lethality | Card owner matches; no repeated in-play index; started attacks this turn <= 1 in play, otherwise <= 0 | 1 + Amount / 100 |
| Gigantification | Card owner matches; no restricted command or identical model source | 3 |
| Shrink | Dealer is owner | (100 - DamageDecrease.BaseValue) / 100 |
| Slow | Target is owner | 1 + 0.1 * SlowAmount.BaseValue |
| Tracking | Card exists; owner/pet dealer; target has Weak | Amount |
| Knockdown | Target is owner; dealer differs from Applier, including null identity comparisons | Amount |

Lethality counts CardPlaysStarted, filtered by native turn predicate, Attack card
type and card owner's creature. The current card can already be included once.
Its private predicate hash is pinned separately. Metadata confirms PileType.Play
is value 5 and CardType.Attack is value 1. Dynamic and command accessors are
checked; typed snapshots preserve signed Amount and actual provider provenance.
No gameplay modifier is invoked again during expected-output calculation.

454 checks pass, including seven rule branches, history filters, fixed factors,
dynamic-vs-Amount cases, pets, commands, provenance/Contribution budgets,
diagnostic corruption and all existing checks. The preview.10 negative control
and production native-shaped output-list test both pass. These cases are
synthetic/metadata tests; native bridge and role/scenario verification remain
pending. The older native trace fixture remains readable by TraceReport.

Release ZIP and DLL identify 0.3.0-preview.11. Amplification 3, log schema 6 and
checkpoint schema 4 are unchanged. No native/stable gate was promoted; the agent
did not deploy or restart the game. Use README's host checklist with an unmodded
guest, beginning with the first-attack divergence retest.
