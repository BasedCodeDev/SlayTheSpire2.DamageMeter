# Weak/Vulnerable chain review — preview.8

Target: v0.107.1; amplification 3; log schema 6; checkpoint schema 4.
Installed method hashes and seed accessors are checked before credit is trusted.

| Callback | Eligibility, in addition to powered attack | Operation |
| --- | --- | --- |
| Vulnerable | target is power owner | dynamic DamageIncrease seed |
| Weak | dealer is power owner | dynamic DamageDecrease seed |
| Paper Phrog | target differs from relic owner's creature | input + 0.25 |
| Cruelty | target differs from power owner | input + Amount / 100 |
| Debilitate / Vulnerable | target is power owner | 2 × input − 1 |
| Paper Krane | target is relic owner's creature | input − 0.15 |
| Debilitate / Weak | dealer is power owner | 2 × input − 1 |

Ineligible roots return 1. Ineligible children return their input. Absent children
are omitted. Native parent order is Phrog/Cruelty/Debilitate for Vulnerable and
Krane/Debilitate for Weak. Expected model references are captured before the root
callback. Each child input/output and final parent result must reconcile.

Pure branch/replay coverage is in tests/MultiplierChainChecks.cs, including all
powered/owner combinations, absence, signed/zero Cruelty amounts, independent
provider shares, self/team Contribution, lethal/block budgets and corruption.
Fixtures in this suite are synthetic. Installed contracts cover the five newly
inventoried nested implementations plus the existing two root methods.

319 automated and installed-assembly checks passed; Release package/DLL version
is 0.3.0-preview.8. Legacy schema-3 fixture analysis still succeeds. Build reports
NU1900 because vulnerability metadata lookup is unavailable; cached dependencies
compile successfully.

Native bridge execution, callback eligibility/order in actual combat, multiplayer
role/scenario verification and stable release gates remain pending. This closes
the focused group's pure branch/replay work, not the broader catalogue or native
snapshot audit. Preview.8 was not deployed by the agent. Use README's host
checklist with an unmodded guest and retain event IDs for available scenarios.
