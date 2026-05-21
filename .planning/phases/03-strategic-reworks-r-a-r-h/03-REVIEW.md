---
phase: 03
reviewed: 2026-05-21T00:00:00Z
depth: standard
reviewer: gsd-code-reviewer
files_reviewed: 60
files_reviewed_list:
  - UtinniCore/swg/game/game.cpp
  - UtinniCore/swg/game/game.h
  - UtinniCore/swg/scene/ground_scene.cpp
  - UtinniCore/swg/scene/ground_scene.h
  - UtinniCore/swg/object/creature_object.cpp
  - UtinniCore/swg/object/creature_object.h
  - UtinniCore/swg/graphics/post_processing.cpp
  - UtinniCore/swg/graphics/post_processing.h
  - UtinniCore/swg/graphics/depth_texture.cpp
  - UtinniCore/swg/graphics/depth_texture.h
  - UtinniCore/swg/graphics/shader.cpp
  - UtinniCore/swg/graphics/shader.h
  - UtinniCore/swg/graphics/graphics.cpp
  - UtinniCore/swg/graphics/graphics.h
  - UtinniCore/swg/ui/imgui_impl.cpp
  - UtinniCore/swg/ui/imgui_impl.h
  - UtinniCore/swg/ui/cui_chat_window.cpp
  - UtinniCore/swg/ui/cui_chat_window.h
  - UtinniCore/swg/ui/cui_manager.cpp
  - UtinniCore/swg/ui/cui_manager.h
  - UtinniCore/utility/log.cpp
  - UtinniCore/utility/log.h
  - UtinniCore/test_exports.cpp
  - UtinniCore/plugin_framework/plugin_manager.cpp
  - UtinniCore/plugin_framework/utinni_plugin.h
  - UtinniCore/swg/client/client.cpp
  - UtinniCore/swg/client/client.h
  - Utinni.CrtMatchPlugin/main.cpp
  - Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj
  - Utinni.LegacyPlugin/main.cpp
  - Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj
  - Utinni.sln
  - UtinniCoreDotNet/Callbacks/CallbackHelpers.cs
  - UtinniCoreDotNet/Callbacks/GameCallbacks.cs
  - UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs
  - UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs
  - UtinniCoreDotNet/Callbacks/CuiCallbacks.cs
  - UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs
  - UtinniCoreDotNet/UI/Controls/PanelGame.cs
  - UtinniCoreDotNet/Utility/Native.cs
  - UtinniCoreDotNet/Utility/Log.cs
  - UtinniCoreDotNetGen/HeaderDiscovery.cs
  - UtinniCoreDotNetGen/Program.cs
  - UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj
  - sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs
  - UtinniCoreDotNet.Tests/CallbackHelpersTests.cs
  - UtinniCoreDotNet.Tests/CallbacksSubscribeUnsubscribeTests.cs
  - UtinniCoreDotNet.Tests/CallbacksSnapshotIterationTests.cs
  - UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs
  - UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs
  - UtinniCoreDotNet.Tests/GetSwgWndProcTests.cs
  - UtinniCoreDotNet.Tests/LogCallerMemberNameTests.cs
  - UtinniCoreDotNet.Tests/HeaderDiscoveryTests.cs
  - UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs
  - UtinniCoreDotNet.Tests/ExportResolutionTests.cs
  - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj
severity_counts:
  critical: 0
  warning: 0
  info: 6
findings:
  critical: 2
  warning: 7
  info: 6
  total: 15
