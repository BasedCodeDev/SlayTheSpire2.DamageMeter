# Native regression fixtures

`native-traces-v3.jsonl` contains two lethal damage/trace pairs from the local
0.3.0-preview.4 host log `a62760df8aba42dbb84c4e380d914a1b.jsonl`, game v0.107.1,
schema 3/accounting 2, captured on 2026-11-01.

| Fixture event | Original canonical event | Original damage sequence |
| --- | --- | --- |
| recorded-1 | ecadc4ca3db74fc1b56bcea9ec3d2cc8 | 135 |
| recorded-2 | c1e8b86e7f06469eb4dfceac32c50276 | 198 |

Player identities become A; Unattributed ownership is preserved. Session/event
IDs are replaced, unrelated fields omitted, and neutral modifiers removed.
Native numerical values and provider weights are unchanged. These fixtures
test observed log consistency, not complete native modifier correctness or guest
compatibility. The original local log is not distributed.

The JSON fixtures cover earlier stack-application and HP-normalisation repairs.
