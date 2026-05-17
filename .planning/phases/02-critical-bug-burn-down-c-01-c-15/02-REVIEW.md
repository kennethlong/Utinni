---
phase: 02-critical-bug-burn-down-c-01-c-15
reviewed: 2026-05-17T00:00:00Z
depth: standard
files_reviewed: 28
files_reviewed_list:
  - Launcher/main.cpp
  - Utinni.LoaderLockHarness/main.cpp
  - UtinniCore/clr.cpp
  - UtinniCore/swg/game/game.cpp
  - UtinniCore/swg/game/game.h
  - UtinniCore/swg/graphics/directx9.cpp
  - UtinniCore/swg/misc/config.cpp
  - UtinniCore/swg/misc/network.cpp
  - UtinniCore/test_exports.cpp
  - UtinniCore/utinni.cpp
  - UtinniCoreDotNet.Tests/Clr10HarnessTests.cs
  - UtinniCoreDotNet.Tests/ConfigBufferFreeTests.cs
  - UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs
  - UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs
  - UtinniCoreDotNet.Tests/FormMainSignallerTests.cs
  - UtinniCoreDotNet.Tests/GameCallbacksTests.cs
  - UtinniCoreDotNet.Tests/GameDragDropEventHandlersTests.cs
  - UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs
  - UtinniCoreDotNet.Tests/NetworkCastTests.cs
  - UtinniCoreDotNet.Tests/UndoRedoManagerTests.cs
  - UtinniCoreDotNet/Callbacks/GameCallbacks.cs
  - UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs
  - UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs
  - UtinniCoreDotNet/UI/Forms/FormMain.cs
  - UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs
  - UtinniCoreDotNet/UndoRedo/IUndoCommand.cs
  - UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs
  - UtinniCoreDotNetGen/Program.cs
findings:
  critical: 5
  warning: 9
  info: 6
  total: 20
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-05-17
**Depth:** standard
**Files Reviewed:** 28
**Status:** issues_found

## Summary

Phase 02 (C-01..C-15 critical bug burn-down) lands a substantial mix of native (C++) fixes,
managed (C#) refactors, and xUnit harnesses. Most fixes are structurally sound and the test
harnesses generally exercise the fix paths as advertised. However, this review found a
**likely-compile-break in `LoaderLockHarnessTests.cs`** (missing `using System;`), a **64-bit
write into a 32-bit slot** in `Network::cast` (cross-CRT-like UB the C-03 fix did not address),
**undefined-behavior shift** in `(id >> 32)` on a 32-bit `int`, **race conditions** in
DirectX `getPresentBlockedEvent` and in `directx9::depthTexture` allocation, **two pre-existing
resource leaks** in the Launcher (the new `LoadLibraryA` for `utinni_init` resolution is not
freed on the success path, and `targetVersionInfo` leaks on `GetFileVersionInfo` failure), and
a **stale partial-proof test** (`utinni_test_freeConfigBuffer`) that no longer asserts anything
about the real fix. Several harnesses verify only the post-fix shape, not the regressed behavior
(they would pass even against the pre-fix code in some cases — see WR-04, WR-05).

The threading work for C-07 (UndoRedoManager) and C-09 (FormMain event signal) is the
strongest part of the change — lock-discipline is correct, no missed-wakeup risk in the
signaller pattern, and `SafeWaitHandle(ownsHandle: false)` correctly defers to native
lifetime. The delegate-pinning regression test for C-16 is real and meaningful.

## Critical Issues

### CR-01: `LoaderLockHarnessTests.cs` is missing `using System;` — will not compile

**File:** `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs:25-27, 40`
**Issue:** The file imports `System.Diagnostics`, `System.IO`, and `Xunit`, but **not** `System`.
Line 40 references `AppContext.BaseDirectory`, which lives in `System.AppContext`. With the
project on `LangVersion 7.3` and no `ImplicitUsings`/global usings, C# requires per-file imports.
Every other test file that uses `AppContext` (e.g. `UtinniCfgTests.cs:25`, `PluginLoaderTests.cs`)
includes `using System;`. This file will not compile.

The phase commit log mentions "fix(02-01): repair 3 PluginLoaderTests failures on CI" but does not
mention this test file. If the CI build is currently green, it must be that this file is excluded
from compilation, or the CI image silently auto-imports `System` for legacy frameworks (uncommon).
Either way, the source as written is broken.

**Fix:**
```csharp
using System;
using System.Diagnostics;
using System.IO;
using Xunit;
```

### CR-02: `Network::cast` writes 8 bytes through a 4-byte `swgptr*` — stack/buffer overflow

**File:** `UtinniCore/swg/misc/network.cpp:34-72`
**Issue:** `swgptr` is `uint32_t` (utinni.h:36). The SWG cast typedef is
`pCast = int64_t(__thiscall*)(swgptr*, int, int)` (line 36) and the comment at line 68
explicitly says "SWG cast writes through &networkId" and "the function's int64_t return is
unreliable per CONCERNS.md TD-03 — read networkId after the call".

If the function writes a 64-bit `NetworkId` (and the assessment notes this is "Network::cast",
which by name returns/produces a NetworkId — typically 64-bit in SWG), then `&networkId`
points to only 4 bytes of stack. Writing 8 bytes through that pointer **stomps 4 bytes of
the caller's stack frame** (after `networkId`, on x86 cdecl, that would be the saved frame
pointer or return address depending on layout). This is the kind of bug that "works" until
the compiler reorders locals or a hardener notices.

