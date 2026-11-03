# Modifier dispatch repair — preview.12

The preview.10 session `16388aa32fb54a59b7545b651350ce1d` recorded 35 offensive
events as unsupported with a Creature-to-ValueProp cast failure and subsequent
chain mismatch. Weak/Vulnerable capture selected by instance alone entered
inherited cap callbacks, whose argument layout differs from the multiplier root.

Specialized handlers now require the exact model, method, signature and pinned
contract. Unrelated callbacks remain generic. Chain entry repeats the guard
before frame creation or argument access. Child-family validation also precedes
argument access. Invalid frames cannot produce partial chains or a secondary
reconciliation error. Failure reasons identify handler, model/method or root.

477 automated and installed-assembly checks passed; packaged DLL/ZIP identify
0.3.0-preview.12. Production bridge tests exercise inherited additive/cap
callbacks on Weak and Vulnerable, active injected trace context, malformed roots,
failed snapshots, preview bypass, unexpected children and parent restoration.
Injected contexts are isolated and restored in finally blocks. A negative
control reproduces the old cast. The preview.10 output-list regressions remain
in the full package gate.

`tests/fixtures/dispatch-failure.json` retains the observed count/reasons and an
anonymised numeric sample with source sequence provenance. It is not a replay
of historical amplification. No statistics were reconstructed.

Native gameplay retesting is pending; no native/stable gate was promoted and
the agent did not deploy or restart the game. Amplification 3, log schema 6 and
checkpoint schema 4 are unchanged. Follow README's preview.12 host checklist.
