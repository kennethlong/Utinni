# Phase 15 Deferred Items (executor-discovered, out of scope)

| Discovered during | Item | Why deferred |
|-------------------|------|--------------|
| 15-14 Task 1 (full-suite verify) | `FindPatternHarnessTests.GetVtbl_WithD3d9Loaded_ReturnsNonZero` fails locally: `Utinni_GetVtbl()` returns 0 because the dummy-device `CreateDevice(HAL)` has no graphics adapter available in the headless test process (the test's own comment documents this exact caveat and even pre-stages a `[Fact(Skip=...)]`). | Pre-existing, environment-dependent (graphics adapter), unrelated to the WorldSnapshot undo change this plan touched. SCOPE BOUNDARY: not caused by this task's files. 695/695 non-harness tests pass including the 8 WorldSnapshotCommandGuard facts. Candidate fix: apply the documented `[Fact(Skip=...)]` or gate on adapter availability — left to a CI-hygiene plan. |
