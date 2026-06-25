# Provider Request — GetEngineHookPoints table is half-built when the injected consumer reads it (static-init race)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-25
**Status:** REQUEST — HIGH, and **root-cause CONFIRMED with live numbers** (§1). This is the real reason
Utinni resolves only **40 of 96** advertised names — NOT a stale staged exe.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`.

---

## 0. TL;DR

`GetEngineHookPoints()` returns a table whose **function-call-initialized rows are still null** when Utinni
reads it. Utinni reads from `utinni_init`, which runs on the injected remote thread **while the SWG exe's
main thread is still SUSPENDED** — i.e. *before* the exe's CRT static initializers (`_initterm`) run. Rows
initialized with a constant (`(void*)&Symbol`) live in `.rdata` and are valid at module load; rows
initialized with a **function call** (`utinni_groundSceneUpdateRealEntry()`, `pmfRealEntry(...)`,
`pmfToVoid(...)`, `utinni_chatWindowCreateNewWindowEntry()`, `TreeFile::enumerateFiles`…) are dynamically
initialized at static-init time and are **null until the main thread resumes**. The consumer never sees them.

Your `utinni_verifyNoNullNoDup()` is green because it runs at the exe's *static-init* time (tail already
filled) — **after** the consumer has read the table. Both are correct; they observe different instants.

**Ask:** make `GetEngineHookPoints()` return a fully-populated table **whenever it is called**, independent
of CRT static-init order — lazily complete the function-call rows on first call (§3). No contract change.

---

## 1. Confirmed — the live evidence

Utinni added a one-shot diagnostic: it re-reads the **same** advertised table from `hkInstall` (which runs
on the engine's own thread, *after* the exe is fully up), counts how many bound names have a non-null addr,
and spot-checks a few. One injection, one exe (your 22:29 v5/98 staged build):

```
[utinni_init]  endpoints: resolved 40/96 by name (56 missing)
[hkInstall]    endpoints DIAG: re-read post-init -> 96/96 resolvable NOW
               (enumerateFiles=OK, cuiManager::setSize=OK, groundScene::update=OK, object::getObjectType=OK)
```

**Same table, same process: 40 → 96** between `utinni_init` and `hkInstall`. The rows are simply null at the
earlier read and filled by the later one. Confirmed: this is a read-timing race, not a missing/old exe.

Corroborating detail: the 40 that resolve are *exactly* the table prefix up to `groundScene::getCurrentCamera`.
The very next row, **`groundScene::update` (engine_advertise.cpp ~line 346), is the first row with a
function-call initializer** (`utinni_groundSceneUpdateRealEntry()`); every row above it is `(void*)&Symbol`.
That boundary is the 40/56 cut, exactly as MSVC `.rdata`-prefix + dynamic-init-tail predicts.

## 2. Why the consumer can't simply "read later"

Utinni resolves **and installs detours at `utinni_init`** by design — it must patch the engine functions
*before* the main thread runs them. It can't defer resolution to post-static-init without missing the hook
window, and it must not re-resolve into slots after detours install (that clobbers the trampolines). The
diagnostic above is a pure read for *proof only* — Utinni does not (and can't safely) re-resolve live. So
the table must be **valid at the instant we read it**: earliest point, remote thread, main thread suspended.

## 3. The ask — make the table valid whenever read

Ensure every row (especially the function-call ones) is populated by the time `GetEngineHookPoints()`
returns, regardless of static-init order. **Option A is the only race-proof one** (it fills on the reader's
thread, on demand):

```cpp
const EngineHookPoints * GetEngineHookPoints()
{
    static bool s_filled = false;
    if (!s_filled)
    {
        // Assign the rows whose initializers are function calls -- the ones MSVC defers to static-init.
        // They need NO runtime state (pmfRealEntry/pmfToVoid/real-entry accessors are pure address
        // arithmetic), so they are safe to run here, on the consumer's thread, before the exe's own
        // static init. Index helpers or a name->lambda rebuild both work; the point is they run on call.
        s_engineHookPoints[IDX_groundScene_update].addr             = utinni_groundSceneUpdateRealEntry();
        s_engineHookPoints[IDX_groundScene_handleInputMapEvent].addr = utinni_groundSceneHandleInputMapEventRealEntry();
        s_engineHookPoints[IDX_cuiChatWindow_enableTextInput].addr   = pmfRealEntry(&SwgCuiChatWindow::acceptTextInput);
        s_engineHookPoints[IDX_cuiChatWindow_chatEnterHandler].addr  = pmfRealEntry(&SwgCuiChatWindow::performEnterKey);
        s_engineHookPoints[IDX_cuiChatWindow_createNewWindow].addr   = utinni_chatWindowCreateNewWindowEntry();
        // ... every remaining function-call row: cuiIo/object/objectTemplate/camera/extent pmfToVoid
        //     rows, commandParser ctors/addSubCommand, treeFile::enumerateFiles, graphics::g_frameNumber,
        //     audio/memory/report/worldSnapshot wherever a call (not &Symbol) is used.
        s_filled = true;
    }
    return &s_table;
}
```

A cleaner equivalent: keep `s_engineHookPoints[]` as **names + a producer per row** and have
`GetEngineHookPoints()` build the `{name, addr}` array on first call — then NO row depends on static-init
and the verify self-check can run inside the same lazy path. Your call on mechanism; the invariant is:
**no `addr` is the result of a function call evaluated at static-init time.**

(Options B "make them `constexpr`" and C "early ctor" don't work: MI-PMF code-component extraction isn't
`constexpr`, and cross-TU static-init order is unspecified + still races the remote thread. A is it.)

### Acceptance
A single re-inject against the same staged exe: Utinni's `utinni_init` resolver jumps **40/96 → 96/96**,
no `MISSING by name` beyond the 2 carve-outs. That one number unblocks the scene list
(`treeFile::enumerateFiles`), the chat/object/worldSnapshot/camera editors, and every dynamic-row detour at
once. (Utinni keeps the post-init DIAG line in place to read the before/after in the same log.)

## 4. No contract change
Pure population-timing fix inside `GetEngineHookPoints()`. `.inc`/`.h`, names, version (v5/98), POD layout
all unchanged — no re-sync. Just rebuild + re-stage `SwgClient_r.exe`.

## 5. Pointers
- Provider: `engine_advertise.cpp` — `s_engineHookPoints[]` (function-call rows from `groundScene::update`
  down) + `GetEngineHookPoints()`.
- Consumer read site: Utinni `UtinniCore/swg/endpoints_bindings.cpp` `resolveFromExe()` (in `utinni_init`,
  pre-resume); the proof diagnostic is `countResolvableNow()` called from `hkInstall`.
- Companion still open: `2026-06-24-utinni-treefile-enum-and-inworld-reflow.md` (Ask A `enumerateFiles` and
  Ask B reflow both LANDED in the v5 build — they just can't be *seen* by the consumer until this fix).