status: issues_found
fix_dispositions_updated: 2026-05-21T18:00:00Z
fix_dispositions_summary:
  CR-01: fixed (commit 427f474 -- per-registry std::mutex across 11 native files + outputSinkMutex)
  CR-02: fixed (commit bc2b4ad -- reject plugins missing destroyPlugin at load time)
  WR-01: fixed (commit 9626174 -- atomic<int> s_ioEventLogCount + _ReturnAddress doc)
  WR-02: fixed (commit cb6fad3 -- doc-only shutdown-lifecycle contract in utinni_plugin.h)
  WR-03: fixed (commit 9248a1a -- moved test-only Game accessors to game_test_internal.h)
  WR-04: fixed (commit c1681bd -- skip-zero handle overflow guard, native + managed)
  WR-05: fixed (commit e17d123 -- atomic<bool> s_chatInputActive)
  WR-06: fixed (commit f72721d -- atomic<swgptr> pCuiChatWindow + pCuiConsoleHelper)
  WR-07: fixed (commit 427f474 -- outputSinkMutex added as part of CR-01)
  IN-01: deferred (info-tier; trivial doc count update, leave for next phase pass)
  IN-02: deferred (info-tier; vcxproj comment update)
  IN-03: deferred (info-tier; pre-existing SEH/EHs documentation)
  IN-04: deferred (info-tier; CONVENTIONS.md ToDo tag)
  IN-05: deferred (info-tier; DIAG log volume; tied to Issue #11 lifecycle)
  IN-06: deferred (info-tier; no code change required per finding text)
---

# Phase 3: Code Review Report

**Reviewed:** 2026-05-21
**Depth:** standard
**Files Reviewed:** 60
**Status:** issues_found

## Summary

Phase 3 lands the R-A..R-H strategic reworks with disciplined attention to the
documented invariants — handle-based Subscribe/Unsubscribe, two-phase plugin
init, single-source RVA, `[CallerMemberName]` logging, header auto-discovery,
and idempotent VSIX wizard. Snapshot-iteration is uniformly applied at every
managed dispatch site under a per-callback lock; the native dispatch sites
also build local-copy vectors before iterating (R-H). Macro-extension
discipline and the `test_internal::TestImpl` pattern preserve CON-N-08
byte-identity of `plugin_manager.h`. Test surface is substantial (28 new
managed Facts, end-to-end fixture DLLs, P/Invoke harnesses) and exercises
each R-letter contract.

That said, the native callback layer ships a **process-wide race condition**:
all 32 native `std::unordered_map<int, fn_ptr>` registries mutate and read
without any synchronization, so the same Subscribe-during-dispatch scenario
the managed side carefully snapshots-under-lock for is undefined behavior on
the native side. The R-B HMODULE cleanup also has a sequencing trap that
will crash if `destroyPlugin` is implemented in the plugin DLL (the
overwhelmingly likely case post-flag-day): `~PluginManager` calls
`destroyFn` THEN `FreeLibrary` on the same module, but `destroyFn` lives in
that module — at refcount=1 with the host being the sole holder, `FreeLibrary`
unloads the DLL right after we invoked code from it; this is fine ONLY if
the plugin doesn't itself unload during destroy. More urgently, the
`Utinni.LegacyPlugin` fixture deliberately allocates a `LegacyPlugin` with
/MT-`new` and the host then `delete`s it with /MD-`delete` — that's the
crash class CON-B-04 the symmetric ABI was supposed to avoid, and the
fixture's whole purpose is to "prove no crash" yet it exercises the very
mismatch path on every test run. Two issues are Critical; the rest are
mostly correctness hardenings and code-quality cleanups.

## Critical Issues

### CR-01: Native callback registries have no thread-safety; concurrent Subscribe vs. dispatch races

**Disposition:** `fixed: 427f474` -- per-registry std::mutex added across all 11 native callback files (32 registries). Subscribe / Unsubscribe writes AND snapshot-build reads in every dispatch site now take the mutex; iteration runs outside the lock so callbacks can re-subscribe without deadlock. Includes regression test `NativeCallbacksHandleTests.ConcurrentSubscribeAndDispatch_DoesNotCrash` (7 mutator threads x 1000 paired Subscribe+Unsubscribe iterations vs 1 dispatcher thread x 250 DispatchInstall iterations).

**Files:**
- `UtinniCore/swg/game/game.cpp:76-85, 95-173, 209-384, 504-525`
- `UtinniCore/swg/scene/ground_scene.cpp:74-81, 117-179, 191-247, 315-327`
- `UtinniCore/swg/object/creature_object.cpp:41-42, 47-85`
- `UtinniCore/swg/graphics/post_processing.cpp:41-44, 49-113`
- `UtinniCore/swg/graphics/depth_texture.cpp:45-46, 215-285`
- `UtinniCore/swg/graphics/shader.cpp:39-40, 45-119`
- `UtinniCore/swg/graphics/graphics.cpp:88-113, 120-329, 418-528`
- `UtinniCore/swg/ui/imgui_impl.cpp:61-62, 365-378, 425-444, 454-462, 478-616`
- `UtinniCore/swg/ui/cui_chat_window.cpp:69-72, 84-99, 340-351`
- `UtinniCore/swg/ui/cui_manager.cpp:69-71, 135-189`
- `UtinniCore/utility/log.cpp:33-35, 56-67, 118-138`

**Issue:** Every native `std::unordered_map<int, fn_ptr>` registry introduced
by R-A is mutated without synchronization. `subscribe*Callback` writes to the
map (`registry[id] = func`, increments a non-atomic `s_next*Id`), `unsubscribe*`
calls `.erase(handle)`, and the dispatch sites read the entire map into a
local `std::vector` snapshot. Standard library `std::unordered_map` is NOT
thread-safe; concurrent insert/erase racing with iteration is undefined
behavior (can crash, hang in rehash, return garbage values).

The managed side carefully takes a `lock (xxxLock)` for every Subscribe,
Unsubscribe, and snapshot-copy — the explicit comment at
`GameCallbacks.cs:280-283` even says so. The native side ships no equivalent.
The R-H snapshot pattern (D-12) says native uses `std::vector<fn_ptr>(...)`
copy — but a copy from an unsynchronized map racing with `emplace` is the
exact same race as iterating it.

Whether this fires in practice depends on whether any callback registration
or callback firing crosses threads. SWG's main loop runs on the render
thread; the managed UI WndProc and the plugin loader's `init()` calls run
on the WinForms UI thread; spdlog's `OutputSink::sink_it_` can be invoked
from any thread that calls into `log::info` (incl. background detour
threads). In particular `log::info` is called from
`hkPresent`/`hkBeginScene`/`hkEndScene` (render thread) AND from
`PanelGame.ReparentSwgWindow` (UI thread, via managed `Log.Info`) —
two different threads write/read `outputSinkCallbacks` concurrently.

`PATTERNS.md` decisions document (line 280) acknowledges the native side
has "no lock today" — but the dispatch sites STILL iterate, and the new
handle-based API explicitly adds Subscribe/Unsubscribe paths intended to be
called from plugin code (which may run on the WinForms thread). Phase 02 D-12
chose to snapshot to avoid iterator invalidation; on a multi-threaded
producer/consumer this is necessary but not sufficient — the iteration that
BUILDS the snapshot races with concurrent writes.

**Fix:** Add a per-class `std::mutex` (or one `std::shared_mutex` per
registry) and bracket every `subscribe*` write, `unsubscribe*` erase, AND
the snapshot-build read with it. Mirror the managed pattern: lock during
copy, iterate the local vector outside the lock. Example for
`game.cpp`:
```cpp
static std::mutex installCallbacksMutex;

int Game::subscribeInstallCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    int id = s_nextInstallId++;
    installCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeInstallCallback(int handle)
{
    if (handle == 0) return false;
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    return installCallbacks.erase(handle) > 0;
}

// In hkInstall:
std::vector<void(*)()> snapshot;
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    snapshot.reserve(installCallbacks.size());
    for (const auto& kv : installCallbacks) snapshot.push_back(kv.second);
}
for (const auto& func : snapshot) func();
```
Same transform across all 32 registries. As a tactical alternative for the
hot-path graphics-callback case where the lock would show up in BeginScene/
EndScene, switch to `std::vector<void(*)()>` for the snapshot storage
(non-map) and protect with an atomic-swap-of-shared-ptr-vector pattern;
that's overkill for non-hot-path registries, but `log::*`,
`graphics::beginScene/endScene/present` are hot enough to warrant attention.

---

### CR-02: `Utinni.LegacyPlugin` fixture exercises the very cross-CRT-delete crash class R-B was meant to fix; the "no-crash" test is a happy-path tautology

**Disposition:** `fixed: bc2b4ad` -- chose direction (a) refuse-to-load. PluginManager::loadPlugins (and test_internal::test_loadFromDirectory) now reject plugins missing destroyPlugin at load time, log error, leak the createPlugin'd allocation rather than risk cross-CRT delete. Test fixture rewritten to assert rejection (`LegacyPlugin_NoDestroyPlugin_RejectedAtLoadTime`) and continued symmetric-plugin load (`LegacyPlugin_AlongsideCrtMatch_LegacyRejected_CrtMatchStillLoads`). ABI sanity static_assert on the legacy struct size added in LegacyPlugin/main.cpp.

**Files:**
- `Utinni.LegacyPlugin/main.cpp:73-101`
- `Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj:65-86` (`/MT` RuntimeLibrary)
- `UtinniCore/plugin_framework/plugin_manager.cpp:82-88` (`delete loaded.plugin` fallback)
- `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs:430-459`
  (`LegacyPlugin_NoDestroyPlugin_FallbackToVirtualDestructor_NoCrash`)

**Issue:** `LegacyPlugin` is intentionally built `/MT` (its own statically
linked CRT) and exports only `createPlugin`, which `new`s a `LegacyPlugin`
through the LegacyPlugin DLL's CRT heap (line 100: `return new LegacyPlugin();`).
The fallback path in `~PluginManager` then calls `delete loaded.plugin`
(plugin_manager.cpp:87) — that `delete` runs in UtinniCore.dll's `/MD`
CRT and frees memory that was allocated in LegacyPlugin's `/MT` CRT. This
is **exactly** the CON-B-04 cross-CRT heap-mismatch class that R-B's
symmetric `destroyPlugin` ABI was designed to eliminate.

The legacy fallback was meant for plugins that PRE-DATE the symmetric ABI
(specifically CON-O-07 Sytner). In practice those would typically have been
built with `/MD` matching the host, in which case the cross-CRT delete still
works because they share the heap. The test fixture, in contrast, **forces
the mismatch** (`/MT` per the vcxproj annotation: "/MT MISMATCHES UtinniCore.dll's
/MD CRT") and then asserts "no crash" as proof the fix works. That's
backwards: on most x86 MSVC runtimes the cross-CRT delete may not crash
deterministically (the /MD heap free of a /MT-allocated block often succeeds
silently and corrupts both heaps quietly until something else triggers an
allocation fault much later). The test as written cannot fail — `delete` on
a /MT-allocated block via /MD's `delete` operator may "no-crash" while still
corrupting the heap.

**Layout-compatible class trick (lines 50-71)**: Because LegacyPlugin
declares its OWN `utinni_legacy::UtinniPlugin` struct rather than including
`UtinniCore`'s header, it dodges the `UTINNI_PLUGIN` macro break — but the
vtable layouts must match byte-exactly for the host's virtual destructor
call to dispatch correctly. There is no compile-time assertion that the
host's `utinni::UtinniPlugin` vtable matches; if the host adds a new
virtual method (e.g. `virtual void shutdown()`) the layout drifts silently
and the legacy fixture's virtual-destructor dispatch slot moves. Documented
as "ABI is identical (layout-compatible) so the reinterpret happens implicitly"
(line 95-97) but that documentation is the only thing keeping it correct.

**Fix:**
1. **Either** kill the `/MT` mismatch in `Utinni.LegacyPlugin.vcxproj` —
   build the legacy fixture with `/MD` (matching UtinniCore.dll). Document
   that the test fixture exercises "createPlugin-only, no destroyPlugin" but
   does NOT exercise CRT-mismatch (CRT mismatch is fundamentally unsafe and
   the new symmetric ABI is the only sound answer — the legacy fallback is
   best-effort for /MD-same-CRT plugins only).
2. **Or** mark the test `[Fact(Skip = "...")]` with an explanatory comment
   that it exercises a known-unsafe path retained for backward-compat with
   /MD-CRT-match legacy plugins only and that the /MT-CRT-mismatch path
   would in fact heap-corrupt under load.
3. Tighten the documentation in `plugin_manager.cpp:84-87` to say "best-effort
   for legacy /MD plugins; /MT plugins MUST migrate to the symmetric ABI".
4. Add a `static_assert(sizeof(utinni::UtinniPlugin) == sizeof(utinni_legacy::UtinniPlugin))`
   in a header that LegacyPlugin includes to catch silent layout drift if a
   future change adds a virtual method to the base.

## Warnings

### WR-01: `_ReturnAddress` used inside `__try` blocks without SEH-translator safeguards

**Disposition:** `fixed: 9626174` -- promoted s_ioEventLogCount to std::atomic<int> with fetch_add + load-peek before the fetch to avoid gratuitously inflating the counter past the 40-cap. _ReturnAddress() /O2-trampoline limitation documented explicitly (de-optimizing the whole function for PC accuracy is not warranted).

**File:** `UtinniCore/swg/scene/ground_scene.cpp:266`

**Issue:** `_ReturnAddress()` is a compiler intrinsic that probes the call
stack at runtime. The code at ground_scene.cpp:266 (`const void* callerPC =
_ReturnAddress();`) lives inside `hkHandleInputEvent` and is captured BEFORE
any optimization barrier. With MSVC `/O2`, the compiler can inline parts of
the hook, which can shift the return address relative to where the
log-throttle decides to capture it. In practice the captured PC will often
point inside the trampoline, not the original caller. Not a crash but the
diagnostic value is suspect — and the throttle (`s_ioEventLogCount < 40`)
means once you've hit the cap, the data set frozen-in is partial. Also note
the `static int s_ioEventLogCount = 0;` is **not thread-safe** — it's
incremented without lock and decremented nowhere; on a multi-threaded
event-handling chain you could log >40 entries before the cap fires due to
read-modify-write races.

**Fix:** Either guard the counter with `std::atomic<int>` and a CAS loop,
or accept the race as diagnostic-only and document it. For `_ReturnAddress`
accuracy, mark the enclosing function `__declspec(noinline)`.

---

### WR-02: `~PluginManager` calls `destroyFn` (lives in DLL X), THEN `FreeLibrary(X)` — but if `destroyFn` was the last reference and unloaded X mid-call, the return crashes

**Disposition:** `fixed: cb6fad3` -- doc-only. Lifecycle contract added to `utinni_plugin.h` covering all four failure modes the reviewer flagged (callback unsubscription, log re-entrancy, DLL_PROCESS_DETACH ordering, no FreeLibraryAndExitThread from destroyPlugin). Concern is theoretical for the current plugin set (TJT, CrtMatchPlugin) but worth pinning down for future authors.

**File:** `UtinniCore/plugin_framework/plugin_manager.cpp:76-93`

**Issue:** Shutdown order is:
1. `loaded.destroyFn(loaded.plugin);` — code in `loaded.hModule` runs
2. `FreeLibrary(loaded.hModule);` — refcount drops by one

The host has `hModule` from `LoadLibrary` at plan-time AND a refcount bump
from the DLL's own `DllMain` initialization. `FreeLibrary` decrements once.
If the plugin DLL was loaded only by the host (which is typical), the
refcount is 1 going into `FreeLibrary`, so the call unloads the module
**before returning** when no other holders remain. Inside `destroyFn`, the
plugin may have stored function pointers to UtinniCore-callbacks
(`subscribeInstallCallback` etc.) that, on `destroyPlugin` body, should
unsubscribe — but the plugin's `~Plugin` body runs in step 1, then step 1
returns to host code, then step 2 unloads. The return path through DLL X's
unloaded code is fine because we're outside DLL X by then.

However: `destroyFn(plugin)` invokes `delete plugin` in plugin X's CRT.
That `delete` body lives in X (the plugin's CRT). After `delete` returns,
control returns to `destroyFn`'s epilogue (still in X) and then to the
caller in UtinniCore.dll. Fine — same TU. The risk is if `destroyFn`
itself fires anything that touches static state in X (e.g. a static
destructor) AFTER returning from this thunk; that's deferred to
`FreeLibrary`-driven `DLL_PROCESS_DETACH`. Order of statics destruction
during `FreeLibrary` is implementation-defined; if the plugin held an
internal `std::vector<int>` of subscribed handles and tried to unsubscribe
each in `~Plugin`, it would call back into the host (fine, the host is
still loaded). But that path is fragile.

A specific failure mode: if a plugin's `destroyPlugin` body calls `log::info(...)`
to announce shutdown (very common), and the host already removed the plugin
from `outputSinkCallbacks` — wait, the plugin's own outputSink callback was
never registered through the new R-A path — but `Log::Info` from C# is the
managed flavor; the native plugin would use `utinni::log::info` directly.
That goes through spdlog's `OutputSink::sink_it_`, which iterates
`outputSinkCallbacks` (unguarded — see CR-01). If a managed-side
`outputSinkSubscribers` still has the plugin's `Action<string>` lambda from
before unsubscribe, the dispatch fires into managed code which on shutdown
may have a torn-down state. Not Phase-3-introduced but the new
Subscribe/Unsubscribe handle API made it MORE likely that a plugin will hold
handles needing cleanup at destroyPlugin time.

**Fix:** Document in `utinni_plugin.h` the lifecycle contract: "destroyPlugin
runs while the framework is still fully functional; do NOT call
`utinni::log::*` from inside destroyPlugin if you previously subscribed a
log output sink (your callback may fire on its own deregistration thunk)."
Add a unit test that exercises a plugin whose destroyPlugin body issues a
`log::info` — confirm no crash, no log-callback misfire.

---

### WR-03: `Game::triggerInstallCallbacks()` is a test-only accessor declared in the public `UTINNI_API` `Game` class header

**Disposition:** `fixed: 9248a1a` -- chose option 1. New header `UtinniCore/swg/game/game_test_internal.h` declares `utinni::test_internal::triggerInstallCallbacks()` + `getInstallSubscriberCount()` as free functions. Implementations moved out of the `Game` class into the `utinni::test_internal::` namespace in `game.cpp`. `test_exports.cpp` updated to call the new free functions. CppSharp ignore-list updated so the test header doesn't leak into managed bindings.

**Files:**
- `UtinniCore/swg/game/game.h:82-88`
- `UtinniCore/swg/game/game.cpp:504-525`

**Issue:** Both `triggerInstallCallbacks` and `getInstallSubscriberCount` are
declared in the public `Game` class header with `UTINNI_API` visibility,
and the cpp comment marks them as "Test-only". A plugin author including
`swg/game/game.h` would see these as part of the public surface — there's
no documentation that they're test-only at the header level (only at the
cpp body and the cpp comment after the declaration). The whole point of
the `test_internal::TestImpl` pattern in plugin_manager.cpp (R-B) was to
keep test seams out of public headers. R-A leaked one through.

**Fix:** Either:
1. Move these to a separate `swg/game/game_test_internal.h` not included by
   default consumers, and have `test_exports.cpp` include it. Pattern aligns
   with the `test_internal::` namespace used for plugin_manager's TestImpl.
2. Or annotate the header declaration with a clear `// TEST-ONLY: do not
   call from plugin code` comment so the test-only intent is visible at
   the API surface.

---

### WR-04: `static int s_next*Id = 1` integer overflow with no documentation; long-running session could wrap to 0 (the reserved sentinel)

**Disposition:** `fixed: c1681bd` -- skip-zero guard `if (id == 0) { id = s_nextXxxId++; }` added to every Subscribe* on both native (11 files, 32 registries) and managed (6 files, 10 registries) sides. The skip burns 1 handle slot every 2^32 subscribes (negligible). 2^31 ceiling remains as the underlying limit; this guard catches the edge of that ceiling.

**Files (all native registries):**
- `UtinniCore/swg/game/game.cpp:81-85`
- `UtinniCore/swg/scene/ground_scene.cpp:78-81`
- `UtinniCore/swg/graphics/graphics.cpp:104-113`
- `UtinniCore/swg/graphics/depth_texture.cpp:46`
- `UtinniCore/swg/graphics/shader.cpp:40`
- `UtinniCore/swg/graphics/post_processing.cpp:43-44`
- `UtinniCore/swg/ui/imgui_impl.cpp:62, 459-462`
- `UtinniCore/swg/ui/cui_chat_window.cpp:71`
- `UtinniCore/swg/ui/cui_manager.cpp:71`
- `UtinniCore/utility/log.cpp:35`
- All managed `*Callbacks.cs` and `Log.cs` files

**Issue:** Every registry uses `static int s_next*Id = 1` with `s_next*Id++`
to allocate handles. `int` overflow at 2^31 (~2.1B subscriptions) wraps to
`INT_MIN` and continues incrementing through negative range up to 0 — at
which point a Subscribe returns the reserved sentinel handle 0, and the
caller's `Unsubscribe(0)` returns false universally (per D-09). A plugin
that holds onto its handle expecting to unsubscribe later silently leaks.

Yes, 2.1B subscribes is enormous — but the design IS that handle 0 means
"invalid" and the post-overflow path actively gives clients invalid handles
without any indication. Worth either: (a) `static_assert` on a `s_nextId >
0` postcondition with a `++` overflow check, or (b) bump to `uint64_t`
(native) / `long` (managed) where overflow is practically unreachable, or
(c) document that overflow behavior is undefined.

A more practical concern: handle ID collisions across plugin subscribe/
unsubscribe churn. After many subscribe+unsubscribe cycles, `s_nextId` keeps
growing while map entries are erased. A long-running session running
SubscribeInstall in a frame loop (deliberate or accidental — say a plugin
that re-subscribes on every BeginScene callback) reaches 2^31 in a few days
at 60fps. Real on a Steam game running unattended.

**Fix:** In the `subscribe*` body, after the post-increment, check
`if (s_next*Id <= 0) { s_next*Id = 1; }` to wrap-and-skip-zero. Better:
log an error when overflow is detected. Or accept the limitation but
document it in the comment at the registry declaration: "Practical limit:
2^31 subscribes per registry per process lifetime; overflow returns invalid
handle 0 with no warning."

---

### WR-05: `_chatInputActive` is a non-atomic `static bool` written from multiple threads

**Disposition:** `fixed: e17d123` -- s_chatInputActive promoted to std::atomic<bool> with relaxed ordering. The hkEnableTextInput diag s_logCount also promoted to std::atomic<int> in the same commit (companion fix to avoid double-allocating slots past the 30-cap under concurrent firings).

**File:** `UtinniCore/swg/ui/cui_chat_window.cpp:78-79, 256-260, 314-316, 386-396`

**Issue:** `s_chatInputActive` (line 78) is read on the imgui WndProc thread
(via `CuiChatWindow::isChatInputModeActive()` and `hkChatEnter`) and written
on the SWG main thread (via `hkEnableTextInput` line 316,
`forceOpenChatInputFromCpp` line 258, `hkChatEnter` line 394). x86's strong
memory model permits this without observable tearing on `bool`, but the
pattern is fragile and the next platform port (or compiler that lifts the
read into a register) breaks. Not Phase-3 critical but R-A's overall
"thread-safety via lock" theme makes this stand out as the one
non-callback shared-state variable left raw.

**Fix:** Change to `static std::atomic<bool>` and use `.load(memory_order_acquire)`
/ `.store(value, memory_order_release)`.

---

### WR-06: `pCuiChatWindow == 0` check race — concurrent ctor + reader

**Disposition:** `fixed: f72721d` -- pCuiChatWindow + pCuiConsoleHelper promoted to std::atomic<swgptr>. Release store on publish in hkCtor pairs with relaxed loads at the four reader sites (writeToAllTabs, writeToCurrentTab, forceOpenChatInputFromCpp, sendMessage). Pointer value is the synchronization datum; the SWG objects pointed at finished construction inside swg::cuiChatWindow::ctor before the publish.

**File:** `UtinniCore/swg/ui/cui_chat_window.cpp:67, 113-118, 248-258, 262-268, 335`

**Issue:** `pCuiChatWindow` is a file-scope `swgptr` written from `hkCtor`
(line 335) on whatever thread SWG constructs the CuiChatWindow on (typically
main thread post-login). It's read by `writeToAllTabs`, `writeToCurrentTab`,
`forceOpenChatInputFromCpp`, `sendMessage` — all with a simple
`if (pCuiChatWindow == 0)` guard. The write isn't atomic; the read isn't
either. If a plugin's keyboard handler thread happens to call
`writeToCurrentTab` exactly while `hkCtor` is mid-construction, the read can
observe a partially-written pointer or, more commonly, never observe the
write because of CPU-cache visibility.

Same applies to `pCuiConsoleHelper` (line 68, written line 336, read line 263,
read line 280).

**Fix:** Mark both as `static std::atomic<swgptr>`. Or document
that all `CuiChatWindow::*` static API calls are unsafe before the chat
window is fully constructed (the current code half-acknowledges this
with the warning log on line 250-251 but only for one of the four
callsites).

---

### WR-07: `outputSinkCallbacks` (native) iterated under spdlog's sink mutex but Subscribe/Unsubscribe is unguarded — partial protection only

**Disposition:** `fixed: 427f474` -- folded into the CR-01 commit per the WR-07 fix guidance ("As part of CR-01, add an explicit `std::mutex outputSinkMutex`..."). An explicit outputSinkMutex now bridges spdlog's partial sink-mutex protection: subscribe / unsubscribe and the OutputSink::sink_it_ snapshot-build all take outputSinkMutex.

**File:** `UtinniCore/utility/log.cpp:39-69, 119-138`

**Issue:** `OutputSink` inherits from `spdlog::sinks::base_sink<std::mutex>`,
so `sink_it_` acquires the spdlog sink mutex before iterating
`outputSinkCallbacks`. Good — that protects the **iteration**. But
`subscribeOutputSinkCallback` and `unsubscribeOutputSinkCallback` write
to the same map without acquiring spdlog's sink mutex (line 121:
`outputSinkCallbacks[id] = func;` and line 132: `outputSinkCallbacks.erase(...)`).
The sink mutex protects writes the sink itself does (none here — sink reads
only) and the iteration. Concurrent Subscribe from one thread and dispatch
from another races on the map's bucket structure. This is a specific
instance of CR-01 but with a partial guard that gives a false sense of
safety.

**Fix:** As part of CR-01, add an explicit `std::mutex outputSinkMutex` and
bracket both subscribe/unsubscribe and the iteration in `sink_it_`. Don't
piggyback on spdlog's internal sink mutex (its contract is "the sink's own
formatter calls"; map mutation is not in that contract).

## Info

### IN-01: `ExportResolutionTests.cs` XML doc-comment says "13 exports resolve" but the constant is now 22

**Disposition:** `deferred: info-tier, out of fix scope per /gsd-code-review --fix scope_guard.`

**File:** `UtinniCoreDotNet.Tests/ExportResolutionTests.cs:44`

**Issue:** Line 44 in the class summary: "Expected: 13 exports resolve (as of
2026-05-19)." The constant on line 75 is `ExpectedExportCount = 18`... wait,
the line reads `private const int ExpectedExportCount = 22;`. The summary
docs are stale by two phases of additions. Inside the per-list documentation
(lines 60-74) the additions ARE listed correctly, but the top-line summary
contradicts the constant.

