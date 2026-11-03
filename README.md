# Based Damage Meter

Diagnostic build for Slay the Spire 2 v0.107.1. Live gameplay and multiplayer verification are **pending**. This is not the final meter.

## Build and checks

Requires .NET 9+ SDK, Godot.NET.Sdk 4.5.1 (restored through NuGet), and an installed game.

```powershell
dotnet build -p:Sts2DataDir="C:/path/to/game/data_sts2_windows_x86_64"
dotnet run --project tests/ProbeChecks.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
```

On this machine, the pre-existing package cache is `C:/Users/User/.nuget/packages`. If running as another account offline, set `NUGET_PACKAGES` to that directory before restoring. Checks need no third-party test framework. An optional argument to the checks executable is the game assembly directory, to print installed hook signatures.

The ZIP is `artifacts/Based.Spires2DamageMeter-0.2.2.zip`. Close the game, then extract its `mods` directory into the game directory. Enable mods through the game's mod controls. Only the observer installs this mod. Do not install the original DamageTracker alongside this diagnostic build during baseline testing. Remove this mod's directory while the game is closed to uninstall. Installation does not edit saves; the game controls modded save handling.

Build, check, package, deploy and launch the current source in the default Steam installation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy.ps1
```

The script refuses to deploy while the game is running. It rebuilds and tests the current source, validates package/DLL versions, then copies only this mod's packaged files into `mods/Based.Spires2DamageMeter`, then launches the game after a successful deployment. Use `-WhatIf` to preview without building, copying files or launching the game, or `-GameDir 'D:/path/to/Slay the Spire 2'` to override the installation directory. If Windows denies access to the game directory, run the command in an elevated PowerShell window.

## Diagnostic panel

Appears on entering combat. Drag with the left mouse button; F8 shows/hides. Columns are HP damage, block damage, poison HP, and Doom HP. Poison and Doom are **subsets** of HP, not additional damage. Totals cover the current fight only and reset next fight. No run restoration or layout persistence yet.

Shared poison/Doom damage is split proportionally by active ownership weights. Automatic reductions preserve proportions; added stacks belong to the player causing the addition. Missing history occupies only its own `Unattributed` share. Select a player in the dropdown for application-source HP totals (display rounded to two decimals). Vulnerable applications are logged without support-damage calculations. Other indirect damage without reliable provenance may also be unattributed; explicit poison-trigger coverage must be checked in-game.

Logs are buffered JSONL files under `<Godot user data>/Based.Spires2DamageMeter/logs/`. The startup record and game log identify the path; each process gets a new file. Fight-end summaries contain effective totals. Log files include player identifiers and the run seed; share them intentionally. A crash can lose the most recent buffered second. An incomplete warning indicates a hook or logging error; retain the game log too.

## Two-player verification checklist

Run both passes on the same game version, with a friend who has **no mods installed**:

1. Host a multiplayer run. Confirm both players appear, role is Host, and no incomplete warning appears.
2. Have each player attack separately. Compare actual enemy HP/block changes against panel and log. Include a blocked attack, lethal overkill, multiple hits, and multiple targets.
3. Apply poison and watch ticks. Include a repeated/explicit trigger where available. Confirm ticks count once, cap at remaining HP, and appear within HP totals.
4. Apply Doom without killing: expect zero immediate Doom HP. Damage the enemy by other means, then let Doom execute: expect only its remaining HP. Exercise multiple Doom deaths when available.
5. Apply the same status from both players and increase/double stacks if available. Confirm applications identify the right player and shared damage is divided proportionally, not credited to the last applier. Six stacks from A and four from B should split ten effective damage 6/4. If B doubles five stacks from A, subsequent damage splits equally. Doom executing eight remaining HP credits only eight total.
6. Apply Vulnerable and perform an action requiring a player choice. Check effect source/recipient logs and that normal combat continues.
7. Finish the fight. Confirm one final summary, retained panel totals, and a fresh reset next fight.
8. Repeat with your friend hosting and you joining. Role should identify Client; repeat direct damage and poison/Doom coverage.

Record game version, role, characters, scenario, expected/observed result, and JSONL filename. Mark unavailable scenarios pending. No host/guest compatibility claims are verified until both passes provide evidence. Three/four-player and macOS/Linux testing belong to later validation.

## Slice 2 verification status

Version 0.2.1 logs use schema 2. Ownership changes and linked damage allocations are recorded separately from raw events. Existing logs are not migrated. Host direct-damage observation was seen in 0.1.0 logs; version 0.2.1 gameplay, asynchronous source capture, and guest operation remain unverified. Standalone tests do not invoke the native game.

The ledger supports explicit source-preserving transfers, but the game adapter currently has no audited card-specific transfer/copy adapters. Unknown automatic additions remain Unattributed. Do not assume transfer coverage from unit tests; supply a concrete transfer/copy scenario during live testing. No run persistence, support-damage calculations, or final meter UI is included.


## Startup repair (0.2.1)

The 0.2.0 ownership hook group stopped registering because Apply returns Task, while ModifyAmount returns Task<int>. Version 0.2.1 handles both shapes, preflights four exact targets, and installs them under a separate Harmony owner. Any installation failure rolls back only that group and disables status ownership credit; resolved HP totals remain available as Unattributed.

Packaging now requires the accounting/context/Harmony checks and compatibility validation against the installed game assemblies. These checks do not establish in-game success. After deploying, inspect the new log for four ownership_target_validated events, four ownership_target_installed events, and ownership_registration with enabled=true, expected=4, installed=4. No ownership registration errors or incomplete warning should appear. The startup record or mod-menu entry alone is insufficient.

Then repeat the host/guest checklist above. Confirm application sources from both players, a 6/4 shared split, stack doubling, proportional decay, lethal poison capped by HP, Doom execution after prior damage, and source isolation after player choices. Keep JSONL event IDs and expected/observed results. Unavailable scenarios remain pending. Build readiness, startup repaired, and multiplayer attribution verified are separate milestones.

## Stack capture repair (0.2.2)

Live 0.2.1 logs verified successful startup but showed a capture defect: Deathbringer added 21 Doom and BlightStrike added 9, while the Amount setter hook observed only unattached 0-to-0 construction writes. Version 0.2.2 pairs BeforePowerAmountChanged and AfterPowerAmountChanged using the explicit target, effect type, and command operation ID. It reads the active target's amount before the change, then the applied model's actual amount afterward. Incoming and applied models need not be identical. Explicit application-hook provider/source data takes priority.

There are now five required ownership targets; use the registration summary's expected/installed values rather than the older four-target checklist. Schema 2 ownership_change records include OperationId, Key, Before, After, Owner, Source, Quality, and Shares. ownership_observation records identify duplicate/unmatched callbacks. Fight summaries and the panel show confirmed, unknown, and unresolved counts.

Re-test Deathbringer then BlightStrike on the same enemy: expect 21 stacks under Deathbringer, another 9 under BlightStrike, both owned by the observed player, with 30 total. Repeat for multiple enemies, two providers, decay, and doubling where available. Poison ticks and Doom executions must still credit only actual HP loss. Repeat representative cases as guest. Preserve operation/event IDs with expected/observed results; unavailable cases stay pending.

The regression fixture tests/fixtures/doom-applications.json uses anonymised providers and distinct incoming/applied model labels. Automated tests cover the observed sequence, isolation, shared attribution, nesting, duplicates, missing history, removal/reapplication, and scope cleanup. They do not prove native callback ordering. Version 0.2.2 is ready for live testing after package checks; native startup and host/guest attribution are pending for this version.


## 0.3.0-preview.1 source checkpoint

This is an incomplete milestone preview. The source builds a tabbed blue/red damage meter with player-source details and persisted layout. Save/load observers attach mod-owned snapshots to semantic checkpoints; reload behaviour remains unverified in-game. Pure amplification and support-transfer maths are tested, but native modifier capture/adapters are not connected. Amplification, Support Damage and Contribution are disabled in the selector. No complete orange-coverage claim is made. The modifier inventory contains 31 power overrides, including reductions/caps and a mock power requiring classification.

The stable 0.3.0 package gate remains closed until native amplification coverage and integration are complete. Do not use preview run totals as verified save/resume evidence. Statistics live under the game's user-data Based.Spires2DamageMeter/checkpoints directory; game saves are never edited.

## Deployment freshness fix

The deployment script now builds/tests/packages every time before copying, checks manifest and DLL versions, verifies installed hashes, and prints the installed version before launch. This fixes the stale 0.2.2 artifact shown after the preview UI source was added. The preview title is Based Damage Meter 0.3.0-preview.1, with Fight/Run tabs and blue/red bars. Amplification/support metrics remain disabled pending native integration.


## UI fixes: 0.3.0-preview.2

Block and HP values now appear centered in the blue/red segments; the right edge shows only team percentage. Narrow segment labels are omitted rather than overlapping, with exact values available in the tooltip. The collapse control now shrinks the background and input area to the header while preserving width/position, and restores the correct plus/minus icon from saved settings. Check mixed and single-metric views, narrow/fractional values, repeat collapse/expand, and restart with a saved collapsed state. These UI behaviours need final visual confirmation in-game.

Deploy with the usual script to rebuild/check/package/install this version and launch through Steam. The header should read Based Damage Meter 0.3.0-preview.2. Orange amplification and support/contribution selectors remain disabled pending native adapter implementation.

## 0.3.0-preview.3 â€” native amplification preview

The current package connects damage scopes, modifier callbacks, target-specific reconciliation, orange bars, Amplification, Support Damage and Contribution. Only traces whose replay matches actual HP/block values are credited; input-dependent callbacks without adapters and unmatched/redirection/HP-loss cases remain unmeasured. The coverage line reports matched/unmeasured counts. A zero metric with unmeasured events is not proof that no amplification occurred. Earlier sections describing disabled selectors apply to earlier previews.

Self amplification remains with its dealer. Teammate amplification transfers effective HP/block credit to its provider only in Contribution; actual Damage ownership is unchanged. Shared or unknown providers remain explicit. Details expose sources and support recipients. Schema-2 checkpoint files preserve transfers and typed source totals; schema-1 history is marked partial. Game saves are not edited.

The preview is not stable-release verified. Test simple Strength/Vulnerable hits and then lethal/block/cap cases, poison/Doom, shared modifiers, host/guest operation, and save/reload replacement. Keep damage_trace, damage_trace_mismatch, damage_trace_unsupported and statistics_checkpoint events. Full interaction coverage, input-dependent adapters, extra HP-loss/redirection paths, and native lifecycle validation remain unfinished. Stable release gates are in validation/release-evidence.json and must contain real verification evidence before being marked passed.

## Corrected HP accounting: 0.3.0-preview.4

The game already caps UnblockedDamage to actual HP removed. Older builds subtracted overkill a second time. Preview.4 credits the engine's HP loss directly, keeps enemy block separate, and never adds or subtracts diagnostic overkill. Thus an engine result of 6 HP with 3 overkill credits 6 HP, not 3. Doom still credits only confirmed remaining HP at execution.

New logs have schemaVersion=3 and accountingVersion=2. Enemy amplification coverage excludes incoming player/summon damage. Incoming zero-loss events are labelled unresolved prevention/redirection unless the actual receiver proves a redirect. eventId, invocationId, traceId, originalTarget and receiver allow correlation with canonical damage events.

Checkpoint files from earlier accounting are preserved but excluded from current totals. The run is labelled partial and corrected collection starts at the current tracking boundary. New files live under checkpoints/accounting-v2; layouts are preserved. Do not expect old undercounted aggregate history to be retroactively repaired.

After deploying, verify the preview.4 header; compare a lethal hit's visible HP loss, raw engine fields, allocation and bar total. Then test a lethal amplified hit, poison and Doom, incoming prevention/redirection, host/guest operation, and reload replacement. Save event IDs with expected/observed values. Stable 0.3.0 remains unverified until its existing evidence gates pass.

## 0.3.0-preview.5 â€” phase capture and validation tooling

Added state snapshots for the seven base-game HP-loss callbacks, block-bypass
replay, enchantment capture, explicit redirection diagnostics and quieter traces
that omit neutral caps. Amplification is credited only when supported replay
matches actual damage; unknown adapters and multi-receiver redirection remain
unmeasured. Actual HP/block accounting continues independently.

The read-only TraceReport tool checks numerical consistency and gives event
references. The expanded modifier manifest includes inherited and non-power
callbacks. Stable release gates now require reviewed, version-matched scenario
results and existing hashed evidence files. See [validation workflow](validation/README.md).

Deploy using the existing script after closing the game:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy.ps1
```

