# Provider Follow-Up — Detour Endpoints Need the REAL Entry, Not a Call-Through Thunk

**Status:** REVIEW FINDING (HIGH) on the Phase-38 handback
(`2026-06-22-utinni-provider-coverage-handback.md`). Action required on the provider side before the
Utinni consumer wires the affected endpoints.
**Author:** Utinni (consumer) review of the provider's Phase-38 coverage delivery, 2026-06-22.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-22-utinni-detour-vs-call-followup.md`.
**Corroboration:** the core claim was independently confirmed by a second model (Codex) — see §4.

---

## 1. The finding

The Phase-38 handback (and the original spec §2) treat every advertised row as an interchangeable
`{name, address}` — "from your side there is no difference [between detoured and called endpoints]."
**That is false for the endpoints advertised via a call-through forwarder thunk.**

For the MI / inflated-PMF classes (`groundScene`, `cuiChatWindow`), the provider advertised a **free
`__fastcall` forwarder** whose body calls the real method:

```cpp
static void __fastcall utinni_groundSceneUpdate(GroundScene* pThis, int /*edx*/, float t)
{
    pThis->update(t);   // call-through
}
```

That address is correct **only for endpoints Utinni CALLS** (Utinni invokes the thunk, it forwards).
It is **wrong for endpoints Utinni DETOURS.** A DetourXS prologue patch on the forwarder fires only
when someone calls *the forwarder*. The engine calls the **real** `GroundScene::update` directly (its
own call path never routes through the provider's thunk), so Utinni's hook **never fires** — the
detour is silently dead. It links, exports, passes `dumpbin`, passes the `static_assert (94==94)`
self-check, and the client boots and renders with no crash — but the editor's per-frame / input hooks
do nothing on the advertised client. This is the worst kind of bug: green everywhere, broken in the
one place it matters.

This is the exact generalization of your own review item **WR-01** (the 5 inline `object::*` rows are
detour-bypassed at inlined sites). Same rule: **a forwarder/inline address is fine to CALL, useless to
DETOUR.** WR-01 caught it for `inline`; it was missed for the thunks.

## 2. Authoritative call-vs-detour classification (from the live Utinni source)

Only the **thunked** endpoints are at risk — the `client::*` external-linkage shims and the
`config::*` plain-`&fn` rows advertise real addresses and are detour-safe. Verdicts below are read
directly off Utinni's detour/call sites (file:line cited).

### groundScene (`UtinniCore/swg/scene/ground_scene.cpp`)

| endpoint | Utinni usage | evidence | thunk verdict |
|---|---|---|---|
| `ctor` | **CALLED** (constructs an offline scene) | `:167` | ✅ placement-new thunk OK |
| `init` | not detoured (called/unused) | absent from `detour()` | ✅ thunk OK |
| `reloadTerrain` | **CALLED** | `:465` | ✅ thunk OK |
| `changeCamera`→`setView` | **CALLED** | `:438/442/455` | ✅ thunk OK |
| `getCurrentCamera` | **CALLED** | `:431` | ✅ thunk OK |
| `update` | **DETOURED** (`hkUpdateLoop`) | `:418` | ❌ **thunk BREAKS the detour** |
| `handleInputMapEvent` | **DETOURED** (`hkHandleInputEvent`) | `:419` | ❌ **thunk BREAKS the detour** |
| `handleInputMapUpdate` | not currently detoured (remove is commented at `:426`) | — | ⚠️ thunk OK *now*; re-classify if the detour is re-enabled |

### cuiChatWindow (`UtinniCore/swg/ui/cui_chat_window.cpp`)

| endpoint | Utinni usage | evidence | thunk verdict |
|---|---|---|---|
| `writeToAllTabs` | **CALLED** | `:208` | ✅ thunk OK |
| `writeToCurrentTab` | **CALLED** | `:220` | ✅ thunk OK |
| `enableTextInput` | **DETOURED** (`hkEnableTextInput`) + also called | `:552` | ❌ **thunk BREAKS the detour** |
| `chatEnterHandler`→`performEnterKey` | **DETOURED** (`hkChatEnter`) | `:555` | ❌ **thunk BREAKS the detour** |

## 3. What to change (provider side)

**Four endpoints must be re-advertised by their REAL function entry, not a call-through forwarder:**
`groundScene::update`, `groundScene::handleInputMapEvent`, `cuiChatWindow::enableTextInput`,
`cuiChatWindow::chatEnterHandler`.

- These are **non-virtual** methods, so a real code entry exists. The `pmfToVoid` sizeof guard you
  hit is about the *inflated PMF representation* (the `this`-adjustment makes the PMF larger than a
  `void*`) — it does **not** mean the function lacks a stable entry. Advertise the address the
  **engine's own call path reaches** (the compiled method body), resolved per the MI encoding: the
  PMF's **code component** (first pointer-sized field for a non-virtual member), accounting for the
  base `this`-adjustment. For a primary-base method (offset 0) the entry is directly usable by
  Utinni's existing `__thiscall` trampoline (`this` in ECX); for a secondary-base method the
  this-adjustment must be reflected. (Codex's nuance, §4 — resolve per the actual dispatch path; do
  not blind-`reinterpret_cast` the PMF.)
- **Keep the call-through thunks** for the CALLED rows (`reloadTerrain`, `changeCamera`,
  `getCurrentCamera`, `init`, `writeToAllTabs`, `writeToCurrentTab`) — they are correct there.
- **`ctor` contrast (important):** `groundScene::ctor` is **CALLED** by Utinni, so your placement-new
  `__fastcall` ctor thunk is correct. But `cuiChatWindow::ctor` (currently DEFERRED) is **DETOURED**
  by Utinni (`hkCtor`, `cui_chat_window.cpp:546`) — when you eventually advertise it, it must be the
  **real ctor entry**, NOT a placement-new thunk (a placement-new thunk would be detour-dead exactly
  like the four above). Two ctors, opposite mechanisms, because Utinni uses them oppositely.

## 4. Codex corroboration (verbatim conclusion)

> "Yes, your claim is correct. A DetourXS inline/prologue detour patches the bytes at the address
> passed to `Detour::Create`. If that address is the provider's free-function forwarder … only calls
> to that forwarder's entrypoint hit the hook. The engine's own update path will call whatever it
> already calls: the real `GroundScene::update` body, a vtable slot target, or a compiler-generated
> adjustor thunk. … For a detoured endpoint, the provider must advertise the actual executable
> entrypoint the engine reaches … A call-through wrapper is only suitable for 'Utinni calls engine'
> endpoints, not 'intercept engine calling itself' endpoints." (+ the MI-PMF-encoding nuance above.)

## 5. Consumer-side coordination

Utinni will **not** wire bindings for the four detoured endpoints until they are re-advertised by real
entry (binding them to the current thunks would ship the silent-dead-hook). The CALLED thunked rows
can be wired now. The shims/plain-`&fn` rows are detour-safe and can be wired now. When you re-advertise
the four, bump `UTINNI_HOOKPOINTS_VERSION` (→ 3) and re-sync the `.h`/`.inc`; the name set is unchanged
(only the addresses behind four names change), so the consumer's required-set is unaffected — this is an
address-correctness fix, not a catalog change.
