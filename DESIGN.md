# Multiplayer Damage Meter - Design and Implementation Plan

Status: preview.18 source; preview.17 withdrawn after save-loading failure and reported progress loss. Native startup and persistence verification remain pending.

### Slice 1 boundary

The first code slice provides a DLL-only diagnostic mod, fight-only HP/block/poison/Doom totals, a movable F8-toggle panel, JSONL events, power-change provenance for poison/Doom/Vulnerable, and an explicit package script. Slice 2 now allocates observed shared poison/Doom ownership proportionally; only unknown shares remain Unattributed. No run totals, persistence, support-damage calculations, or public publishing are implemented. See README.md for the live verification checklist. Automated checks exercise standalone accounting; they do not establish runtime hook coverage or multiplayer correctness.

## 1. Create the design document first

The **first implementation turn** must create `DESIGN.md` in the project root containing this agreed plan. Do not implement code during that turn.

Use `DESIGN.md` as the implementation's source of truth. Update it when decisions change, distinguishing agreed requirements from unresolved questions and verified behaviour.

## 2. Foundation and architecture

Build a public in-game mod for Windows, Linux, and macOS that tracks the whole multiplayer party locally. Only the viewer needs it installed; no custom networking is required.

- Adapt the MIT-licensed [DamageTracker](https://github.com/BAIGUANGMEI/STS2-DamageTracker) overlay and relevant [tSC-DataModel](https://github.com/latentspacez/tSC-DataModel) attribution code. Preserve licences and pin imported revisions.
- Package one independently named mod without a separate framework dependency.
- Use C#/.NET 9, Godot controls, and narrowly scoped Harmony observation patches. Initially target installed game version **v0.107.1**, verifying supported versions before release.
- Separate event capture, accounting, persistence, and presentation. Capture run/combat identity, sequence, actor, recipient, source, actual values, modifier provenance, and stack ownership.
- Mark the mod non-gameplay-affecting. Preserve combat state, choices, random-number usage, and action ordering.
- Formally support base-game content. Retain observable totals for unknown modded effects and label missing attribution.

## 3. Interface and accounting

### Meter interface

Use **one movable, resizable, collapsible panel**:

- **Current Fight / This Run** tabs; default to Current Fight.
- Header metric selector: Damage, Support Damage, Contribution, Block Generated, Block Used, Healing, Damage Taken, Energy, and Card Draw.
- Fixed-width player-name column, aligned bar origins, and a common scale against the largest displayed total.
- One selected-metric total centred over each full bar track, including zero; party percentage remains at the right.
- Hover for selected-metric source and recipient breakdowns. Info exposes help and diagnostics.
- Retain completed-fight statistics until the next encounter. Size the panel to the party.
- Persist layout and selections. Provide a configurable show/hide shortcut, defaulting to F8.
- Use patterns or labels alongside colour.

The Damage view displays mutually exclusive segments:

| Colour | Meaning |
|---|---|
| Blue | Enemy block consumed, excluding orange |
| Red | Enemy HP removed, excluding orange |
| Orange | Effective damage enabled by amplification effects such as Vulnerable |

Orange **replaces** its portion of blue/red. A 15-HP hit containing 5 amplification displays **10 red + 5 orange = 15**.

Label the combined total **HP + Block**. Tooltips expose full HP damage, block damage, amplification, and orange's HP/block split. Poison and Doom are HP damage, not automatically orange.

### Damage and status ownership

- Record actual resolved HP loss and block absorption separately. Exclude overkill from credited damage.
- **Poison:** credit actual HP removed per resolved tick, capped by remaining HP. Track stacks applied separately.
- **Doom:** credit only an actual Doom execution, using remaining HP immediately before execution. Applying Doom or predicting lethal damage earns no HP damage.
- Divide shared poison/Doom damage proportionally by active owned stacks. Maintain ownership through applications, decay, removal, transfers, and restoration.
- When B doubles A's stacks, A owns the original stacks and B owns the additional stacks.
- Explicit triggers do not transfer existing stack ownership; retain the triggering action in source details.
- Count each event once, including Doom deaths that bypass ordinary damage hooks.
- Replace upstream single-`Applier` poison/Doom attribution with the ownership ledger.

### Support and other metrics

- Main Damage credits unamplified damage to original owners and effective numeric amplification to its providers. Actual Damage preserves original observed ownership. Orange follows provider-owned amplification.
- Show Support Damage separately, credited to modifier providers. Support Damage is already included in credited Damage and must never be added twice. Contribution is replaced by Actual Damage.
- Calculate effective amplification from captured modifier inputs without replaying gameplay. Respect block, remaining HP, and rounding; allocate overlapping benefits proportionally without inflating totals.
- Track actual energy granted, extra cards drawn, effective healing, and block granted in their own units. Include provider, recipient, source, and successful recipient-event counts.
- Exclude normal turn draw from support totals. Exclude overhealing and failed gains.
- **Do not assign downstream damage credit to donated energy, draws, or gifted cards.** Remove corresponding upstream reassignment behaviour.
- Track block generated separately from block consumed. Attribute consumption to provider-owned grants in grant order, documented as an accounting convention.
- Track actual player HP damage taken, identifying self-inflicted damage separately.

## 4. Persistence and implementation sequence

Persist statistics atomically in writable user data, separate from game saves. Match profile/run/checkpoint identity when restoring, including effect ownership. Replayed combat attempts replace earlier attempt data; missing history or provenance produces a visible partial-data indicator.

After the document-only first turn:

1. **Prove multiplayer observation:** verify remote damage, sources, and support as host and guest with unmodded teammates.
2. **Implement accounting:** establish metrics, ownership, amplification, and reconciliation tests.
3. **Complete UI and restoration:** implement the panel, breakdowns, preferences, summaries, and checkpoint-aware persistence.
4. **Validate and prepare releases:** package GitHub ZIP and Steam Workshop distributions with installation instructions, supported versions, accounting rules, and limitations.

Keep capture lightweight and refresh visible bars at most ten times per second. Tracking failures must preserve gameplay and visibly mark affected statistics incomplete.

## 5. Acceptance and release checks

- **20 damage against 10 HP:** credit 10 HP damage.
- **20 damage against 5 block and 10 HP:** credit 5 block damage and 10 HP damage.
- Verify poison overkill, repeated ticks, shared ownership, doubling, transfers, and decay.
- Verify Doom application without execution, execution after prior damage, shared ownership, explicit triggers, and simultaneous deaths.
- Confirm orange segments reconcile to totals and support credit never duplicates damage.
- Cover multi-hit/multi-target attacks, summons, orbs, retaliation, overhealing, failed draws, player choices, and simultaneous actions.
- Test host/guest operation with unmodded teammates in 2â€“4 player parties, reconnects, save/resume, replayed encounters, and consecutive runs.
- Check small segments, long names, screen resizing, mouse/keyboard interaction, and partial-data states.
- Recruit macOS/Linux testers and require real gameplay and multiplayer results before marking those platforms verified.
- Prepare release assets after validation; public uploads are a separate release action.

Defaults: English UI, local collection without telemetry upload, mouse/keyboard controls, current-run restoration, and no historical run browser or controller navigation in the first release.

## Slice 2 implementation checkpoint

Implemented proportional player/application-source weights, actual Amount-setter observation, asynchronous Apply/ModifyAmount source scopes, removal cleanup, pre-Doom snapshots and failed-attempt cleanup, event deduplication, source selection in the diagnostic panel, and schema-2 logs. Pure-ledger tests include additions, decay, doubling, partial history, transfer primitives, drift, and fractional reconciliation. Live host/guest tests and native asynchronous/failure-path verification remain pending. Transfer/copy primitives require audited game-operation adapters before live coverage can be claimed. The UI still presents fight-only diagnostics; broader accounting and run persistence remain future slices.


## Slice 2.1: registration repair

Version 0.2.0 failed ownership registration because non-generic Apply returns Task rather than Task<T>. Version 0.2.1 resolves exact targets, validates signatures before patching, supplies both task wrapper shapes, and makes ownership registration atomic under a separate Harmony owner. Failed registration disables ownership credit and reports status damage as Unknown while independent capture remains enabled. Logs identify target signatures and final capability state.

The package gate runs ledger/accounting tests, synthetic Harmony task/context/failure/rollback checks, and real installed-assembly signature validation. These establish readiness for testing only. Actual startup repair requires a fresh 0.2.1 log reporting all four ownership targets installed without error. Host/guest poison and Doom attribution still requires live validation. Transfer/copy limitations remain unchanged; no new meter UI or run persistence is part of this repair.

## Slice 2.2: actual stack mutation capture

0.2.1 startup is verified by four successful ownership registrations with no capture errors. Its live Deathbringer (+21 Doom) and BlightStrike (+9) observations exposed a second issue: real gains bypassed the setter hook and became Unknown. Version 0.2.2 replaces setter attribution with paired before/after power observations keyed by combat-scoped target/effect and operation ID, using explicit hook targets/providers/sources. The tracker accounts for nested committed changes, removes pending operations on completion/failure, and preserves Unknown when correlation is unavailable. Removal captures the key before detaching the effect. Five ownership descriptors are now validated atomically.

Automated checks include an anonymised reproduction, distinct incoming/applied identities, no-op construction, target separation, nested changes, duplicates, partial history, failed scopes, and lifetime reset. Panel and fight summaries expose capture counters; raw damage confidence reflects allocation. Package 0.2.2 remains pending live Deathbringer/BlightStrike and host/guest validation. No final UI, run totals/persistence, support attribution, or transfer adapters were added.

## Slice 3 agreed milestone (implementation in progress)

Target: designed 440px meter with Current Fight/This Run tabs, all base-game status amplification (orange), Support Damage/Contribution, and checkpoint-aware save/resume. Stages: modifier catalogue and pure accounting; checkpoint store; Godot UI; integrated checks. Orange replaces HP/block portions, never adds damage. Self amplification remains with dealer; teammate support transfers conserve totals. Added poison/Doom stacks, extra attacks, energy and draws remain separate.

Run identity includes profile/mode/start time/seed/party. Encounter attempts replace earlier attempts. Store atomic mod-owned snapshots keyed to semantic game checkpoint fingerprints; never edit game saves or restore future/abandoned attempts. Unknown history and unsupported modifiers must remain visible. Scope excludes defence/healing views, historical browser, controller controls and public publishing. Completion requires full catalogue coverage plus persistence/UI checks; native host/guest and reload verification remain separate gates. Do not label partial implementation as 0.3.0 ready.

### Slice 3 source checkpoint: preview.1 (not milestone complete)

Implemented pure effective-amplification calculations and conserving support transfers for validated frozen modifier outputs; structured totals fields; semantic checkpoint hashing and atomic snapshot storage with backup; save/load observer bridge; replaceable encounter snapshots and Fight/Run aggregation; a new tabbed blue/red meter with source details, stable hovered order, viewport-clamped position, width resizing, collapse/F8, and saved layout. Modifier discovery checks 31 declared power overrides against the installed assembly; this is an inventory, not validated native coverage.

Native modifier trace capture/adapters, per-modifier formula/interaction audit, actual orange/support/contribution UI integration, destination-specific structured source totals, and comprehensive save/UI native integration checks remain unfinished. The three affected metrics are explicitly disabled. Unit tests exercise model maths and checkpoint file behaviour, not native game load/save ordering. Source version is 0.3.0-preview.1; the stable 0.3.0 package gate remains closed. Existing 0.2.2 artifacts are not replaced by the preview source until packaging runs. Do not describe this checkpoint as implementation-complete.

### UI patch: 0.3.0-preview.2

Raw block/HP values are centered in their respective blue/red segments; single-metric views label their filled segment. Only team percentage remains in the right column. Values use up to two decimals and outlined text; labels that cannot fit remain in tooltips. No change to accounting or bar scaling.

Collapse now uses one state method for button presses and restored settings, updates the plus/minus icon, hides the body, and defers panel height reset until layout has been queued. Width and position are preserved. Periodic width clamping no longer rewrites stale height, and content changes trigger height recalculation. Build and existing automated checks are required before packaging; in-game visual confirmation remains pending.

Next milestone step remains native modifier capture and per-modifier adapter validation across the discovered catalogue, followed by orange/support/contribution integration. Run save/resume and multiplayer native validation remain required. This UI patch does not complete amplification coverage or the full 0.3.0 milestone.


## Current damage/support implementation: 0.3.0-preview.3

Connected actual CreatureCmd damage scopes to Hook.ModifyDamage and declared AbstractModel additive/multiplicative/cap callbacks. Traces are scoped separately from previews and correlated to their target; replay must equal final HP/block values before amplification receives credit. Read-only IL audit rejects callbacks that read their incoming amount until a dedicated adapter exists. Installed power-method audit passes, but this establishes input independence, not full interaction/HP-loss/redirection coverage. Unsupported or unmatched events retain actual totals and increment unmeasured coverage counts.

Expanded ownership observation to all powers. Added typed source identities, structured source destination totals, provider/recipient transfer records, self-versus-team support accounting, and checkpoint schema 2. Legacy schema 1 restores as partial. Encounter snapshots persist detailed records. Save operations freeze completed encounters before awaiting disk completion, ignore stale run-generation updates, and no longer clear the active encounter when storage first attaches. The UI now exposes amplification, support and contribution from reconciled traces; orange replaces its blue/red portions. Source/recipient details and unmeasured trace counts remain visible.

A local IL audit tool is in tools/GameAudit; it reads game metadata without invoking game effects. 102 automated/installed-assembly checks passed at this checkpoint. Native startup, modifier interaction matrix, HP-loss/redirection handling, host/guest attribution, delayed-save lifecycle, reloads and visual confirmation remain pending. validation/release-evidence.json records pending gates; stable packaging now checks those gates instead of unconditionally refusing version 0.3.0. Do not mark this preview as a fully verified 0.3.0 release. Remaining v1 defence/healing/energy/draw and platform/release work stays in the main design.

## Damage correctness repair: 0.3.0-preview.4

Confirmed from game LoseHpInternal IL and preview.3 logs: DamageResult.UnblockedDamage is already HP-before minus HP-after. The prior implementation subtracted OverkillDamage again and undercounted lethal hits. EngineDamage.Normalize is now the shared native boundary: actual HP = nonnegative UnblockedDamage, actual block = nonnegative BlockedDamage, overkill remains diagnostic-only. The legacy Add entry point delegates to canonical allocation. Direct damage, poison, trace reconciliation and raw logs use the same semantics; Doom pre-execution HP remains unchanged.

Live regression fixture covers native (HP,overkill) pairs (6,3), (5,2), (1,6), plus a 20-damage hit into 5 block/10 HP. Offensive trace coverage uses the actual result receiver and is separated from incoming diagnostics. Incoming zero-loss events remain prevention_or_redirection_unresolved unless receiver identity proves redirection. They do not inflate offensive unmeasured counts. Command invocation, trace and canonical event IDs link diagnostics and allocations; pre-hit snapshots support later analysis. Native callback ordering/redirection still needs live evidence.

Logs use schema 3/accounting version 2. Checkpoints use schema 3 with explicit accounting version, stored in a separate accounting-v2 directory. Pre-fix files are preserved but their aggregates are excluded, with a partial-history warning that survives subsequent saves. There is no automatic reconstruction from old logs. Layout preferences are unchanged. This patch repairs normalization, not the remaining full modifier/native multiplayer/reload validation gates.

## Validation interval: 0.3.0-preview.5

Implemented a read-only JSONL analyser with game/mod/accounting/session/role/fight
groups, canonical event references, normalization/allocation/segment/support
conservation checks, known/unknown ownership counts and incompatible-accounting
exclusion. It reports observed consistency, observed failures or not exercised;
an absence of errors never proves full native coverage. Two anonymised native
lethal trace pairs supplement synthetic corrupt/legacy/duplicate cases.

Expanded the versioned manifest from the declared power inventory to effective
AbstractModel callbacks, including inherited listeners, relics, enchantments,
HP-loss phases and redirection. It records module/method hashes, direct IL
dependencies, adapter classifications and outstanding scenario obligations.
Every entry remains pending native interaction validation.

Added pure ordered HP-loss replay and read-only native state adapters for Buffer,
Hardened Shell, Intangible, Slippery, Beating Remnant, The Boot and Tungsten Rod.
Adapters require the audited module/IL and must reproduce each native callback's
output. Re-evaluate their captured scalar rules for baseline and removal cases;
do not freeze their observed HP output. Preserve native truncation after HP-loss
phases and actual block consumption independently. Block-bypass flags and
enchantment callbacks are captured. Neutral caps no longer bloat traces.

Capture HP-loss phase input/output, redirect receivers and adapter errors.
Redirection involving multiple receivers retains actual totals but has no
amplification allocation adapter yet. Unknown or mismatching adapters suppress
amplification, increment unmeasured coverage and preserve canonical damage.
Exact Hook signatures are checked before patching.

Stable packaging now requires structured, reviewed results for mandatory
scenario sets, matching versions and hashed local evidence references. Bare
passed strings cannot open a release gate. All native gates remain pending.
Detailed procedures are in validation/README.md.

### Remaining milestone work

Complete per-entry active/inactive and interaction evidence, resolve supported
multi-receiver attribution, and validate native host/guest and delayed-save/
reload behaviour. Verify the panel at 1080p/1440p. Automated maths, reflection
and file checks establish preview readiness only. Do not label this interval or
the full 0.3.0 milestone implementation-complete while relevant adapters and
native gates remain unresolved. The later defence/healing/energy/draw metrics
and platform/distribution work remain in the full design above.

## Preview.6 planned slice
Identity-based result correlation, split-result diagnostics, linked schema-4 log checks and automated regression coverage. Existing gameplay tests were modded host with an unmodded guest, at each increment; specific scenario verification and modded-guest testing remain separate. Redirect amplification stays unmeasured. Accounting version 2 and checkpoint schema 3 remain unchanged.

### Preview.6 implementation checkpoint
Implemented command-scoped result identity bindings at construction/HP-return
boundaries, retained split-result context, incoming/outgoing duplicate suppression,
block-owner snapshots, async completion membership checks and cleanup. Native
construction capture requires the audited installed module and exact boundary
signatures. Capture uncertainty preserves actual totals and leaves amplification
unmeasured. Schema-4 records include binding and canonical identity metadata;
Doom gains an event identity and enemy classification. The analyser joins records
and reconciles completed-fight HP/block summaries; incomplete fights and legacy
correlation limits remain explicit. Automated preview readiness does not complete
native eligibility/order, full modifier, guest or reload validation.

## Preview.7 implementation plan
Audit installed modifier coverage, repair capped proportional provider weights,
version amplification separately from actual HP, and preserve actual checkpoint
history while excluding legacy derived support/amplification. Native checks remain
separate. Save lifecycle repairs and new metrics are excluded.

### Preview.7 checkpoint
Implemented capped-overlap potential weights, allocation-basis diagnostics,
63 pinned installed method contracts, HP-loss accessor preflight and provider
reconciliation in the analyser. Counterfactuals remove positive numeric outputs
at captured state; they do not remove power presence or replay gameplay.
Amplification version 2 uses log schema 5 and checkpoint schema 4; compatible
legacy actual damage survives while derived amplification/support is excluded
with a persistent partial flag. Full per-entry eligibility/interaction cases
and native snapshot verification remain unfinished; the contract inventory must
not be presented as a completed coverage audit. Save lifecycle and new metrics
remain outside this slice.

## Preview.8 agreed slice
Complete Weak/Vulnerable chains and the five nested Cruelty/Debilitate/Paper
callbacks with typed snapshots, independent eligibility tests and per-provider
proportional credit. Keep other catalogue work and native verification separate.

### Preview.8 implementation checkpoint
Implemented typed root/child multiplier snapshots and pure branch rules for Weak,
Vulnerable, Cruelty, both Debilitate callbacks, Paper Phrog and Paper Krane. Native
read-only adapters record eligibility guards, seed, ordered callbacks and provider
shares. Child expectations are captured before native callbacks; unknown hashes,
missing state, order/output mismatches leave actual damage intact and amplification
unmeasured. Pure replay removes positive numeric contributions at captured
eligibility, keeps relic adjustments, and reevaluates dependent operations.
Amplification version 3/log schema 6 reuse checkpoint schema 4 compatibility.
Branch tables, replay, support/source conservation and analyser corruption cases
are automated; native snapshot execution and live role/scenario gates remain
pending. The broader catalogue audit is not completed by this focused group.

## Preview.9 agreed slice
Validate all seven additive powers with read-only typed snapshots, signed-amount
rules, command/history/summon eligibility, provider provenance and Harmony state
tests. Accounting/log/checkpoint versions remain unchanged; native verification
and other modifier families remain separate.

### Preview.9 implementation checkpoint
Added pure signed-amount/eligibility rules and typed read-only native snapshots
for all seven additive powers. Command identities, Shiv tag, Phantom history
predicate/accessors and summon ownership are explicit. A shared Harmony state
coordinator preserves nested multiplier capture and exception cleanup; preview
callbacks are excluded. Native output mismatches mark amplification unmeasured
while canonical actual damage remains independent. Zero outputs retain audit
observations; the analyser compares expected/native results. Branch/history,
provenance, cap/Contribution and patched synthetic context tests supplement
installed contract checks. Native adapter execution/role verification and the
broader catalogue remain pending. Accounting/log/checkpoint versions are unchanged.

## Multiplayer divergence repair: preview.10
The 2026-11-03 host log records an ArgumentNullException in
AfterModifyingHpLostBeforeOsty during the unmodded guest's first Strike. Host
enemy HP remained 88 versus guest 82. Installed Harmony reproduction established
that HpPhasePostfix(object[] __args, ...) wrote the stale null out argument back
over the game's modifier list. Typed input injection fixes output preservation;
the prefix was also converted to typed input. Existing postfixes were reviewed
for the same out-parameter shape. No damage-accounting definitions change.
395 checks include a negative control and actual production observer patches on
a native-signature synthetic method. Native multiplayer retest remains pending.

## Preview.11 agreed increment
Validate Double Damage, Lethality, Gigantification, Shrink, Slow, Tracking and
Knockdown through typed pre-callback snapshots and expected/native comparisons.
Preserve preview.10's out-list fix/regression. Amplification 3/log 6/checkpoint 4
remain unchanged. Native gameplay gates remain separate.

### Preview.11 implementation checkpoint
Implemented typed expected/native validation for seven attack multipliers using
the existing shared Harmony coordinator. Pinned Lethality's started-attack
history predicate and validated enum, command and dynamic accessors. Provider
shares are frozen before callbacks; eligibility facts include real pet collection
membership and command identity. Added optional multiplicative_snapshot analysis
and independent branch/history, dynamic-vs-Amount, cap/Contribution tests. Full
package checks retain preview.10's original out-list corruption negative control
and production typed-observer regression. No accounting/version migration, UI or
save lifecycle changes. Pure/metadata coverage does not establish native bridge
execution or multiplayer correctness; those gates remain pending.

## Preview.12 dispatch repair plan
The preview.10 session 16388aa32fb54a59b7545b651350ce1d records 35 offensive
unmeasured events with a Creature-to-ValueProp cast failure and chain mismatch.
Weak/Vulnerable root capture selected by instance alone also handled inherited
cap callbacks with a different layout. Require exact callback/contract dispatch
before state creation or positional access; exercise production hooks with native
metadata and preserve preview.10's output-list regression.

### Preview.12 implementation checkpoint
Added exact method/model/signature/hash dispatch for chain, additive and attack
multiplier handlers. Native chain entry independently rejects unrelated callbacks
before frame allocation or positional reads. Child family/signature validation
precedes child argument access; failed snapshots invalidate the frame and suppress
secondary reconciliation errors. Production observer/coordinator regressions use
installed native metadata and isolated injected capture context with finally
restoration. They cover inherited additive/cap callbacks on both powers, malformed
roots, failed snapshots, wrong child family, preview exclusion and parent cleanup.
An anonymised observed-failure fixture retains the 35-event evidence; no old
amplification is reconstructed. Accounting versions and native gates are unchanged.

## Preview.13 agreed change
Provider-owned amplification applies to all numeric modifiers, HP and block, including poison views. Active stacks split shared provider credit proportionally. Only effective capped bonus is credited; old actual history is preserved and older credited history remains explicitly partial.

### Preview.13 implementation checkpoint
Added a distinct canonical-event credited ledger with HP/block, effect kind,
provider source and triggering source records. Base credit remains with raw
owners; effective amplification goes to all providers including self. Raw
accounting remains available as Actual Damage. Shared poison uses its original
owner weights for base and amplifier weights for bonus. Doom stays execution-only.
Main/effect/Amplification views, orange segments, sorting, percentages and source
details use credited records. Support is a covered-history subset, never added
again. Legacy Contribution selection migrates to Damage. Attribution snapshot
version 1 is checkpoint schema 5/log schema 7; legacy actuals remain accessible
and credited history is visibly partial. Analyser checks event and per-player
summary conservation, including missing credit records and Doom. Existing math,
dispatch and out-parameter regressions remain in the package gate. Native UI and
multiplayer scenario verification remains pending.

## Preview.14 agreed UI increment
Move player source/recipient details and global diagnostics from the footer into
row hover tooltips. Retain a compact Partial/Incomplete badge beside Fight/Run
with full explanations on hover; header also exposes diagnostics before rows.
Preserve accounting versions, layout, row stability and centered bar values.

### Preview.14 implementation checkpoint
Implemented native custom row tooltips with live values and metric-specific
source/recipient lists. Removed click selection and footer details; row list
height follows player count with scrolling for excess rows. Warning badges use
Incomplete precedence over Partial; header/badge hover retains full diagnostics.
Tooltips wrap within the game viewport and trim source entries to twelve or fewer
with an explicit remainder count. They close on collapse, F8 hide and row removal.
509 checks pass, including formatting/filtering/status checks and existing
accounting, dispatch, out-parameter and installed contract regressions. Native
visual verification remains pending; see README preview.14 checklist.

## Preview.15 agreed UX
Keep all eight metrics grouped as Damage and Attribution with stable saved IDs.
Shorten title, add always-available Info and header data-quality badge, including
while collapsed. Show relevant labelled colour swatches and Party %. Centre one
selected-metric total over each full track; show zero and retain full names on
hover. Focus player tooltips on selected metric, relevant bonus destinations and
sources; technical diagnostics live in Info/status. Preserve accounting, schemas,
layout preferences, hover stability and input behaviour. Validate native layouts
at 1280x720/1920x1080 with two/four players separately from automated checks.

### Preview.15 implementation checkpoint
Implemented grouped selection with unchanged saved metric IDs, focused player
tooltips, centred full-track totals including zero, contextual labelled swatches,
Party % heading and empty state. Header Info contains help/version/diagnostics;
data-quality status remains accessible while collapsed. Existing capture and
storage schemas are unchanged. Package build and 515 checks pass, including
selected-metric detail/subset checks and prior installed-contract and multiplayer
observer regressions. Native visual checks and multiplayer release gates remain
pending; README contains the preview.15 acceptance checklist.

## Effective Damage agreed increment (preview.16)
Default new installations to Effective Damage, retaining all saved selections.
Count all HP and only block later breached by a block-respecting HP hit against
the same enemy. Retained shields/top-ups preserve pending ownership across turns.
Exact breaks wait for any later qualifying hit; a new shield at zero discards old
pending credit. Natural expiration, death and combat end discard pending credit.
Bypass damage never unlocks block. Preserve provider splits and existing views.
Add effective version 1/checkpoint 6/log 8, with partial legacy effective history.

### Preview.16 implementation checkpoint
Independent effective ledger, target block gain/awaited clear observers and
trace-based bypass qualification are connected to HP/death/Doom/combat cleanup,
Fight/Run UI and persistence. Saved selections and other views remain unchanged.
New installations default to Effective Damage. Missing observations retain HP
and flag incomplete coverage. Build/package and 530 checks pass, including prior
multiplayer regressions. Native UI/gameplay verification remains pending.

## Preview.17 agreed persistence repair
Bind statistics to actual written/read run-save payloads using fingerprint v2,
checkpoint 7 and isolated storage. Freeze statistics at save entry, publish only
after success, reject stale completions, restore once after outer successful load.
Preserve native results/exceptions and existing archives. Fix future restarts only;
no retrospective recovery. Native reload tests remain separate release gates.

### Preview.17 implementation checkpoint
Implemented scoped native save-store payload observation, frozen save snapshots,
accepted primary/backup reads, outer-load restoration, generation/order guards
and semantic run-state matching. Fingerprint v2/checkpoint 7 are isolated from
legacy archives. No retrospective recovery or relaxed latest-checkpoint matching.
Production observer registration, mutation capture, exception preservation, stale
completion checks and credited/effective checkpoint roundtrip pass in the 544-check
package gate. Real host/guest restart acceptance remains pending; see README.

## Preview.18 native startup repair
Native logs confirm preview.17's generic LoadSaveFromPath run specialization
changed profile/progress schema interpretation and triggered false corruption
handling. Remove that patch entirely; never patch shared generic save loaders.
Observe non-generic I/O only within outer run-load scopes and restrict filenames
to current_run/current_run_mp primary/backup. Production regression registration
asserts generic run/profile/progress targets remain unpatched. 549 checks pass;
native startup and restart verification remain pending. The attempted recovery failed to preserve visible progress; its script is retired.

## Incident cleanup checkpoint
Retire the recovery command without save access, block preview.17 packaging and
deployment, quarantine its distribution and remove only disposable investigation
tools/fixtures. Preserve logs, real-save copies, .corrupt originals and recovery
backups. User requested cleanup only: no further restoration or installation
changes. Automated checks are not native startup/persistence verification.

Cleanup verification: preview.17 quarantined, recovery stub and package/deploy
guards checked, disposable audit/fixtures removed with real-save sample preserved.
Normal preview.18 package passes 550 checks. Hash comparison: 246 external files,
zero changes. No deployment, launch or recovery performed.

## Preview.19 compact top row
Remove Damage Meter title and Partial/Info labels. Metric dropdown replaces the
title; resize arrow and collapse share that row. Remove the bottom resize row.
Left-drag arrow resizes, right-drag moves; arrow hover retains version, help and
diagnostics while collapsed. Dropdown/player hover retains relevant data-quality
notices. Keep saved layout/metric IDs, F8 and accounting unchanged. Native visual
verification remains pending.

## Preview.20 dedicated grab handle
Add a left-drag grab handle next to the top-row resize arrow. The arrow resizes
only; the grab handle moves only and shows a drag cursor/tooltip. Persist position
on release, retain viewport clamping and collapsed access, cancel dragging on F8.
No changes to saves, capture or accounting. Native visual checks remain pending.

## Preview.21 compact collapsed controls
Move the collapse/expand toggle to the left of the top row. Collapsing hides the
metric dropdown together with the body; expanding restores the selected metric.
Grab and resize handles remain available. Retain saved collapse/layout state and
panel width. Native interaction verification remains pending.

## Preview.22 button-only collapse
Collapsed mode hides dropdown, body, resize and grab controls and removes the
320px minimum width. Deferred layout shrinks both dimensions to the expand button.
Retain expanded width separately; saving/restarting while collapsed must not
replace that width with button width. Expansion restores controls and prior width,
subject to viewport clamping. Native visual checks remain pending.

## Preview.23 solo presentation
Use native session type, never row/player count, to select presentation. In solo
hide names (except Unattributed), Party % heading, row percentages and tooltip
party-share text. Bars fill freed columns with centred total intact. Unknown
session types retain the last known mode, initially multiplayer presentation.
Preserve multiplayer, dimensions, collapse, drag/resize, F8 and all accounting.
No save or capture-hook changes; native visual verification remains pending.

## Preview.24 structured tooltips
Replace flat player text with title/scope, prominent total, aligned breakdown,
Already included bonus rows, source table and separate notes/warnings. Use shared
legend text colours: HP red, block blue, bonus orange; combined totals neutral.
Combined views use Source/HP/Block/Total; single metrics Source/Value; support
Recipient-source/Bonus HP/Bonus Block/Total. Pending block is muted and excluded.
Keep solo suppression, live native hover, viewport bounds and twelve-source cap.
No capture, accounting or persistence changes. Native visual checks remain pending.

### Preview.24 implementation checkpoint
Typed semantic numeric cells and source rows now drive native container cards.
Palette is shared with the legend; summary subsets, support destinations and
pending block are separated. Existing card instances update values without
resetting hover; structural changes rebuild only when the table/section shape
changes. Wrapped source rows are height-budgeted within the game viewport with
explicit remainder count. Package gate passes 571 checks, including semantic
colour/aggregation, solo, zero and warning presentation checks. Native visual
acceptance remains pending; no save/capture/accounting changes were made.

## Project documentation and logo
README is the current project overview with deployment, controls, limitations and
Kaztelle's concept/design credit. Historical preview notes move to root
CHANGELOG.md with reciprocal links and explicit supersession/withdrawal context.
Built-in imagegen generated the minimal meter wordmark; PNG and prompt provenance
live in assets. Package includes linked documentation and logo. No mod-version,
capture/accounting changes, installation or save mutations are part of this work.

## Save-safety certification work
Certify the final hot-reload candidate, including host callbacks, operation state,
runtime replacement and native managers. Keep the README incident warning until
isolated native tests and solo/host/guest gameplay restart evidence all pass for
exact build hashes. No old-progress recovery, existing-save mutation or Steam
Cloud access. Existing reload-only gate is not persistence certification.

### Save-safety work checkpoint (incomplete)
Added shared run-save classification to host and runtime read/write observation.
Host callbacks now contain missing/throwing runtime failures; finalizers restore
scope directly and preserve native exceptions. 15 path-policy checks and 12
production callback-source isolation checks pass. Main project builds. Native
fixture runner exports under the matching game runtime but crashes before scene
entry (0xC0000005), so native schema, full candidate persistence and gameplay
certification remain pending. Structured evidence gate is fail-closed; README
incident warning retained. No live-save restoration/deployment/cloud access.

## Preview.25 development hot reload

The ordinary build and development build share permanent host/contract assemblies
and a separate runtime. `-Development` maps to `DevelopmentMode=true`; only that
host watches atomic runtime publications. Runtime-only changes activate between
callbacks on the next available frame, including during combat. No combat/menu
boundary is required. Host/contract changes require a restart.

Shared data objects retain accounting, ownership, pending effective block,
deduplication, coverage, run history and layout. Methods forward synchronously to
the active runtime; pending task wrappers and typed Harmony entry points remain
in the host. Custom Godot classes also remain permanent. UI roots and deferred
callbacks belong to a generation; failed candidates release their resources and
leave the existing runtime/UI active.

Content-addressed build directories and an atomic `runtime/current.json` pointer
prevent partial DLL loads. Host/contract hashes, runtime hashes and API/state
versions are checked. Git metadata and checkout paths are excluded from permanent
binary identity. More than two retained generations stops further reloads.

Private runtime log/phase values are snapshotted into JSON without framework
reflection serialization: .NET's global accessor cache otherwise retains their
assemblies even with distinct serializer options. Native tests explicitly emit
private anonymous log values through repeated reloads and check collection.
The developer workflow and implementation boundaries are in `docs/hot-reload.md`.
Isolated engine tests do not certify live multiplayer or save/progress safety.
