# Phase 08 — Deferred Items

## Out-of-scope discoveries logged during 08-01 execution

| Category | Item | Discovered | Notes |
|----------|------|------------|-------|
| Pre-existing test flake | `NativeCallbacksHandleTests.Subscribe_DuringDispatch_NotInvokedInCurrentIteration_InvokedInNext` fails when run inside the full Release/x86 suite but passes in isolation. Surfaced during 08-01 Task 4 acceptance. Unrelated to any Formats/Iff code. Last commit on the file is `427f474` (Phase 3 R-G CR-01) — predates 08-01 by months. | 2026-05-28, during 08-01 Task 4 acceptance | Scope-boundary rule: not auto-fixed (does not affect 08-01's correctness, security, or ability to complete). Likely a timing/concurrency race in the cross-iteration subscriber-dispatch contract. Belongs with the existing GameCallbacks ForceGCCollect AV isolation work or a follow-on 06-04-style stability plan. |