Confirm the preview.5 title. Native adapter eligibility/order, modifier interaction
coverage, host/guest attribution, reload lifecycle and resolution-specific UI
checks are still pending. This build does not complete the 0.3.0 milestone.

## 0.3.0-preview.6 ? result identity correlation

Damage results are bound by object identity to command/resolution context, including
redirected receiver and original-target overflow results. Duplicate callbacks are
suppressed for incoming and outgoing observations. Block ownership and phase
snapshots remain attached; multi-receiver amplification remains unmeasured.

Logs now use schema 4; accounting version 2 and checkpoint schema 3 are unchanged.
TraceReport checks linked canonical records and completed-fight HP/block totals
(including Doom). Incomplete fights defer missing-link conclusions. Schema 3
remains numerically readable, without the new correlation checks.

Host checklist after deploying with the existing script:
1. Confirm preview.6 and successful damage-trace registration.
2. Check ordinary, fully blocked and lethal damage against visible HP/block loss.
3. Exercise poison and Doom, checking actual HP and shared ownership.
4. Where available, exercise Osty redirection with zero and lethal overflow.
   Both results should retain one resolution ID and distinct canonical event IDs.
5. Finish combat and run TraceReport on the log. Preserve event IDs for mismatches.

Previous increments were gameplay-tested as modded host with an unmodded guest.
Specific native scenario verification and modded-guest testing remain separate;
this package does not automatically pass those release gates.

