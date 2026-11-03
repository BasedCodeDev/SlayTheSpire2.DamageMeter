# Development hot-reload checks — 2026-11-03

The initial preview.25 checks below were isolated automated checks, without a live
installation or changes to existing saves or Steam Cloud. Later preview sections
record subsequent fixes and observations; this is not save-safety certification.

The native runner uses an isolated export of the installed game's custom engine,
which reports `4.5.1-m.12 (custom_build)`. It loads a separate fixture scene rather
than a gameplay run. The fixture uses real host/contracts/runtime assemblies and
synthetic meter state and task completions.

Passed scenarios:

- 50 initial native load/UI/calculation/dispose/unload cycles with a stable test
  Harmony hook.
- 50 real-runtime replacements alternating UI text and calculation code, retaining
  totals, deduplication, pending block, selected metric/scope, layout, collapse and
  F8 state. Tooltip windows open before replacement and close on retirement.
- Damage/source/save task wrappers survive replacement; native save exception
  identity is retained. Private runtime anonymous log values serialize through each
  generation without retaining its assembly.
- Broken DLLs, incorrect hashes, incompatible host/API/state, malformed manifests
  and throwing/null/parented replacement UI retain the previous runtime and UI.
- Duplicate and rapid publications coalesce. An actual filesystem notification
  activates an atomic replacement before the one-second polling fallback.
- Retired contexts collect after the successful and rejected replacements.
- Normal/development packaging, existing accounting and installed-game contract
  checks, Git-independent host/contract hashes and isolated deployment checks pass.

Commands and implementation boundaries are in [the development guide](../docs/hot-reload.md).
Reproducible local output is under `artifacts/reload-gate`, `artifacts/host-stability`
and the deployment fixture directories. The primary native report is
`artifacts/reload-gate/result.txt`.

Pending: real combat and host/guest acceptance, startup/progress preservation and
save/resume/restart certification against complete isolated game fixtures. Keep
the historical incident warning and existing save-safety gates.

## Preview.37 combat-entry dispatch repair

The live preview.25 log recorded `MissingMethodException: Probe.HandleHook` while
Bronze Scales applied Thorns during room entry. The host/contract/payload hashes
matched the deployed manifest. This was an omitted dispatch route, not a partial
copy. A compiled-boundary audit also found `EffectiveCapture.AfterClearCompleted`
missing; both routes are restored in preview.37.

Host guards now contain synchronous prefix/postfix dispatch failures, identify
the operation and active build, and mark coverage incomplete. They do not await,
replace or suppress the native task. API/state versions and save policy are
unchanged. A full closed-game installation is required because the host changed.

The packaging check inspects compiled host/contract calls and both executable
runtime dispatchers. It verifies all 180 required keys and rejects missing routes
and unverifiable dynamic keys, including misleading literal arguments. Unknown
operations still throw. Isolated native checks exercise real hook metadata and
a synthetic Thorns setup observation, injected prefix/postfix failures, original
task fault/cancellation identity, and the same checks after runtime replacement.
The existing 50 prototype and 50 real-runtime replacement cycles still pass.

Reports: `artifacts/dispatch-fix-checks.log`,
`artifacts/dispatch-fix-package-normal.log`,
`artifacts/dispatch-fix-package-development.log`, and
`artifacts/reload-gate/result.txt`. Actual Bronze Scales combat entry, a normal
encounter and multiplayer join/resume remain manual acceptance checks. These
fixtures do not certify a complete gameplay run or save safety.

## Preview.38: first-attack JSON binding failure

Seb confirmed preview.37 combat entry and meter display. The first multiplayer
Strike then faulted in `NativeDamageCaptureLogic.BlockPostfix`, followed by checksum
ID 3 divergence and disconnection. The game log records `MissingMethodException`
for the existing `JsonElement LogValue.Freeze(object)` helper. This was not an
incomplete deployment: installed hashes matched the manifest and package.

The regression uses Godot's actual `PluginLoadContext` with the installed
`sts2.deps.json`, loads the host into that game context as ModManager does, then
loads the collectible runtime. The unfixed code reproduces the exact exception
through the real patched `Creature.DamageBlockInternal`. The host's JSON assembly
is loaded by `PluginLoadContext`; the runtime's JSON reference binds to a separate
default-context assembly. The helper's return type therefore has a different
identity despite its identical printed signature.

With host-context dependency resolution, the real block method returns 6, reduces
block from 10 to 4 and records the expected JSON phase, both before and after
runtime replacement. Checks assert that host and runtime share the game's JSON
assembly while a separate copy still exists in the default context. The native
gate also injects faults into lower-level prefix/postfix/finalizer callbacks,
verifies parent-state restoration and checks native task/output preservation.

Reproduction and build logs are under `artifacts/binding-diagnosis`; the native
report remains `artifacts/reload-gate/result.txt`. These checks use isolated
synthetic creatures and do not load or alter saves or Steam Cloud.

### Deployment and limited multiplayer observation

Normal and development packages each passed 597 checks. The isolated native
report contains 140 passing assertions, including 50 real-runtime replacements,
the actual-game-loader reproduction and native failure/output/task checks before
and after replacement. The 12 isolated deployment checks also passed.

Seb closed the game and requested redeployment. Preview.38 was installed in
development mode and launched; installed host, contracts and runtime hashes match
the manifest. Active build: `BCEC645B82CB7E8E932C6CF50048CED8E432CD90B07D9FEC72AC8351253F444F`.

Seb then played the first Strike, another action and an end turn, and confirmed
continued multiplayer play and meter operation. A diagnostic snapshot records
completed Strike actions at checksum IDs 1, 2 and 3, end-turn actions, later enemy
and player turns, and continued actions through checksum ID 24. The meter session
identifies preview.38 as Client and contains 17 damage events and 13 damage traces
with block/HP phase JSON. Both logs have zero matching observer/helper-binding,
action-exception, divergence or disconnect messages at this checkpoint.

Read-only diagnostic snapshots (ignored artifacts, not tracked save data):

- `artifacts/binding-diagnosis/live-preview38-godot.log`, SHA-256
  `3400557AC6B0A1F6C9679FE1F6DD3680B3A371F84F7F799CF978551D11D5715D`.
- `artifacts/binding-diagnosis/live-preview38-meter.jsonl`, SHA-256
  `BBA6272A3DCE278743EBB65130FE9AB52F5F4DF2FEB4C78642D9B33EDF4BDDA0`.

This supports the first-attack fix and continuation in this client session only.
Broader multiplayer acceptance (including full host/guest scenarios and live
multiplayer reload) and save/progress safety remain **incomplete**. No save or
Steam Cloud files were manually edited, restored, deleted or uploaded by the agent.
