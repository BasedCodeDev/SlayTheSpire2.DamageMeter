# Additive power review — preview.9

Target game v0.107.1. Amplification 3, log schema 6 and checkpoint schema 4
are unchanged. Ineligible callbacks return zero; eligible callbacks return the
captured signed Amount. All rows require a powered attack.

| Power | Additional native eligibility |
| --- | --- |
| Strength | Dealer equals power owner. |
| Accuracy | Dealer equals owner; non-null card has Shiv tag. |
| Vigor | Dealer equals owner; unrestricted command, or non-null card equals restricted ModelSource and dealer equals Attacker. |
| Calcify | Dealer's monster is Osty and dealer's PetOwner.Creature equals power owner. |
| Leadership | Dealer differs from owner and shares its combat side. Null dealer cannot earn credit; native exceptions are preserved. |
| Phantom Blades | Dealer equals owner; non-null Shiv card; no completed Shiv card play by owner happened this turn. |
| Tainted | Target equals power owner. |

Metadata confirms tag value 5 is Shiv. Vigor's internal command data/accessors
and Phantom Blades' private history predicate are checked against the installed
assembly. Context snapshots use object identity, not display names. Required
state failures and native output disagreements suppress amplification for the
resolution; they do not modify gameplay or actual HP/block accounting.

Pure branch/snapshot rule tests, history filtering, source/Contribution budgets,
Unknown provenance and synthetic patched nesting/exception cleanup are in
tests/AdditiveChecks.cs. These fixtures are synthetic. Native callback execution
and multiplayer eligibility/order remain separate verification work.

387 automated/installed-assembly checks passed. Release packaging succeeded;
the packaged DLL reports 0.3.0-preview.9. The legacy schema-3 trace fixture remains
readable. NuGet vulnerability metadata lookup emitted NU1900; cached dependencies
compiled successfully. No native/stable gate was promoted and the agent did not
deploy this package. Use README's host checklist with an unmodded guest.
