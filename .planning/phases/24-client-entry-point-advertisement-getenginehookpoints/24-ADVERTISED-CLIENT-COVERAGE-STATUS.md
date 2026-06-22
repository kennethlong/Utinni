# Advertised-Client Coverage Status & Remaining-Mechanism Backlog

**Status:** HANDOFF — post-live-smoke reconciliation (2026-06-22). Companion to
`24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md` (the contract) and
`2026-06-15-utinni-dx11-hookpoint-advertisement-spec.md` (the graphics twin).
**Author:** Utinni Phase 24 close-out.
**Audience:** the follow-on milestone (both the `swg-client-v2` provider instance and Utinni).
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-22-utinni-advertised-client-coverage-status.md`.

---

## 0. Why this doc exists

The engine-entrypoint SPEC (dated 2026-06-20) was written *before* the Phase-24 live-smoke. It
documents the **full ~230 RVA catalog** as the target and frames full population as "a second pass."
The live-smoke then established what is actually live, what is gated off, and — crucially — that a
large fraction of the spec's ~230 RVAs **cannot be `&fn` table rows at all** and need a *different
mechanism*. This doc is the reconciled, current-state backlog so the follow-on milestone starts from
fact, not from the pre-implementation spec's optimism.

**Headline:** the advertised client (`SwgClient_r.exe`) runs **RENDER-only** today. ~78 endpoints are
cleanly advertised (the `utinni_engine_hookpoints.inc` set). The rest are either gated off on the
advertised client or are architecturally un-advertisable as a plain function pointer. "Full retirement
of hardcoded RVAs" is NOT achieved and is NOT a simple catalog append.

---

## 1. What is LIVE today (advertised + consumed, RENDER path proven)

The provider's `utinni_advertise.cpp` populates the shared `utinni_engine_hookpoints.inc` set (~78
names across 17 groups), and the Utinni resolver overwrites those literals by name before
`createDetours()`. Live-proven on `SwgClient_r.exe` (Checkpoint 1, 2026-06-22): inject → detour →
render to login, no crash, `endpoints: resolved 77/77 by name`.

Advertised groups: `config` (3), `client::clientMain` (1), `game` (12), `graphics` (15),
`cuiManager` (5), `cuiIo` (3), `consoleHelper` (1), `commandParser` (3), `extent` (1), `object` (12),
`objectTemplate` (4), `worldSnapshot` (6), `camera` (5), `memory` (2), `audio` (2), `treeFile` (1),
`report` (1). (Exact names: `utinni_engine_hookpoints.inc`.)

This set is sufficient to boot + render the overlay. It is NOT sufficient for the scene/editor
features (those subsystems are gated off — §2).

## 2. What is GATED OFF on the advertised client (and WHY) — the real remaining work

On the advertised client, `swg::endpoints::installable()` skips any subsystem whose primary target did
not resolve from the catalog, and `createDetours()/createPatches()` skip the **MISC + INPUT** groups
wholesale (`isAdvertisedClient()`). Those un-resolved endpoints fall into four mechanism buckets — none
of which is "the provider just hasn't typed the row yet":

| Bucket | Examples | Why not a plain `&fn` row | Mechanism the follow-on needs |
|--------|----------|---------------------------|-------------------------------|
| **Multiple-inheritance / inflated PMF** | ALL `groundScene::*` (via `NetworkScene : Scene, MessageDispatch::Receiver`); `cui::chatWindow::*` | member PMF is > `sizeof(void*)` → fails the `pmfToVoid` sizeof guard; the raw code address isn't a usable `&fn` | a free-function **`__thiscall` thunk** per method on the provider side, advertised as the thunk's address (the `.inc` flags these "needs a __thiscall thunk, 37-03") |
| **Virtual methods** | `object::{addToWorld,removeFromWorld,setParentCell}`, `cui::io::processEvent` | the symbol is a vtable slot, not a stable function address | resolve off the **live vtable** consumer-side (Utinni already does this for D3D9 Present), or a provider thunk that calls the virtual |
| **Inline / no-ODR-address** | `game::g_mainLoopCounter` (only inline accessor `Game::getLoopCount`), `object::move_o` (inline) | the compiler emits no out-of-line address to take | provider exposes a real **out-of-line accessor**, or Utinni reads the underlying global differently |
| **File-local exe statics** | `client::{wndProc,writeMiniDump,writeCrashLog,setupStartDataInstall}`, `config::{setModalChat,getModalChat}` | symbol has internal linkage in the exe → not addressable from `utinni_advertise.cpp` | provider adds an **external-linkage shim** (a one-line forwarder) |
| **Mid-function byte/JMP patches** | chat-context routing (Issue #11), UI Y-axis cascade NOP, debug-camera input-suppress NOP, worldSnapshot `.trn`-name NOP, crashlog inline hook, `shader::popCell` | Utinni patches *into the middle* of a function body at a Pre-CU instruction offset — cannot be expressed as a function entry point at all | a **function-entry equivalent**, a **cooperative engine toggle** (e.g. a real "modal chat context" setter), or acceptance that the specific editor is SWGEmu-only (SPEC §8 #1) |

**Implication for "RVA retirement":** the ~78 cleanly-advertisable endpoints can retire their RVAs.
The MI/virtual/inline/static buckets retire only after the provider adds thunks/shims. The
mid-function-patch bucket may never be a pointer contract — it needs per-feature redesign or a
cooperative engine API, decided when each dependent editor is smoke-tested on the advertised client.

## 3. Prioritized follow-on backlog (suggested order)

1. **DX11 live overlay** — separate, blocking the Phase-18/19 live-smokes. See
   `24-DX11-ADVERTISED-CLIENT-GAP.md`. (Highest value: it's the one the milestone was nominally about.)
2. **`groundScene::*` thiscall thunks** — unblocks the terrain editor + the TJT scene-change repro path
   on the advertised client (today the scene subsystem is dark there). Provider-side thunks +
   re-advertise; Utinni drops the `groundScene` `installable()` skip.
3. **`client::*` external-linkage shims** — crash-log / minidump / wndProc; small, mechanical.
4. **Virtual `object::*` + `cui::io::processEvent`** — resolve off the live vtable consumer-side
   (preferred — no provider change) vs. provider thunks. Decide the pattern once, apply to both.
5. **Mid-function-patch features** — triage per feature: Issue #11 chat routing, UI cascade,
   debug-cam suppress, `.ws`/`.trn` load. Each needs a cooperative API or is accepted SWGEmu-only.

## 4. What is explicitly NOT in scope (do not advertise)

Per SPEC §6 "Endpoints you do NOT need to advertise": the D3D9 device-vtable hooks (Utinni harvests
those at runtime), `D3DXCompileShader` (lives in the D3DX DLL), and the DirectInput8 vtable writes
(patched on the COM instance). These are Utinni-internal and never want a provider symbol.

## 5. Acceptance for "full coverage" (so the follow-on knows "done")

The shared `.inc` grows only by names that are genuinely `&fn`-addressable (after the provider's
thunks/shims land), the Utinni resolver's coverage assertion stays green for that grown required set,
and Utinni drops the corresponding `installable()` / group skips so the advertised client installs the
same hook set SWGEmu does — **except** the mid-function-patch bucket, which is tracked per-feature.
Full parity is reached when no subsystem is `isAdvertisedClient()`-skipped except the documented
mid-patch features, AND the DX11 overlay renders (the gap doc).
