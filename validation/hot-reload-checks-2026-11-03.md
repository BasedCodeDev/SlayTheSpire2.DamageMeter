# Development hot-reload checks — 2026-11-03

Isolated automated checks pass for preview.25. This is not gameplay or save-safety
certification. No live installation, existing saves or Steam Cloud were modified
by this work.

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