## 0.3.0-preview.7 ? proportional capped amplification

Known buff providers retain proportional credit when overlapping caps hide their
individual effects. Fallback weights use uncapped/unrounded numeric potential;
allocated orange HP/block never exceeds the effective destination budget. Trace
diagnostics expose each weight and its basis. Unknown stack history stays Unknown.

Logs use schema 5/amplification version 2; actual HP accounting remains version 2.
Checkpoint schema 4 restores older compatible actual HP/block/poison/Doom/source
totals but excludes old amplification/support/transfer history, with a persistent
partial-history notice. Loading never rewrites originals; saving retains backups.

All 63 installed method contracts are pinned; changed methods fail closed. This
is structural coverage, not completed native eligibility/interaction validation.
The remaining review is explicit in validation/modifier-coverage-v0.107.1.json.

After deployment, confirm preview.7. As host with an unmodded guest, compare
shared Strength/Vulnerable hits below and above block/lethal/cap boundaries.
Check orange HP/block totals, provider/source splits, Support and Contribution.
Exercise poison ownership separately and ensure Doom gains no inferred bonus.
Retain event IDs and weight diagnostics. Repeat representative cases as modded
guest when available. Stable release gates remain pending.

## 0.3.0-preview.8 ? Weak/Vulnerable chains