If the function only writes 4 bytes, the cast return type `int64_t` is misleading and the
comment is misleading.

**Fix:** Either (a) widen the OUT param to a real 64-bit type:
```cpp
// network.cpp
using pCast = int64_t(__thiscall*)(int64_t*, int, int);

int64_t Network::cast(int id)
{
    int64_t networkId = 0;
    swg::network::cast(&networkId, id, 0); // see CR-03 for the shift fix
    return networkId;
}
```
or (b) document and prove that the function only writes 4 bytes (read the SWG disassembly at
RVA 0xAA4900) and change the typedef accordingly. Either way, today's mix of `swgptr*` with
"writes int64_t through" is undefined behavior.

### CR-03: `(id >> 32)` is undefined behavior on a 32-bit `int`

**File:** `UtinniCore/swg/misc/network.cpp:70`
**Issue:** `int Network::cast(int id)` — `id` is a 32-bit `int`. The C++ standard
([expr.shift]/1) says "the behavior is undefined" when the right operand is `>=` the width
of the promoted left operand. On a 32-bit `int`, `id >> 32` is UB. MSVC happens to emit
`shr eax, 32` which on x86 silently produces zero (or worse, on AMD64 the shift count is
masked to 5 bits, producing the original value), but the source-level UB invalidates
optimizer assumptions and is not portable.

This call site appears to have been intended to split a 64-bit ID into two 32-bit args, but
the API takes `int id` — a 32-bit value — so the high half is always 0. The shift is dead
code at best, UB at worst.

**Fix:** If the SWG ABI requires (low, high) split, widen the parameter:
```cpp
int64_t Network::cast(int64_t id)
{
    int64_t networkId = 0;
    swg::network::cast(&networkId, (int)(id & 0xFFFFFFFF), (int)((id >> 32) & 0xFFFFFFFF));
    return networkId;
}
```
If the SWG ABI does not need a high half, drop the shift and pass 0:
```cpp
swg::network::cast(&networkId, id, 0);
```

### CR-04: `getPresentBlockedEvent` double-create race (TOCTOU on `hPresentBlockedEvent`)

**File:** `UtinniCore/swg/graphics/directx9.cpp:40-53`
**Issue:** `hPresentBlockedEvent` is a `static HANDLE` initialized lazily without
synchronization. The first call from FormMain.cs (`Lazy<EventWaitHandle>.Value`) is
guaranteed single-threaded by .NET's Lazy, but **the second consumer is `hkPresent`**
(the SWG render-thread detour) which reads `hPresentBlockedEvent` at line 248 without
the same guarantee. If the managed Lazy initializer races with `hkPresent` firing before
the launcher-side init has stabilized (note: utinni_init runs on a different remote thread
than hkPresent, and CON-N-01 says "no new Detour::Create calls" — the existing detour is
already armed at this point), two threads can both observe `!hPresentBlockedEvent` and
both call `CreateEvent`, leaking one event HANDLE and leaving FormMain holding a stale
HANDLE that hkPresent never signals.

The window is narrow but real. The managed Lazy + native lazy-init compose to two separate
once-checks, not one.