**Fix:** Update the class-summary comment on line 44 to say "Expected: 22
exports resolve (as of 2026-05-21)." or remove the count from prose and
rely on the `ExpectedExportCount` constant + per-export list below.

---

### IN-02: `Utinni.CrtMatchPlugin.vcxproj` comment claims fixture "Does NOT link against UtinniCore" but `AdditionalDependencies` lists `UtinniCore.lib`

**Disposition:** `deferred: info-tier, out of fix scope.`

**File:** `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj:70, 94, 102-105`

**Issue:** Lines 70 and 94 add `UtinniCore.lib` as an `<AdditionalDependencies>`
entry — the fixture DOES statically link against UtinniCore. The comment at
102-105 says: "Does NOT link against UtinniCore; the framework LoadLibrary's
the fixture at runtime." That contradicts the dependency. Plan 03-02
SUMMARY's deviation #3 explains that linking became necessary because the
`UtinniPlugin` base class is `UTINNI_API` and the vtable needs to resolve at
link time, but the project XML's `<LinkLibraryDependencies>false</LinkLibraryDependencies>`
comment + the new `AdditionalDependencies` together create a confusing
picture.

**Fix:** Update the inline comment on lines 102-105 to say: "Links against
UtinniCore.lib for the UtinniPlugin base-class vtable; the loader LoadLibrary's
this fixture at runtime regardless. LinkLibraryDependencies=false because
the build chain handles the dependency via the explicit AdditionalDependencies
entry above."