Vulnerable and Weak retain ordered nested effects from Paper Phrog, Paper Krane,
Cruelty and Debilitate. Positive child status effects receive their own provider
shares; relic adjustments remain baseline and reductions earn no positive support.
The replay reevaluates Debilitate for each counterfactual rather than freezing its
observed output. Child order, input/output continuity, final multiplier and
installed contracts must match before amplification receives credit.

Logs use schema 6/amplification version 3. Checkpoint schema 4 retains compatible
actual damage while excluding older derived amplification/support history with a
partial-history notice. Other modifier/native validation remains separate.

Host checklist with an unmodded guest: compare ordinary Weak/Vulnerable attacks,
then available Paper relic, Cruelty and Debilitate combinations. Include a different
provider, mixed block/HP and lethal caps; inspect child/source/recipient credit.
Check non-powered damage does not receive these attack-only effects; keep poison
and Doom ownership checks separate. Preserve event IDs and mark unavailable cases
unexercised. Confirm the header reads preview.8 after normal deployment.

## 0.3.0-preview.9 ? additive power validation

Strength, Vigor, Accuracy, Calcify, Leadership, Phantom Blades and Tainted now
use typed pre-callback eligibility/amount/provider snapshots. Native output must
match the pure rule before amplification receives credit. Vigor checks command
object identity; Phantom Blades counts matching finished Shiv plays by its owner
this turn; Calcify checks Osty and its pet owner. Zero observations are logged.

