# Provider-owned amplification — preview.13

Main damage ownership now follows the agreed policy for all numeric amplification.
Raw engine observations and their original owner totals remain separate.

| Case | Credited result |
| --- | --- |
| 6 attack, 50% Vulnerable, ample HP | attacker 6; provider 3 |
| Providers own 2:1 active stacks | the 3-point bonus splits 2:1 |
| Attacker owns the amplification | same player gets 9, with 3 orange |
| Enemy has 4 HP | attacker 4; provider 0 |
| Enemy has at least 9 block | attacker 6 block; provider 3 block |
| Enemy has 7 block | attacker 6 block; provider 1 block and 2 HP |
| Shared poison, amplified from 6 to 9 | base 6 follows poison ownership; bonus 3 follows amplifier ownership |

Credited records retain canonical event ID, damage kind, provider/application
source and triggering source. Support is a subset, not another addition to
Damage. Unknown attribution stays explicit. Unsupported traces preserve actual
ownership provisionally. Doom remains confirmed execution-only damage.

497 automated/installed-assembly checks passed; Release ZIP and DLL identify
0.3.0-preview.13. These include credited accounting, source/Support conservation,
self/shared/Unknown providers, effect views, deduplication, schema-5 restoration,
legacy actual-history preservation and schema-7 event/summary reconciliation.
The older native trace fixture remains readable. Existing dispatch and production
out-list regressions also remain in the package gate.

Attribution version 1, checkpoint schema 5 and log schema 7 are new. Actual HP
accounting remains 2 and amplification remains 3. Legacy credited history is
excluded rather than guessed; Actual Damage retains compatible earlier totals.
Credited Damage, Amplification and Support use the same coverage boundary.

Native multiplayer, UI rendering and release gates remain pending. The agent
did not deploy or restart the game. Use README's preview.13 host checklist with
an unmodded guest; retain canonical event IDs for inspected cases.
