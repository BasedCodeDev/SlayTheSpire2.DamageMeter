# 0.3.0-preview.5 verification checkpoint

2026-11-01; installed target game v0.107.1; accounting version 2.

- Release build and package succeeded; 152 automated and installed-assembly
  checks passed. ZIP manifest identifies 0.3.0-preview.5.
- NuGet vulnerability lookup was unavailable (NU1900 warning); compilation and
  tests completed using cached dependencies.
- The pending stable evidence file was rejected with six pending gates, as
  required. No live gate was promoted to passed.
- The analyser snapshot of the existing **preview.4** host session
  `a62760df8aba42dbb84c4e380d914a1b.jsonl` contained 66 matched traces, zero
  consistency failures and zero parse failures. This is prior-version observed
  consistency, not preview.5 gameplay verification. Unknown ownership and
  unexercised scenarios remain unresolved.

Local generated artifacts: `artifacts/package-preview5.txt`,
`artifacts/trace-report-preview4.json`, `artifacts/stable-gate-preview5.json`,
`artifacts/Based.Spires2DamageMeter-0.3.0-preview.5.zip`.

Preview.5 has not been deployed by the agent. Native startup/adapter eligibility
and ordering, full modifier interactions, multi-receiver amplification,
host/guest validation, checkpoint lifecycle and resolution-specific UI checks
remain pending. See DESIGN.md and validation/README.md for the next work.