The existing multiplier chains share one Harmony state coordinator with the new
adapter. Preview calls are excluded. Actual damage survives unsupported capture.
Amplification 3, log schema 6 and checkpoint schema 4 remain unchanged.

After normal deployment, confirm preview.9. With an unmodded guest, compare
Strength/Accuracy attacks and ineligible non-attacks; exercise Vigor's selected
command, first/subsequent Shiv plays and turn reset, and correct/other-owner Osty
where available. Check Leadership excludes its owner and Tainted applies on the
correct target. Inspect mixed block/HP, lethal and shared-provider cases. Preserve
additive_snapshot resolution IDs; unavailable native cases remain unexercised.

## 0.3.0-preview.10 ? multiplayer divergence fix

Fixed the HP-loss observer corrupting the game's out-modifiers list through
Harmony argument-array writeback. The postfix now reads typed inputs only.
The host previously aborted the first Strike while an unmodded guest applied
damage, causing a checksum mismatch and disconnect.

Regression tests reproduce the old null-list failure and verify exact list
identity, empty/nonempty outputs, downstream Contains, and the production
observer against a native-shaped emitted method. 395 checks pass. This is a
code/package fix; repeat the multiplayer first-attack test after deployment.

Close the running game, run scripts/deploy.ps1 and confirm preview.10 before
resuming multiplayer. Restarting a run without deploying still uses the old DLL.

## 0.3.0-preview.11 ? attack multiplier validation

Typed pre-callback snapshots validate Double Damage, Lethality, Gigantification,
Shrink, Slow, Tracking and Knockdown. Expected/native disagreements suppress
amplification while actual damage remains independent. Command source identities,
owner/pet membership, started-play history and dynamic values are captured with
native short-circuit guards. Zero/negative multipliers retain native semantics;
reductions earn no positive support and missing provenance stays Unknown.

Optional multiplicative_snapshot records expose facts, eligibility/output and
provider shares; the analyser flags disagreements. Amplification 3, log schema 6
and checkpoint schema 4 are unchanged. Preview.10's HP observer fix is retained
and its production out-list regression passes.

Host checklist with an unmodded guest: first confirm preview.11 and verify a
first attack completes on both peers without divergence. Where available test
owner/owned-pet Double Damage, selected-command Gigantification, first/subsequent
attack Lethality, Shrink/Slow values, Tracking with/without Weak, and Knockdown
with its applier versus another dealer. Include mixed block/HP, lethal caps and
shared providers. Preserve snapshot resolution IDs. Native cases and roles not
actually exercised remain pending. Close the game before normal deployment.

## 0.3.0-preview.12 ? callback dispatch repair