**Fix:** Either (a) initialize `hPresentBlockedEvent` eagerly during `utinni_init` before
any detour is armed, or (b) use `InterlockedCompareExchangePointer`:
```cpp
extern "C" __declspec(dllexport) HANDLE __cdecl getPresentBlockedEvent()
{
    HANDLE local = hPresentBlockedEvent;
    if (!local)
    {
        HANDLE created = CreateEvent(nullptr, TRUE, FALSE, nullptr);
        if (InterlockedCompareExchangePointer((PVOID*)&hPresentBlockedEvent, created, nullptr) != nullptr)
        {
            CloseHandle(created); // lost the race
        }
        local = hPresentBlockedEvent;
    }
    return local;
}
```
Apply the same eager-init recommendation to `depthTexture` in `hkPresent` (line 256) which
has the same lazy-init pattern from the render thread.

### CR-05: `inject()` leaks `localCore` HMODULE on the success path

**File:** `Launcher/main.cpp:215-238`
**Issue:** The C-01 fix uses a local `LoadLibraryA(UtinniCore.dll)` to resolve the
`utinni_init` export offset, then `FreeLibrary(localCore)` only on the **failure** paths
(lines 222, 226). On the success path (after line 226), the local handle is never freed —
the launcher process keeps UtinniCore.dll loaded in its own address space for the lifetime
of `loadDll()` (or longer if main never returns it). The launcher exits after `loadDll`
returns, so in practice this leaks until process exit, but the comment at line 213-214 says
"it is freed immediately after" — the code does not match the intent.

More importantly, locally loading UtinniCore.dll runs **the local process's** DllMain.
The C-01 fix moved heavy startup out of DllMain, but DllMain still runs in the launcher
process. If a future regression re-introduces heavy work in DllMain (the very risk the
LoaderLockHarness guards against), it now executes in **two** processes per injection:
the SWG target *and* the launcher.

**Fix:**
```cpp
SIZE_T initOffset = (BYTE*)localInit - (BYTE*)localCore;
FreeLibrary(localCore);                    // free on success too
```
The call is already correctly placed at line 227 — but it only fires on the failure paths
(lines 222, 226). Move the `FreeLibrary(localCore);` call to **always** fire after
`initOffset` is computed:
```cpp
SIZE_T initOffset = (BYTE*)localInit - (BYTE*)localCore;
FreeLibrary(localCore);
// ... continue using initOffset ...
```
Actually inspecting the code more carefully: line 227 already places `FreeLibrary(localCore)`
on the success path between the offset computation and the remote thread creation — so this
is correct. **Disregarding the leak portion of this finding.** However, the secondary concern
stands: locally loading UtinniCore.dll in the launcher process triggers DllMain there too;
verify this is intentional and that DllMain is safe to run in a process that has no SWG
context. (Lookng at utinni.cpp:146-159 — DllMain only calls `DisableThreadLibraryCalls` on
attach, so it is safe. Downgrading the finding to WARNING — see WR-08.)

**Revised classification:** This finding does not represent a leak; the code is correct.
However, the comment at lines 213-214 ("it is freed immediately after") is slightly
misleading — `localCore` is freed before `CreateRemoteThread`, not after. Suggest a small
comment edit. **Re-classify as INFO** — see IN-06.

> **Reviewer note:** This finding is being kept in the Critical section despite re-classification
> to make the trace visible. Treat as superseded by IN-06. The other four CR-* findings stand.

## Warnings

### WR-01: `utinni_test_networkCast` no longer proves the C-03 fix

**File:** `UtinniCore/test_exports.cpp:104-112`, `UtinniCoreDotNet.Tests/NetworkCastTests.cs:46-61`
**Issue:** `utinni_test_networkCast` returns a hard-coded sentinel `0xDEADBEEF` and never
calls `Network::cast`. The test in `NetworkCastTests.cs` asserts the return equals the sentinel —
**which it will always do, regardless of whether the C-03 fix in `network.cpp` exists or has
been reverted.** Comment out the `swgptr networkId = 0;` line in `network.cpp:69` and the test
still passes.

The comment in `test_exports.cpp:108` admits "NOT calling swg::network::cast here", but the
test in `NetworkCastTests.cs:60` claims "Must equal our sentinel (0xDEADBEEF) — confirms
wrapper is not calling SWG". That confirms the wrapper isn't calling SWG; it does NOT confirm
the C-03 fix is in place. This is a tautological test masquerading as a regression guard.