---

### IN-03: `dumpStringAt` in `cui_chat_window.cpp` uses `__try`/`__except` SEH which conflicts with /EHs build flags

**Disposition:** `deferred: info-tier, pre-existing pre-R-A.`

**File:** `UtinniCore/swg/ui/cui_chat_window.cpp:163-188, 201-218`

**Issue:** The `__try { ... } __except(EXCEPTION_EXECUTE_HANDLER) { ... }` blocks
in `dumpStringAt` and `dumpActionStringSlotsFromCpp` use Windows SEH (Structured
Exception Handling). UtinniCore.vcxproj's `<ExceptionHandling>` defaults to `Sync`
(C++ exceptions only, no SEH). The code only compiles because MSVC tolerates SEH
in `Sync` mode but emits warnings; the AV catch-handler is **best-effort only**
in this build mode. Not Phase-3-introduced (was already there pre-R-A) — flagging
for awareness given the diagnostic intent. The `__except` body cannot run C++
destructors safely without `/EHa`, which UtinniCore doesn't set.

**Fix:** Document the SEH-vs-C++-EH limitation in the comment block before
`dumpStringAt`, or migrate the AV-guard to `IsBadReadPtr` (deprecated but
synchronous and exception-free) or `try/catch (std::exception&)` with
explicit pointer-range validation.