Fixed Weak/Vulnerable chain capture starting for inherited additive/cap callbacks
with incompatible parameter layouts. Specialized handlers now require the exact
model, method, signature and pinned contract before reading inputs. Generic
callbacks remain generic. Failed snapshots cannot produce a partial chain plus
a second misleading reconciliation error. Child family/signature is also checked.

The preview.10 log contained 35 affected unmeasured offensive events. This patch
does not reconstruct historical amplification. Amplification 3, log schema 6 and
checkpoint schema 4 are unchanged. The preview.10 out-list fix is retained.

After normal deployment, confirm preview.12 with an unmodded guest. Verify a first
attack completes, then apply Weak/Vulnerable and compare ordinary, blocked and
lethal attacks. Check matched traces, orange/provider credit, and absence of the
old Creature-to-ValueProp cast and false chain-mismatch reasons. Genuine mismatches
must still remain unmeasured. Native scenarios not exercised stay pending.

## 0.3.0-preview.13 ? provider-owned amplification

Main Damage, HP Only, Block Only and effect views now credit effective numeric
amplification to its providers. A 6-damage attack increased to 9 credits 6 to the
attacker and 3 to the Vulnerable provider. Active owned stacks divide shared
bonuses proportionally; self-provided bonuses remain with that player. Caps and
remaining HP limit actual extra credit. An attack already lethal at 4 HP credits
4 to the attacker and zero bonus to the provider. Enemy block uses the same rule.

This applies to all numeric amplification, including supported poison increases.
Poison base damage stays with stack owners; Doom execution ownership is unchanged.
Orange follows credited amplification. Support Damage is a cross-player subset
already included in Damage. Contribution is replaced by Actual Damage, which
retains original observed ownership. Sources separate the triggering attack from
the application that supplied the bonus. Unknown providers remain Unattributed;
unmeasured amplification preserves provisional original ownership with a warning.

Attribution 1 uses log schema 7/checkpoint schema 5; HP accounting 2 and
amplification 3 are unchanged. Older actual history remains in Actual Damage;
credited Damage/Amplification/Support share a new coverage boundary, visibly
partial for legacy encounters. Original checkpoint files survive loading and
normal saves retain backups. No historical attribution is invented.

Host checklist: confirm preview.13 with an unmodded guest. Apply Vulnerable then
hit for 6 against enough HP: expect attacker 6/provider 3, team 9. Repeat with
shared active stacks, self application, fully blocked and mixed destinations, and
an already lethal hit. Check credited sources, percentages, Actual Damage and
Support subset. Repeat available poison/Doom and save/resume scenarios; keep
canonical event IDs. Visual/native verification and release gates remain pending.

## 0.3.0-preview.14 - hover details

Hover any player's name, bar or percentage for values, credited/actual totals,
Poison/Doom, amplification, support and sources for the selected metric and scope.
The permanent details/diagnostics footer is removed and the panel shrinks to its
rows. Partial/Incomplete appears beside the scope tabs when needed; hover that
badge or the title for diagnostics. Source lists wrap and shorten with `+N more`
when space is limited. Open details refresh as damage arrives.

509 automated checks pass, including installed-game contracts and existing
multiplayer observer regressions. In-game visual verification remains pending:
after deployment confirm preview.14, hover name/bar/percentage, switch metrics
and scopes, check support recipients and warnings, then collapse, use F8, resize
and move near screen edges. Check that details disappear when hidden and that
combat interaction and row order remain stable while hovering. Accounting,
attribution, log and checkpoint versions are unchanged.

## 0.3.0-preview.15 - simplified combat view

All eight metrics are grouped by Damage and Attribution, preserving saved IDs.
Each bar shows one centred selected-metric total, including zero, with Party %
on the right. The labelled colour key follows the selected metric. Player hover
shows relevant values and sources only; Info provides help, version and technical
diagnostics. Partial/Incomplete remains in the header even while collapsed.