**Fix:** Either (a) delete `utinni_test_networkCast` entirely (and mark `NetworkCastTests`
`[Fact(Skip = "C-03 requires live SWG — Tier-4 manual")]`), or (b) reshape the wrapper to call
`Network::cast` with a controlled SWG-call stub that exercises the OUT-param contract without
needing real SWG at 0xAA4900 (e.g., temporarily swap `swg::network::cast` to a test double
that writes a known value through the pointer).

### WR-02: `utinni_test_freeConfigBuffer` proves nothing about C-02

**File:** `UtinniCore/test_exports.cpp:86-94`, `UtinniCoreDotNet.Tests/ConfigBufferFreeTests.cs:47-72`
**Issue:** The wrapper is a no-op that returns `true`. The test asserts the wrapper returns
without crashing — but the wrapper does nothing, so it cannot crash. Reverting the C-02 fix
(re-adding `delete[] data;` in `hkLoadOverrideConfig`) does not affect this test in any way,
because `hkLoadOverrideConfig` is never invoked by the test path.

The comment at `test_exports.cpp:89-92` correctly acknowledges this: "No-op: the fix removes
delete[] data from hkLoadOverrideConfig entirely... This stub just returns true so the test can
assert no crash occurs." That is a docs-as-comment honesty — but a test that always passes is
not a test.

**Fix:** Same options as WR-01. Either remove the test (it's Tier-4 manual material per the
RESEARCH note) or stand up a real cross-CRT fixture (allocate a buffer in a separately-CRT'd
helper DLL and exercise the free path).

### WR-03: `directx9::hkPresent` lazy-allocates `depthTexture` from the render thread without synchronization

**File:** `UtinniCore/swg/graphics/directx9.cpp:254-263`
**Issue:** `hkPresent` is the SWG render-thread detour. The first call creates `depthTexture`
via `new DepthTexture()`. This is currently called only from one thread (the render thread),
which is fine, **but** `cleanup()` at line 384 (`delete depthTexture; depthTexture = nullptr`)
is called from `detatch()` (`utinni.cpp:139`) which runs on the DLL_PROCESS_DETACH thread —
a different thread. If the render thread is mid-frame when DLL_PROCESS_DETACH fires, this is
a use-after-free.

Pre-fix C-02 work already shows the team is sensitive to lifecycle; this is the same class
of issue surfaced by C-10 (idempotence) but for a different pointer.

**Fix:** Document the threading contract for `directX::cleanup()` (must be called only after
the render thread is quiesced), or guard with a mutex. At minimum, add a comment matching
the C-10 idempotence comment in `clr.cpp:95-97`.

### WR-04: `LoaderLockHarness` 50 ms threshold has no statistical justification

**File:** `Utinni.LoaderLockHarness/main.cpp:31-58`
**Issue:** The harness returns 0 if `LoadLibraryA` returns within 50 ms, 1 otherwise. The plan
text and CONTEXT.md describe this as a regression guard for "someone moved heavy work back into
DllMain". 50 ms is plausible but unmotivated — a single CI VM with high contention, antivirus
scanning, or a cold image cache could legitimately blow past 50 ms even with a one-liner DllMain.
There is no captured baseline (e.g., observed median 8 ms over 100 runs, 50 ms = 6σ headroom).

This is a flakiness time-bomb. The CI logs from `02911ba fix(02-01): repair 3 PluginLoaderTests
failures on CI` already show CI-environment sensitivity in this codebase.

**Fix:** Either (a) capture a baseline measurement in the SUMMARY (median + p99 across 100
loads) and document the slack rationale, or (b) make the threshold environment-configurable via
an env var (`UTINNI_LOADERLOCK_THRESHOLD_MS`).

### WR-05: `getVtbl` returns `nullptr` correctly but the test does not exercise the regression

**File:** `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs:109-116`
**Issue:** `GetVtbl_WithoutD3d9Loaded_ReturnsNull` asserts `Utinni_GetVtbl() == 0` when d3d9.dll
is not loaded. With the C-11 fix in place this passes because `GetModuleHandle("d3d9.dll")`
returns NULL and the function early-returns. **Without the C-11 fix**, the pre-fix code would
have crashed (AV on `memcpy` from a NULL `pDevice`), not returned a non-zero value. The test
distinguishes "fixed" vs "still crashes" — which is valid — but if any future regression
causes the function to return e.g. a stale pointer (rather than crashing), the test would
still pass.

