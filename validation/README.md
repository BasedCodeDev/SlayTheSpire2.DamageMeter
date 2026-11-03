# Validation workflow

The current build is a preview. All stable release gates remain pending.

Development hot-reload implementation checks are recorded in
[the preview.25 check report](hot-reload-checks-2026-11-03.md). The isolated runtime
reload checks do not certify live gameplay, multiplayer or persistence.

## Inspect recorded logs without changing them

```powershell
dotnet build tools/TraceReport
dotnet run --no-build --project tools/TraceReport -- "C:\path\session.jsonl"
```

Pass multiple filenames to compare sessions. Reports group by observed game/mod
version, accounting version, session, role and fight. Evidence references contain
filename, line, event ID and sequence. A running game's log can be read with
shared access; rerun after combat ends for a complete snapshot. A partially
written last record is reported as a parse failure, not silently ignored.

Exit 1 indicates parse/consistency failures. Exit 0 does **not** certify native
correctness: inspect unsupported/missing trace counts and unknown shares, and
record scenarios that were not exercised. Older numerical schemas/accounting
are counted as excluded and never qualify as current consistency evidence.

## Modifier inventory

`modifier-manifest-v0.107.1.json` lists effective callbacks on concrete
AbstractModel listeners, including inherited implementations and enchantments.
Each entry records module ID, method IL hash, direct dependencies, classification,
adapter and required cases. Pending status means it has no completed native
interaction evidence. A frozen-output candidate is not a passing validation.

Regenerate against the local game assembly:

```powershell
dotnet build tools/GameAudit
dotnet run --no-build --project tools/GameAudit -- "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64" --manifest > validation/modifier-manifest-v0.107.1.json
```

The seven HP-loss adapters snapshot state for Buffer, Hardened Shell, Intangible,
Slippery, Beating Remnant, The Boot and Tungsten Rod. Module/IL hashes must match;
each callback's observed output must equal the adapter's output. Eligibility
and scalar reads never invoke damage callbacks again. Unknown shapes, missing
state and mismatches preserve actual damage and suppress amplification credit.
Redirection is observed but multi-receiver amplification remains unmeasured.

## Record native results

For each manifest entry, record active and inactive cases, relevant target/card/
provider conditions, positive/negative stack amounts, block/lethal boundaries,
overlapping modifiers and unsupported interactions. Include host and guest
sessions with unmodded peers. Do not turn an unavailable case into a pass.

Test HP-loss caps both when already binding and when only the counterfactual
crosses the threshold. Test Buffer with consumed block, The Boot below one HP
and below its minimum, Hardened Shell/Beating Remnant turn budgets, Tungsten Rod,
block bypass, enchanted cards, Osty redirection and overflow. Retain phase,
receiver and canonical event references. Native callback eligibility and ordering
remain pending until these cases run in-game.

Also validate save/resume, replayed-encounter replacement, delayed saves,
consecutive runs and old-accounting exclusion. At 1080p and 1440p inspect mixed
and tiny segments, long names, collapse/expand, resizing and partial-data states.

## Stable packaging evidence

`release-evidence.json` schema 2 requires matching mod/game/accounting versions.
Each passed gate needs `reviewedBy`, an ISO `reviewedAt`, and all scenario IDs
defined in `ReleaseEvidence.RequiredScenarios`. Each scenario needs `status`,
`expected`, `observed` and references containing a repository-relative `path`,
file `sha256` and a specific `locator` (line/event or screenshot region).
Record full per-entry audit results in referenced artifacts. The gate checks
record completeness and artifact integrity; a human review must establish the
truth of the results. An analyser report alone is insufficient evidence.

Example shape (illustrative only; do not copy as a passing result):

```json
{
  "status": "passed",
  "reviewedBy": "reviewer",
  "reviewedAt": "2026-11-01T12:00:00Z",
  "cases": [{
    "scenario": "direct",
    "status": "passed",
    "expected": "6 HP removed, 3 diagnostic overkill",
    "observed": "6 HP credited and displayed",
    "references": [{"path": "validation/results/case.jsonl", "sha256": "SHA256_OF_FILE", "locator": "line 12, event abc"}]
  }]
}
```

Run `dotnet run --project tools/TraceReport -- --release validation/release-evidence.json . 0.3.0`
to inspect the gate. Stable packaging runs the same check. Preview packages can
be produced with pending native gates; public publishing remains separate.

## Hot-reload save-safety work (not certified)

Independent fixture checks:

```powershell
dotnet run --project tests/SavePolicyFixture/SavePolicyChecks.csproj
dotnet run --project tests/SaveCallbackFixture/SaveCallbackChecks.csproj
powershell -ExecutionPolicy Bypass -File scripts/check-native-save-fixtures.ps1
```

Path tests pass 15 checks. Production callback source, with dependency substitutes,
passes 12 isolation checks for missing/throwing runtime, finalizers, native save
errors and cancellation. These do not certify the complete candidate. The native
fixture currently crashes before scene entry (exit -1073741819); its schema cases
have not passed. Logs are under artifacts/save-safety-gate. The harness uses only
a synthetic ISaveStore, never Steam or existing save directories. The main project
build passes, but the refactor remains in progress.

The new runner and callback fixtures are scaffolding, not a completed native
integration suite. Remaining: fix harness initialization, use complete candidate
runtime instead of dependency substitutes, populate progression fixtures, verify
runtime reload deferral/rollback, then record isolated solo/host/guest gameplay
restarts. No live account or existing saves may be used. No recovery is authorized.

`save-safety-evidence.json` explicitly lists pending cases. `scripts/check-save-safety.ps1`
requires host/contract/payload/game/engine paths and checks exact binary hashes plus
reviewed hashed scenario evidence. Keep the README warning until that gate passes.
No candidate version bump or deployment is justified by the current fixture results.