Native visual verification remains pending. After deployment confirm preview.15
in Info. Check two/four-player layouts at 1280x720 and 1920x1080, minimum/default
width, long names, large totals, zero/narrow segments and screen edges. Exercise
all eight metrics in Fight/Run, source trimming, unavailable attribution and
legacy history warnings. Verify the 6-to-9 Vulnerable split still credits 6/3,
plus poison, Doom, self bonuses and teammate support. Check live hover stability,
scrolling, collapse, F8, dragging, resizing and saved selections. Accounting and
storage/log schemas are unchanged. Automated checks do not establish native UI
or multiplayer correctness.

## 0.3.0-preview.16 - Effective Damage

Effective Damage defaults on new installations; existing saved selections remain.
HP always counts. Block waits until a block-respecting HP hit breaches the same
enemy's shield. Retained block and exact breaks can wait across turns; top-ups
above zero preserve pending ownership. New shields after zero, natural expiry,
death and combat end discard unqualified credit. Poison/Doom cannot unlock it.
Provider splits remain intact. Hover for qualified/pending amounts; pending block
is excluded from totals and percentages. Older effective history is partial.
Existing views remain available. Effective version 1/checkpoint 6/log 8 preserve
existing accounting and attribution versions. Build and 530 checks pass.

Native verification remains pending: select Effective Damage after deploying and
check ordinary/retained shields, top-ups, exact breaks with later HP, new shields,
bypass damage, multiple enemies, summons and multi-hits. Verify teammate/provider
credit, sources, Fight/Run, save/resume and failed-attempt replacement. Repeat as
host/guest with an unmodded teammate and preserve logs. Automated signature checks
do not establish gameplay correctness.

## 0.3.0-preview.17 - withdrawn (save-loading failure)

Run statistics now bind to the actual JSON payload written/read by the game's
save store. Fingerprint v2 preserves all saved values and sorts object keys.
Statistics are frozen before saving and published only after success. Nested
multiplayer loads restore once, using the accepted primary/backup payload before
canonicalization. Stale save completions cannot replace newer state. Semantic
run identity preserves history when only the runtime state object changes.

Checkpoint schema 7 uses a separate fingerprint-v2 directory. Legacy files remain
untouched; this fixes future restarts only and does not recover already-missing
history. An unmatched payload remains visibly partial, never borrowing the latest
checkpoint. HP accounting, attribution and effective-damage versions are unchanged.

Build/package and 544 checks pass, including production registration, byte capture,
exception preservation, stale completion rejection and payload checkpoint roundtrip.
Those automated results did not establish startup safety. This version caused
false corruption handling and reported progress loss. Do not package, deploy or
run it. See the incident record below.

## 0.3.0-preview.18 - startup save-loader repair

Preview.17 patched LoadSaveFromPath<SerializableRun>, a shared generic body. In
native startup this also changed profile/progress schema interpretation, falsely
rejecting valid profile v2 and progress v21 against run schema v16. The game
renamed originals to OLD/FUT.corrupt and created replacement data. Preview.18
removes all generic loader patches. Scoped non-generic I/O captures only run-save
filenames; successful outer run loads select the captured payload.

549 checks pass, including production registration leaving generic run/profile/
progress loaders unpatched and ignoring non-run payloads. Native startup/reload
verification remains pending. The schema/accounting versions are unchanged.

The attempted recovery failed to restore the user's visible progress. The
one-off recovery script is retired and always exits without accessing saves.
Do not rerun older copies. Save archives and recovery backups remain untouched.

## Incident cleanup

Preview.17 is blocked by packaging/deployment and its workspace ZIP is quarantined.
Preview.18 native persistence verification remains pending; packaging success is
not evidence of startup, progress preservation or restart correctness. Cleanup
makes no changes to installed mods, saves or Steam Cloud and attempts no recovery.
See [the incident record](validation/storage-incident-2026-11-03.md).

## 0.3.0-preview.19 - compact top row

