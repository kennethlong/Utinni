# Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-21
**Phase:** 24-client-entry-point-advertisement-getenginehookpoints
**Areas discussed:** Binding scope, WR-05 consoleHelper::sendInput, EPA-04 coverage gate, Accessor-style globals, Diagnostic-globals reconciliation

---

## Binding scope

| Option | Description | Selected |
|--------|-------------|----------|
| MVP-critical subset first | config::loadOverrideConfig + graphics group + game core + cuiManager::render — kill crash, boot, render, scene repro; full catalog later (research-recommended) | |
| Full 79-name catalog now | Bind every advertised name; maximizes advertised-client coverage; pulls in name-mismatch + read-vs-call for all rows | ✓ |
| MVP + safe extras | MVP plus clean drop-in rows; skip read→call/thunk rows | |

**User's choice:** Full 79-name catalog now.
**Notes:** Combined with the two carve-outs (WR-05, below) → `s_bindings[]` = full `.inc` minus `consoleHelper::sendInput`.

---

## WR-05 — consoleHelper::sendInput (3-arg PMF stack-corruption risk)

| Option | Description | Selected |
|--------|-------------|----------|
| Defer — leave on RVA | Don't bind this phase (not on MVP path); file cross-repo provider-thunk task (research-recommended) | ✓ |
| Fix provider thunk now | Add swg-client-v2 thunk, then bind — cross-repo paired work | |

**User's choice:** Defer — leave on RVA.
**Notes:** The one intentional unbound `.inc` name; must be coverage-gate allow-listed so it doesn't read as drift.

---

## EPA-04 coverage gate (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| Compile-time subset assert | X-macro static_assert; fails BUILD on drift | ✓ |
| Catch2 unit test | Process-isolated resolve() against synthetic fixture | ✓ |
| Runtime log-and-degrade | Inject-time resolved/missing summary; degrade not crash | ✓ |

**User's choice:** All three layers.
**Notes:** Resolver factored to take a table ptr + binding list so it's testable without injection.

---

## Accessor-style globals (read→call adaptation)

| Option | Description | Selected |
|--------|-------------|----------|
| Defer — keep RVA reads | Leave global reads on RVA literals; verify none crash; check swapchain supplies overlay size (research-recommended if so) | |
| Adapt the critical ones now | Adapt read→call for boot/render/scene-path globals, defer diagnostic | ✓ (then escalated below) |

**User's choice:** Adapt the critical ones now — then, on the reconciliation question, escalated to ALL.

---

## Diagnostic-globals reconciliation (follow-up — "full catalog" vs "critical globals only")

| Option | Description | Selected |
|--------|-------------|----------|
| Defer diagnostic globals | Adapt+bind critical globals only; leave g_frameNumber/g_runningFlags on RVA read, not in s_bindings; record as carve-outs | |
| Adapt ALL globals now | read→call every advertised accessor global; only unbound name is consoleHelper::sendInput | ✓ |

**User's choice:** Adapt ALL globals now.
**Notes:** A global can't be bound without adapting its site to call (binding overwrites the slot with a function pointer; `memory::read` on it = garbage). So `s_bindings[]` = TRUE full catalog minus only WR-05.

---

## User correction — "retirement, what are we retiring"

The user flagged the word "retirement," emphasizing the new path must work **alongside** the existing
SWGEmu calling mechanism. Captured as **D-00** in CONTEXT: "retire" = per-client runtime override on the
advertised client ONLY; the hardcoded SWGEmu RVA literals stay in source permanently and the resolver is
a strict no-op when the export is absent. Nothing is deleted; the SWGEmu path is co-equal and load-bearing.

## Claude's Discretion

- Resolver shape: in-place overwrite of `pFn` literals (one resolver, one branch) — not a central map.
- `UTINNI_HOOKPOINTS_VERSION` stays pinned at 1; soft-warn on mismatch, resolve by name.
- EPA-03: Approach A (resolve `graphics::install` so existing `hkInstall`→`kickoff` fires).
- Plan decomposition: ~4 plans (resolver+tests / full-catalog binding+globals / EPA-03 decouple / live-smoke).

## Deferred Ideas

- WR-05 provider thunk (cross-repo, swg-client-v2) — later wave.
- x64 advertisement (Backlog 999.7 other half).
- Mid-function JMP/NOP/byte patches (cannot be `&fn`) — SWGEmu-only.
- `UI*::ctor` + MI-class ctors + `groundScene::*` (provider-deferred; keep RVA).
- Wider advertised-path coverage beyond 79 endpoints — later catalog waves (SWGEmu source path untouched).