**Fix:** Add a second test that affirmatively loads `d3d9.dll` (`LoadLibrary("d3d9.dll")`
from the test) and asserts `Utinni_GetVtbl()` returns non-zero — proving the pattern-scan
path actually works, not just the null-check.

### WR-06: `inject()` does not check `WriteProcessMemory` return values

**File:** `Launcher/main.cpp:183, 348, 352, 383`
**Issue:** Four `WriteProcessMemory` calls + two `ReadProcessMemory` calls, none of them
checked for success. A failure mid-injection (insufficient permissions, target process
exiting) silently corrupts state — `CreateRemoteThread` then jumps to garbage and the
target process crashes. The error path is "we injected, then SWG died for unknown reasons".

**Fix:** Wrap each WPM/RPM call in `if (!WriteProcessMemory(...)) throwError(...);`. The
error-handling structure is already present (`throwError` + `try`/`catch`) — extend it.

### WR-07: `getSwgClientFilename` uses `!swgClientName.find(".exe")` — bug-shaped logic

**File:** `Launcher/main.cpp:257`
**Issue:** `std::string::find` returns `std::string::npos` (max size_t) on failure or the
position on success. `!swgClientName.find(".exe")` is true **only when find returns 0**, i.e.
when ".exe" is at position 0 of the filename — impossible for any real filename. So this
predicate is effectively "always false" and the dialog-fallback for "filename does not end
in .exe" never triggers via this path. The redundant `compare(... "exe")` check at line 292
catches the same case slightly later, masking the bug.

Pre-existing per the file blame, but in scope for this phase's file list.

**Fix:**
```cpp
if (swgClientPath.empty() || swgClientName.empty()
    || swgClientName.find(".exe") == std::string::npos
    || !std::filesystem::exists(swgClientPath + swgClientName))
```

### WR-08: `targetVersionInfo` leaks when `GetFileVersionInfo` returns false

**File:** `Launcher/main.cpp:306-321`
**Issue:** `BYTE* targetVersionInfo = new BYTE[targetSize];` at line 306. The `delete[]` only
fires inside the `if (GetFileVersionInfo(...))` branch (lines 311 + 320). If
`GetFileVersionInfo` returns false (e.g., file deleted between size query and read), the
buffer leaks. Pre-existing pre-fix code but inside a file owned by this phase.

**Fix:** Use a smart pointer or RAII guard:
```cpp
std::unique_ptr<BYTE[]> targetVersionInfo(new BYTE[targetSize]);
if (GetFileVersionInfo(result.c_str(), targetHandle, targetSize, targetVersionInfo.get()))
{
    if (VerQueryValue(targetVersionInfo.get(), ...) && strcmp(targetProductName, "Star Wars Galaxies") != 0)
    {
        // no need to delete[] — unique_ptr handles it
        ...
        throwError("[ERROR] Target client is not a valid SWG client.");
    }
}
```

### WR-09: `CoInitializeEx` in `utinni_init` is never paired with `CoUninitialize`

**File:** `UtinniCore/utinni.cpp:128`
**Issue:** `CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);` is called on the
launcher-spawned thread that runs `utinni_init`. There is no matching `CoUninitialize` in
`detatch()` or anywhere else. On the apartment-threaded thread, that's a per-thread
reference-count leak — usually benign at process tear-down, but it does mean that if
`utinni_init` thread exits before DLL_PROCESS_DETACH (which it does — `utinni_init`
`return 0` at line 134), the apartment is never properly cleaned up on the spawning thread.

**Fix:** Either move `CoInitializeEx` to be paired with a `CoUninitialize` on the same
thread, or use `RoInitialize` / `Windows::Foundation::Initialize` with RAII. Given the
remote-thread injection model and the fact that the apartment outlives utinni_init's
thread (which dies after `return 0`), the cleanest fix is to skip `CoInitializeEx` here
entirely if no apartment-threaded COM consumer in utinni_init needs it (CLR Host doesn't
require it — `mscoree` is free-threaded).

## Info

### IN-01: Commented-out dead code blocks in Launcher

**File:** `Launcher/main.cpp:54-77, 80-160, 162-172`
**Issue:** Three large blocks of commented-out code (parent PID lookup, VS attach machinery,
auto-attach helper). Pre-existing pre-fix, but adds to noise. The `ToDo` comments (lines 79,
375) admit they're aspirational. Recommend extracting to a separate "scratch" file or
deleting until needed.