---

### IN-04: `IgnoreHeadersWithName` extension comment in `Program.cs` notes "Phase 6 STAB-03" cleanup but no tracking issue cited

**Disposition:** `deferred: info-tier, out of fix scope.`

**File:** `UtinniCoreDotNetGen/Program.cs:119-135`

**Issue:** The R-F header-discovery glob picks up headers (`swg_string`,
`command_parser`, `utinni_command_parser`, `ui_textbox`, `string_utility`)
that don't project cleanly through CppSharp. The Program.cs ignore-list
papers over this with a `Phase 6 STAB-03` reference but no issue number,
no TD-NN, no FIXME tag in the canonical convention (the codebase uses
`// ToDo`, not `// FIXME`). A future maintainer searching for "TODO" or
"FIXME" won't find this; the cleanup risks being forgotten.

**Fix:** Add a `// ToDo` per CONVENTIONS.md§"ToDo Tag" alongside each
ignore line, citing the eventual `_internal/` migration target. Or add a
single tracking entry to a TD-NN concern list in `.planning/codebase/CONCERNS.md`
and cite that ID here.

---

### IN-05: Diagnostic log lines in `game.cpp` `hkInstall`/`hkMainLoop`/`hkSetScene`/`hkCleanupScene` are unconditional — log volume on long sessions

