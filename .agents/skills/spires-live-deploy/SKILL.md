---
name: spires-live-deploy
description: Automatically publish completed mod implementation to a running compatible Slay the Spire 2 development host before the final response, implicitly required by project AGENTS.md.
---

# Live deployment

Run once after completing mod implementation and passing relevant checks. Skip
planning, questions, documentation-only or test-only work, incomplete/failing
implementations, and explicit requests not to deploy.

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .agents/skills/spires-live-deploy/scripts/deploy-live.ps1
```

This builds the entire current checkout, including existing uncommitted changes.
Use `-GameDir` and `-LogDirectory` for non-default locations. The helper publishes
only into a running compatible development host and checks new logs for ten seconds.

Never fall back to ordinary deployment. Do not close, launch or restart the game,
replace host/contract binaries, modify saves, or bypass failing checks. Report
setup/restart requirements and stop. Do not republish after timeout or failure.
Filesystem permissions still apply; use the execution tool's approval mechanism
when required. Do not pull, commit, push or bump versions automatically.

Report the current-working-tree build identifier and outcome: activated,
published but activation unconfirmed, skipped, setup/restart required, or failed.
Publication does not prove activation, visual correctness or gameplay/save safety.

See [the hot-reload guide](../../../docs/hot-reload.md). This is an agent
workflow, not a background completion hook.