Metric dropdown, resize arrow and collapse now share the top row; Damage Meter,
Partial and Info labels and the bottom resize row are removed. Left-drag the
arrow to resize; right-drag it to move. Hover the arrow for version, controls and
diagnostics. Relevant data-quality notices remain on dropdown/player hover.
Layout preferences, metrics and accounting are unchanged. Native visual checks
remain pending: verify minimum width, dropdown selection, arrow resize/move,
collapse, F8 and tooltips before/after changing scopes.

## 0.3.0-preview.20 - grab handle

Left-drag the new grab handle beside the resize arrow to move the meter. The arrow
now resizes only. Both controls remain available while collapsed; position is saved
on release. Native visual verification remains pending: check move/resize, release
outside the panel, viewport edges, collapse/F8 and position after restarting.

## 0.3.0-preview.21 - collapsed dropdown

The collapse toggle is now on the left. Collapsing hides the dropdown and body;
expanding restores both and preserves the selected metric. Grab/resize controls
remain available. Native checks pending: toggle, saved collapsed state, movement,
resizing, F8 and expansion without losing metric/scope selections.

## 0.3.0-preview.22 - button-only collapse

Press minus to shrink the meter to just its plus button. Resize and grab handles
are hidden while collapsed. Expanding restores the previous width and controls.
Native checks pending: repeated toggles, collapse after resizing, restart while
collapsed, viewport resizing and F8. The saved expanded width must be preserved.

## 0.3.0-preview.23 - solo meter

Single-player hides the character-name and party-percentage columns and their
hover text. Bars use the freed space; Unattributed retains its label. Multiplayer
presentation stays unchanged, including one-player multiplayer sessions. Native
checks pending: solo Fight/Run across metrics, Unattributed rows, solo/multiplayer
transitions, new rows, minimum width, collapse/expand, F8 and dragging/resizing.
No changes to saves, accounting or capture hooks.

## 0.3.0-preview.24 - structured tooltips

Player hover now shows a metric/scope header, prominent total, aligned breakdown,
source table and separate explanatory/warning notes. HP numbers are red, block
blue, bonuses orange; combined totals stay neutral. Combined views show HP/Block/
Total columns; single metrics use one Value column. Support lists recipient/source
with bonus destinations. Already included and pending/excluded amounts are clearly
separated. Solo hides player identity and party share. Long source names wrap and
lists trim to twelve or fewer entries within viewport height, with +N more.

Native visual verification remains pending: check every metric in Fight/Run,
solo/multiplayer, long names, large/fractional values, zero/unknown data, warnings,
live updates, screen edges and 1280x720/1920x1080. Verify colour readability,
column alignment, wrapping, trimming and closure on collapse/F8/row removal.
Capture, accounting and save handling are unchanged.

## 0.3.0-preview.25 - character icons, class colours and HP ordering

Multiplayer rows show the game's small character icon before the player label.
Rows are ordered by total HP damage, highest first, regardless of the selected
metric; equal HP totals use a stable player-ID order. Bars use character colours:
HP and shield damage share the supplied lighter shade, and amplification uses
the supplied darker shade. Ironclad is red, Silent green, Regent orange, Necrobinder
pink and Defect blue. There is no separate shield or blue Block segment. Native
visual verification remains pending.

## 0.3.0-preview.26 - compact player rows

The character icon remains at the left of each multiplayer row. The username is
drawn at the start of the filled bar, with rounded damage and party share at the
right (for example, `153 | 62.7%`). The Party % column heading is removed and the
bar legend reads HP / Amp / %. Damage labels are rounded to whole numbers; hover
details retain the underlying precise totals. Native visual verification remains
pending.

## 0.3.0-preview.27 - two-color damage bars

Shield damage is included in the HP-colored portion of the bar instead of being
drawn as its own segment. Bars show only HP (including shield damage) and
amplification, matching the HP / Amp / % legend. Amplification uses each
character's darker supplied color. Accounting and effective-damage qualification
are unchanged.