**Disposition:** `deferred: info-tier, tied to Issue #11 / Issue #12 lifecycle (diag is still useful while those issues are open).`

**File:** `UtinniCore/swg/game/game.cpp:255-272, 278-317, 320-353, 356-383`

**Issue:** The "DIAG 2026-05-19" log instrumentation in
`hkMainLoop`/`hkInstall`/`hkSetScene`/`hkCleanupScene` calls
`utinni::log::info` 4-6 times per scene transition. `hkInstall` and
`hkSetScene` only fire once per scene change, so volume is modest. But
each call serializes into spdlog's basic_file_sink and serves no
forward-going user value once the original 2026-05-19 issue is resolved.
With CR-01 active (no synchronization on the spdlog output-sink callback
list), each `log::info` call iterates `outputSinkCallbacks` under spdlog's
sink mutex while a managed Subscribe could be writing the map.

**Fix:** Either gate the DIAG logs behind a runtime flag (e.g.
`getConfig().getBool("Log","diag_scene")`) or remove them now that scene-load
state-machine debugging is complete. Lower priority because the volume is
low.

---

### IN-06: `Log.cs` adds `using System.Linq` import unused in source — but is required at runtime for `Dictionary<,>.Values.ToArray()`

**Disposition:** `accepted: finding text explicitly says "No code change required; flagging for future maintainers."`

