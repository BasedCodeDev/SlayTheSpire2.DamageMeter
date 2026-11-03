# Save-loading incident - 2026-11-03

Preview.17 patched LoadSaveFromPath<SerializableRun>, a shared generic method.
Native startup consequently interpreted profile/progress data against run-save
schema requirements. Logs show valid profile schema 2 rejected as too old and
progress schema 21 rejected against run schema 16. The game renamed originals
OLD/FUT.corrupt and created replacement data. A second startup repeated this.

Preview.18 removes generic loader patches. Automated registration checks assert
that generic run/profile/progress loaders remain unpatched. These checks do not
prove native startup or persistence safety.

An incident-specific script attempted restoration. Seb reported that all visible
progress was lost afterwards. Inspection found populated pre-incident modded
progress (133 card-stat and 33 epoch records), an empty replacement, preserved
recovery copies, and a separate populated unmodded save. This establishes surviving
records, not completeness, successful restoration or the exact recovery failure.

## Cleanup decision

Cleanup only; no further recovery. Retire the recovery script, reject preview.17
in packaging/deployment, quarantine its workspace ZIP, preserve diagnostic logs
and real-save copies, and remove disposable audit tools and synthetic fixtures.
Leave all installed mods, game saves, Steam Cloud and recovery backups unchanged.
The rejected build and serialization sample are retained under ignored workspace
artifacts/incident-evidence; they are not packaged or tracked as source.

Native startup, progress preservation, host/guest restart and repeated-resume
verification remain pending. Existing automated pass counts are not evidence that
those gates passed. Do not relabel this cleanup as a successful recovery.

## Cleanup verification

Completed cleanup without deployment or game launch. Preview.17 ZIP is quarantined
and confirmed by its embedded manifest; the normal distribution copy is absent.
The serialization sample was preserved before deleting its disposable audit tool.
Recovery command is a fail-closed stub. Fixture checks reject preview.17 in both
manifest and project versions before packaging/deployment and confirm retired
recovery cannot create outputs. Normal preview.18 packaging succeeds with 550
checks, including prior regressions. Before/after SHA-256 comparison found zero
changes across 246 external save/recovery/install files. No recovery was attempted.