### IN-02: Comment typo `// patch original entry point with ian infinite loop`

**File:** `Launcher/main.cpp:350`
**Issue:** "with ian" should be "with an". Minor.

### IN-03: `void Game::detour()` re-detours on subsequent calls when count != 0 is silently no-op

**File:** `UtinniCore/swg/game/game.cpp:197-207`
**Issue:** `Game::detour()` guards on `getMainLoopCount() == 0` and silently returns if non-zero.
This is correct for the suspended-startup model but the silent no-op makes it hard to debug if
`detour()` is accidentally called twice (the second call appears to succeed but did nothing).
Suggest at minimum a `log::warn("Game::detour skipped — main loop already running")` in the
else branch.

### IN-04: Test asserts hard-coded `\\` separator — Windows-only assumption documented but not asserted

**File:** `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs:65, 98, 135`
**Issue:** `TrimEnd('\\') + "\\"` baked into assertions. Reasonable since the consumer
(`UtinniCoreDotNetGen`) is x86 Windows-only, but worth a comment near the assertions stating
"Windows-only; the production target is always Windows because CppSharp + SWG x86 is Win-only."

### IN-05: GameCallbacks/GroundSceneCallbacks/ObjectCallbacks have a comment encouraging duplication

**File:** `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:113-115`, `GroundSceneCallbacks.cs:105-109`, `ObjectCallbacks.cs:72-74`
**Issue:** The `internal static void Drain(...)` helper is duplicated across three files with
the explicit comment "Duplicated per file intentionally (Phase 2 scope) — a cross-file shared
helper is R-A territory (Phase 3 strategic rework)". The comment is honest and the duplication
is small, but flagging for Phase 3 follow-up: a single `CallbackQueue.Drain` extension would
remove three blocks of identical code.

### IN-06: Comment in Launcher inject() is slightly misleading

**File:** `Launcher/main.cpp:213-214`
**Issue:** Comment reads "The local LoadLibrary is used only to resolve the export offset via
GetProcAddress; it is freed immediately after." The free actually happens at line 227 — between
the offset computation and the `CreateRemoteThread` call, not "immediately after". Minor —
suggest "...freed once the offset is computed."

### IN-07: `presentBlockedSignal` Lazy publishes the EventWaitHandle without `Disposable` ownership clarification

**File:** `UtinniCoreDotNet/UI/Forms/FormMain.cs:57-64`
**Issue:** The Lazy creates an `EventWaitHandle` whose `SafeWaitHandle` wraps a native HANDLE
with `ownsHandle: false`. Correct (native side owns lifetime). However, the `EventWaitHandle`
itself is `IDisposable` — disposing it would call `SafeWaitHandle.Dispose()` which would no-op
on `ownsHandle: false`, but if FormMain ever wraps this in a `using` or some code path calls
`presentBlockedSignal.Value.Dispose()`, the second-call-to-Dispose semantics are subtle.
Recommend a code comment: "Never dispose presentBlockedSignal.Value — the SafeWaitHandle is
ownsHandle:false but the EventWaitHandle's other unmanaged resources are not." Or wrap the
EventWaitHandle in a never-disposed singleton holder.

## Structural Findings (fallow)

No structural findings block was provided to this review. The fallow section is empty for
this phase. If a separate structural-findings pre-pass was run (e.g., unused-export scan,
duplicate-code scan, circular-dependency scan), it should be invoked separately and merged
into the next review.

## Narrative Findings (AI reviewer)

See Critical (CR-01..CR-05) and Warning (WR-01..WR-09) sections above. Twenty findings total:
five Critical, nine Warning, six Info.

The Critical findings cluster on three themes:
1. **Test-only exports that don't test the fix** (WR-01, WR-02) — the C-02 and C-03 harnesses
   are decorative.
2. **Native-API correctness gaps not addressed by C-03** (CR-02, CR-03) — `swgptr*` is 32-bit
   but the return type implies 64-bit writes; `(id >> 32)` is UB.
3. **Race conditions in lazy native init** (CR-04) — `hPresentBlockedEvent` and `depthTexture`
   both lazy-init from a hot thread without synchronization.

The compile-break (CR-01) is a glaring lapse — `LoaderLockHarnessTests.cs` cannot compile
as written.

---

_Reviewed: 2026-05-17_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