**File:** `UtinniCoreDotNet/Utility/Log.cs:28`

**Issue:** `using System.Linq;` is added by Phase 3. Grep finds no
`.Where`/`.Select`/etc. usage in `Log.cs`. But the file does use
`outputSinkSubscribers.Values.ToArray()` on line 220 — `Dictionary<,>.ValueCollection`
implements `ICollection<T>`, and `.ToArray()` on it comes from
`System.Linq.Enumerable`. So the import is actually used; just easy to miss.
Not actually unused — but the same code in `GameCallbacks.cs:284`
(`installSubscribers.Values.ToArray()`) compiles only because that file
imports `System.Linq` too. Worth a comment in any future refactor.

**Fix:** No code change required; flagging for future maintainers who may
strip "unused" using directives via automated tooling.

## Cross-Cutting Findings

**Snapshot iteration consistency:** All 13 managed dispatch sites
(`GameCallbacks.CallInstallCallbacks`, `CallSetupSceneCallbacks`,
`CallCleanupSceneCallbacks`; `GroundSceneCallbacks.CallCameraChangeCallbacks`;
`ObjectCallbacks.DequeueOnTargetCalls`; `CuiCallbacks.DequeueOnReceiveSystemMessageCallbacks`;
`ImGuiCallbacks.OnEnabledCallback/OnDisabledCallback/OnPositionChangedCallback/OnRotationChangedCallback`
via the shared `DispatchSnapshot` helper; `Log.CallOutputSinkCallbacks`) apply
the snapshot-under-lock + iterate-outside-lock pattern uniformly. All 32
native dispatch sites build a `std::vector` snapshot before iteration —
syntactically correct, but **without a lock the snapshot build itself is
racey** (CR-01).

