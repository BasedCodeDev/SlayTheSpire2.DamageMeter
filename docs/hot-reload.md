# Development hot reload

Close the game once and install the development host:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy.ps1 -Development
```

Enable the mod and follow the game's restart prompt if necessary. Subsequent runs
of that command build, check and publish a runtime while the game stays open.
`update.bat -Development` first pulls the current branch's configured upstream,
then performs the same deployment. `-GameDir` and `-WhatIf` are supported. The batch
wrapper still pulls when given `-WhatIf`; deployment itself publishes nothing.

Preview.37 fixes a missing combat-observation route in earlier hot-reload builds.
Its host also contains dispatch failures before they can abort room loading.
Preview.38 also fixes a JSON type-identity mismatch that faulted the first attack
under the game's assembly loader. Native damage/HP-loss/modifier/block observers
contain diagnostic faults while retaining native results, output arguments and
task failures/cancellation. Runtime dependencies resolve through the permanent
host's context rather than accidentally using separate framework assemblies.
Close the game for the first preview.38 installation; publishing only a runtime
cannot update the permanent host. The packaging gate checks all compiled
host/contract operation keys against the runtime's executable dispatch routes.

The watcher reacts to a completed deployment on the next available game frame,
including during combat. It never waits for combat or a menu transition. The
fallback poll runs once a second if a filesystem notification is lost. A callback
already executing finishes before the implementation switches.

## What can change

- `runtime/logic` contains the UI and accounting implementations.
- `runtime/capture` contains replaceable stateless native capture adapters.
- `src` retains shared state, typed hook entry points and Godot bridge classes.
  `host` owns the loader, pending work, registration lifetime and dispatch.
- `contracts` defines the permanent runtime boundary and compiles shared state
  from `src`. State objects contain data and forwarding methods, rather than
  references to replaceable runtime types.

Implementation changes behind existing interfaces reload immediately. Changing
host/contract code, hook signatures, state layouts or the build toolchain can
require a restart and full development installation. Close the game and rerun
`scripts/deploy.ps1 -Development` using the PowerShell command above; the script
installs the updated host/contracts and requests a game launch. The deployment
script checks the exact host and contract hashes before writing anything to a
running install.
Git revision metadata is excluded from those binaries so an ordinary pull does
not itself force a host replacement. Runtime builds retain symbols and version
information.

Normal builds use the same separation but load once, without an active watcher.
Normal deployment continues to require a closed game. The explicit build property
is `DevelopmentMode=true`; this is independent of Debug/Release optimization.
Use the packaging/deployment scripts to build both assemblies in order.

## Publication and activation

After successful activation, a green exclamation-mark button pulses beside the meter for
two seconds and stays visible until clicked to dismiss. Hover it for the loaded version
and build identifier. It stays visible beside a collapsed meter; F8 hides it
with the panel and pauses its animation. Near the left edge it appears above the
meter (or to its right at the top-left corner) to avoid covering controls.
Each successful reload shows it again. Startup and rejected updates do not trigger it.

Each build lives in `runtime/builds/<content-hash>` beneath the installed mod.
Deployment copies to a staging directory, verifies the contents, then atomically
publishes `runtime/current.json`. The previous pointer is retained as
`runtime/current.json.previous`. Never overwrite a loaded DLL in place.

The host validates hashes and API/state compatibility before loading a candidate
into a collectible assembly context. Game, Godot and contract assemblies are shared
with the running game. UI is prepared before the old UI is detached. Failed
preparation or activation keeps the previous implementation and UI.

Existing totals, source attribution, pending block, ownership, deduplication,
run history and layout stay in permanent objects. Pending game tasks also stay in
the host; their synchronous completion handlers use the active implementation.
Changing state compatibility requires a matching host installation; automatic
state migrations are not attempted.

Old UI, signals, tooltips and deferred callbacks are released after a successful
switch. Development logs and stored trace phases are frozen to plain JSON through
a cache-free snapshot writer: private payload types must never enter framework
serializer accessor caches. Use `Hosting.LogValue.Freeze` or shared contract DTOs
when adding serialized runtime data. Generation-local JSON options isolate the
remaining metadata. Diagnostic collections check retired assembly contexts;
more than two retained generations stops further reloads and requests a restart.

Hover the resize arrow for the loaded version and capture diagnostics. It does
not show hot-reload activation/rejection status or the build identifier; check
the game log for those details. Mod JSONL logs record successful activation as
`runtime_reloaded` (including the build identifier) and errors as
`runtime_reload_error`.

A deployment reporting **published** means the files are ready, not that the
running host has activated them. The host can still reject the candidate; failed
reloads retain the previous runtime and UI.

## Verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Development
powershell -NoProfile -ExecutionPolicy Bypass -File tests/DevelopmentDeployChecks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-host-stability.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-native-reload.ps1
```

The native runner exports an isolated fixture using the installed game's engine
executable. It exercises real runtime loading, UI, calculation changes, state,
pending task wrappers, rejection and unloading without loading a gameplay run or
using existing saves. It requires a Godot 4.5.1 .NET editor and Windows export
templates; pass `-Godot` and `-GameDir` to override this machine's defaults.

These fixtures do not certify live combat, host/guest networking, progress
preservation or save/resume behavior. Existing native acceptance and save-safety
gates remain pending; the historical save-loading incident warning still applies.

## Automatic agent deployment

Project agents implicitly follow the repository's `spires-live-deploy` skill
before their final response after completing mod implementation and passing
checks. No skill invocation or separate deployment request is needed. Planning,
questions, documentation-only/test-only work, unfinished/failing implementations
and explicit requests to skip deployment do not trigger it.

The workflow builds the current checkout, including uncommitted edits, and uses
`deploy.ps1 -Development -LiveOnly`. It requires an already running compatible
development host. A closed game, missing setup or changed host/contracts stops
automatic deployment; the agent never launches or restarts the game. Existing
manual deployment behavior is unchanged.

The helper checks new JSONL log events for ten seconds. It reports the build
identifier and activation, reload error, or publication with activation
unconfirmed. An unchanged build may produce no new event. Missing logs and
unconfirmed activation do not trigger a retry. Custom installations can use the
helper's `-GameDir` and `-LogDirectory` options. This is a project instruction
workflow, not an enforced background completion hook; execution permissions
still apply.