**Handle-0 sentinel discipline:** Every Subscribe path returns a non-zero
handle (`s_next*Id` starts at 1, post-increment), and every Unsubscribe path
short-circuits `handle == 0` to `return false`. Consistent across native and
managed — except for the integer-overflow case (WR-04) where the post-2^31
behavior produces 0 as a regular value.

**`Add*Callback` D-10 wrappers:** Retained as thin wrappers around `Subscribe*`
everywhere required. `RemoveOnTargetCallback`/`RemoveCameraChangeCallback`/
`RemoveInstallCallback`/etc. are best-effort delegate-equality scans (slow
but correctness-preserving). The `ImGuiCallbacks` Remove pair previously
missing for `onEnabled`/`onDisabled` is now complete. `CuiCallbacks.RemoveOnReceiveSystemMessageCallback`
similarly added. Good.

**`UTINNI_PLUGIN` macro break:** The new macro definition mandates a matching
`destroyPlugin` body. The `Utinni.LegacyPlugin` fixture deliberately
side-steps this by declaring its own `utinni_legacy::UtinniPlugin` mirror
class — clever but with the layout-drift risk noted in CR-02.

**CON-N-08 byte-identity:** `plugin_manager.h` is unchanged (verified via
the test_internal::TestImpl pattern keeping `Impl::LoadedPlugin` entirely in
the `.cpp`).

**LoadLibrary failure path:** `GetLastError()` IS called immediately after
the `LoadLibrary` failure check (line 204 `const DWORD err = GetLastError();`)
before any other Win32 call could clobber it. Good — Phase 3 caught this
correctly. The `test_internal::test_loadFromDirectory` mirror has the same
discipline (line 342).

**`Native.GetSwgWndProc()` cached read:** The `readonly IntPtr swgWndProcAddr`
field is initialized in PanelGame's ctor body (line 92) before the WndProc
hot-path executes. WndProc cannot fire before ctor completes (Win32 message
pump doesn't start until handle creation; Handle isn't created until
HandleCreated event, which is wired AFTER the field-set). Safe.

**XXE mitigation:** Props.cs explicitly creates an `XmlReader` with
`DtdProcessing = Prohibit` + `XmlResolver = null` and wraps it in a
`using` block — this is the documented .NET 4.7.2 workaround for
`XDocument.Load(string)`'s non-prohibition default. DirectoryBuildPropsTests
Fact 5 asserts the XmlException is thrown for a DOCTYPE input. Properly
plumbed end-to-end.

## Verdict

Phase 3 ships the seven strategic reworks with strong test coverage and
careful preservation of CON-N-08. The work succeeds at its stated goals —
plugin authors will find the new Subscribe/Unsubscribe + destroyPlugin
surface workable, the single-source-RVA refactor cleanly eliminates the
duplicated 0x00AA0970 literal, and the Props.cs idempotent merger is a
clear improvement on the destructive early-return that was there before.
However, **CR-01 (native thread-safety) is a real defect** that will fire
under any plugin that subscribes from a different thread than dispatches —
which is the entire premise of the WinForms-thread-driven plugin model.
**CR-02 (LegacyPlugin /MT mismatch test)** is misleading rather than
unsafe-by-default (most production legacy plugins would be /MD-match) but
the test fixture as written gives false confidence in the fallback path.
Both should be addressed before Wave-1 TJT subpanels (Phases 7-11) start
shipping new callback-using code on the new ABI.

The seven Warning items are correctness/robustness hardening (notably WR-04
handle-0 sentinel overflow and WR-05/WR-06 atomic statics in
`cui_chat_window.cpp`). Info items are cleanup. Recommended status:
**issues_found** with two BLOCKER findings that warrant follow-up commits
before this phase is "done" in the strictest sense — though the documented
verification (76 → 104 tests passing, msbuild green) confirms the happy
paths all work and CI gates remain intact.

---

_Reviewed: 2026-05-21_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
