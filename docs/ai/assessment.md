# Code-quality assessment

> **Status:** Audit completed 2026-05-16 across three parallel reviewers
> (native C++, managed C#, build/SDK/tooling). This document captures the
> full findings as a single reference. It's the work-list that gets us to a
> stable 1.0 base for the [vision](vision.md) — a one-stop modding tool
> for SWG.

## Executive summary

**The architecture is solid. The execution has real bugs but they're all
localised and fixable — none touch the foundational design.**

The framework's *shape* is good: clean separation between the SWG shim
layer (`swg::*`), the public façade (`utinni::*`), the CLR bridge, the MEF
plugin contract, and the editor host. The detour-installation convention is
uniform and easy to extend. The two-language plugin model (C++ + C#) is
consistent and well-documented. The undo/redo event model, hotkey manager
with INI persistence, themed WinForms controls, and `*Impl`-separated
plugin pattern (from the Jawa Toolbox) are all things to *keep*.

The risky stuff lives in well-known files — none of them are foundations.
You can ship fixes incrementally without rewriting anything. The biggest
investment isn't a code rewrite; it's adding **CI** so the next change
doesn't silently regress.

### Effort estimate

| Phase | Effort | Outcome |
| --- | --- | --- |
| Fix the 15 critical bugs | ~2 person-weeks | Framework is reliable. No more silent failures. |
| Do the 8 strategic reworks | ~3–4 person-weeks | Plugin authoring is genuinely pleasant. CI catches regressions. |
| Cleanups + dep bumps | ~1 person-week | Modern toolchain, no dead code. |
| **Total to a confident "1.0"** | **~6–8 person-weeks** | Sovereign fork ready to advance independently of upstream. |

### Findings summary

- **15 critical issues** — must fix; cause crashes, silent failures, or data loss
- **8 strategic reworks** — 1–2 days each, significant pay-back
- **~30 cleanups** — low-risk tidying, dead-code removal, naming consistency
- **24 solid foundations** — explicitly call-out so they don't get touched
- **8 open questions** — need someone-who-knows-the-history to answer

---

## 🔴 Critical issues (must fix)

Ordered roughly by impact.

### C-01 — `DllMain` does `CreateThread → main() → LoadLibrary` inside the loader lock

**File:** `UtinniCore/utinni.cpp:138-151`
**Problem:** The spawned thread immediately walks plugin DLLs via
`LoadLibrary` and brings up the CLR (`CoInitializeEx`, `mscoree`).
Microsoft explicitly forbids both inside `DLL_PROCESS_ATTACH`. Works today
only because the launcher's `WaitForSingleObject(hThread)` happens to
serialize things. Load-bearing luck.
**Fix:** Defer all heavy startup until the first SWG callback fires (e.g.
inside `Game::install`), or have the launcher trigger startup via a separate
`CreateRemoteThread` call to an exported `utinni_init` function after
`LoadLibraryA` returns.
**Severity / effort:** High / Medium.

### C-02 — Cross-CRT `delete[]` in config override path

**File:** `UtinniCore/swg/misc/config.cpp:65-72`
**Problem:** SWG allocates the buffer with its own CRT; Utinni `delete[]`s
with its own. Undefined behaviour; the following line then double-frees via
the SWG dtor.
**Fix:** Use SWG's own buffer-free function. Verify from IDA decomp.
**Severity / effort:** High / Medium.

### C-03 — `Network::cast` returns uninitialized stack memory

**File:** `UtinniCore/swg/misc/network.cpp:65-69`
**Problem:** `swgptr networkId;` is never written before the cast call,
and the call's return value is discarded. Comment `// This is broken`
admits it. Caller `WorldSnapshotReaderWriter::Node::getNodeNetworkId`
blindly forwards garbage.
**Fix:** Reverse-engineer the real ABI (likely returns through the first
parameter) or remove the function until correct.
**Severity / effort:** High / Low.

### C-04 — `GroundSceneCallbacks.DequeuePostDrawLoopCalls` drains the wrong queue

**File:** `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:97-106`
**Problem:** Drains `preDrawLoopCallQueue` instead of
`postDrawLoopCallQueue`. `AddPostDrawLoopCall` is effectively a no-op for
post-draw semantics. Likely undetected since 2020.
**Fix:** Two-line change. Factor a `Drain(ConcurrentQueue<Action>)` helper
so this category of bug can't reoccur.
**Severity / effort:** High / Low.

### C-05 — `GameDragDropEventHandlers` static-field pattern silently breaks drag-drop

**File:** `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs:33-44`,
called from `UI/Controls/PanelGame.cs:68`
**Problem:** `Initialize(panel)` is called before any subscriber exists, so
it wires `panel.DragDrop += null` once and that's it. Later
`OnDragDrop += handler` only mutates the static field, never the panel's
event. Plugin drag-drop handlers never fire on the live game window. (The
Jawa Toolbox object-browser drag-drop may be working via a different path;
needs verification.)
**Fix:** Replace with a proper `static event` and a single forwarder, or
expose the `PanelGame` instance and let plugins subscribe directly.
**Severity / effort:** High / Low.

### C-06 — `PluginLoader.Load` swallows every plugin exception silently

**File:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39-73`
**Problem:** If any plugin DLL throws during composition (missing
dependency, ctor exception, ambiguous export, x86/x64 mismatch, hotkey ctor
crash — see C-08), `ComposeParts` throws and the *entire* editor tears
down with no message telling the user which plugin caused it.
**Fix:** Wrap per-plugin in its own `AssemblyCatalog` + try/catch. Log the
offending DLL name and `ReflectionTypeLoadException.LoaderExceptions[*]`.
Allow surviving plugins to load.
**Severity / effort:** High / Low–Medium.

### C-07 — `UndoRedoManager` is thread-unsafe and `AllowMerge` is dead code

**File:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:60-74`
**Problem:** `Stack<T>` mutated from the game thread (when commands are
pushed from callbacks) and the UI thread (when user clicks Undo) without
locks. `AllowMerge()` is declared but **never called** — the manager always
invokes `Peek().Merge(new)` and trusts the return. `RedoCommands.Clear()`
happens before the merge check, so a merged-away command still clears redo.
**Fix:** Lock around stack mutations. Decide what `AllowMerge` means and
either call it or delete it. Document the merge contract.
**Severity / effort:** High / Medium.

### C-08 — `Hotkey.ProcessString` throws on any unknown enum token → triggers C-06

**File:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66-92`
**Problem:** `Enum.Parse` on a typo'd `input.ini` ("Ctrl + T") throws from
the plugin ctor → MEF composition fails → entire editor dies silently
(because of C-06). Compounding bug.
**Fix:** `Enum.TryParse`. Log + disable the hotkey on failure rather than
throwing.
**Severity / effort:** High / Low.

### C-09 — UI thread busy-waits on the game thread during minimize / restore

**File:** `UtinniCoreDotNet/UI/Forms/FormMain.cs:57-78`
**Problem:** `WM_SYSCOMMAND` handler does `BlockPresent(true)` then
`while (IsPresentBlocked()) Thread.Sleep(1)`. If the game thread is
awaiting the UI thread for any reason, hard deadlock. Comment admits
"Find better solution in the future."
**Fix:** Timed `WaitOne` on an event the native side signals, with a
fallback timeout (e.g. 100 ms).
**Severity / effort:** High / Medium.

### C-10 — `clr::stop()` dereferences nulls after a failed `clr::load()`

**File:** `UtinniCore/clr.cpp:93-102`
**Problem:** No null checks on `Release` calls. If startup fails (any of
four `SUCCEEDED(hr)` branches) the cleanup path already nulled the
pointers — then `detatch()` calls `stop()` again from
`DLL_PROCESS_DETACH` and crashes.
**Fix:** Null-check before each `Release`, null after. Or `ComPtr`.
**Severity / effort:** High / Low.

### C-11 — DirectX9 hook installation has no null check on pattern scan

**File:** `UtinniCore/swg/graphics/directx9.cpp:297-303`
**Problem:** `findPattern` can return 0 (pattern not found, `d3d9.dll`
not yet loaded). The code then does `memcpy` from address `0x2` →
immediate crash.
**Fix:** Bail with logged error if `findPattern` or
`GetModuleHandle("d3d9.dll")` returns 0.
**Severity / effort:** Medium / Low.

### C-12 — VSIX manifest pins to VS 2019 only (`[16.0,17.0)`)

**File:** `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest:9-11,17`
**Problem:** Any contributor with VS 2022 cannot install the templates.
The most visible thing breaking new-user onboarding today.
**Fix:** Widen to `[16.0,18.0)`. Bump `Microsoft.VisualStudio.SDK`
PackageReference. Test extension load in both IDEs.
**Severity / effort:** High / Medium.

### C-13 — Jawa Toolbox Debug config has a wrong relative path

**File:** `UtinniPlugins/The Jawa Toolbox/TheJawaToolbox/TheJawaToolbox.vcxproj:63`
**Problem:** Uses `..\..\..\..\` (four) while Release uses `..\..\..\`
(three). Debug builds drop their output in `D:/bin/` — nowhere usable. The
`.sln` also lacks `Debug|Win32.Build.0` for this project, masking the
breakage.
**Fix:** Three dots. Restore the `.sln` build entry.
**Severity / effort:** High / Trivial.

### C-14 — `utinni.cfg` ships with `login.swgemu.com:44453` as default

**File:** `data/utinni.cfg:4-5`
**Problem:** For a sovereign fork this defaults users into SWGEmu's
infrastructure. Some shards may not auth Utinni-launched clients — could
violate ToS too.
**Fix:** Blank both. Add comment about set-your-server-host.
**Severity / effort:** High / Trivial.

### C-15 — CppSharp `slnDir` computation is brittle

**File:** `UtinniCoreDotNetGen/Program.cs:39-41`
**Problem:** Requires the binary's path to literally contain `\bin\`. CI
runners or non-default output directories throw
`ArgumentOutOfRangeException`.
**Fix:** Pass `$(SolutionDir)` as `args[0]`, or walk up looking for
`Utinni.sln`, or env var.
**Severity / effort:** High / Low.

---

## 🟡 Strategic reworks worth doing

These need 1–2 days each but pay back significantly:

### R-A — Symmetric callback `Add` / `Remove` everywhere

Native callbacks take raw C function pointers and have **no `Remove`**
anywhere. Managed `Add*Callback` has asymmetric `Remove` coverage (some
have it, some don't). Plugins that subscribe and then dispose continue to
fire callbacks against dead controls — eventual `ObjectDisposedException`
on `Control.BeginInvoke`.
**Fix:** Standardize on a tagged-handle pattern: `Subscribe()` returns an
`IDisposable` / opaque handle, `Unsubscribe(handle)` removes it. Or
convert to proper `event` semantics. Mechanical across ~15 files but worth
it.

### R-B — Plugin lifecycle contract

Native `PluginManager::~PluginManager` does `delete plugin` — only safe if
plugin uses the same CRT. Adding a `destroyPlugin` symmetric export is the
right contract. While there: log `LoadLibrary` failures (currently
silent), track `HMODULE`s, and call `plugin->init()` after the load loop
(it's declared but **never called** today).
**Fix:** Symmetric `createPlugin` / `destroyPlugin` ABI. Call `init()`
after the load loop. Log failures. Track and `FreeLibrary` on shutdown.

### R-C — Single source of truth for hard-coded RVAs

`0x00AA0970` (SWG WndProc) is duplicated in
`UtinniCore/swg/client/client.cpp:43` and
`UtinniCoreDotNet/UI/Controls/PanelGame.cs:40`. Same with the two
`isSafeToUse` flag addresses (and the doc says `&&` while the code uses
`||` — actual bug somewhere).
**Fix:** Expose `Client::getSwgWndProc()` via `UTINNI_API`, have
`PanelGame.WndProc` resolve it at runtime. Same pattern for any other RVA
that leaks out of `UtinniCore/swg/*`.

### R-D — Add CI — even just a build job

There is no CI, no tests, no analyzers, no `.editorconfig`. A single
30-line `.github/workflows/build.yml` invoking `msbuild Utinni.sln
/p:Configuration=Release /p:Platform=x86` catches 90% of regressions
immediately.
**Fix:** Build workflow first. `.editorconfig` second. Smoke-test xUnit
project third.

### R-E — Replace `Log` `StackTrace` reflection with `[CallerMemberName]`

`UtinniCoreDotNet/Utility/Log.cs:50-69`. Walks the stack on every call —
expensive when class/function name prefixing is enabled.
`[CallerMemberName]` and `[CallerFilePath]` are compile-time, free at
runtime.
**Fix:** Mechanical refactor.

### R-F — CppSharp header auto-discovery

`UtinniCoreDotNetGen/Program.cs:67-92` lists 27 headers; the
`UtinniCore/swg/` tree has ~60. New C++ APIs silently miss being projected
to managed unless someone remembers to register them in `Program.cs`. The
TODO comment admits this.
**Fix:** Glob `UtinniCore/**/*.h`, filter by blocklist / `_internal/`
convention.

### R-G — `Directory.Build.props` wizard is destructive-by-omission

`sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs:9-14`. If a
`Directory.Build.props` already exists, the wizard silently returns —
plugin then fails to find `UtinniCoreDotNet.dll` with no useful error.
**Fix:** Idempotent merge (parse existing, inject missing properties) or
emit a separate `Utinni.props` and Import it.

### R-H — `SynchronizedCollection` iteration races

Native callback vectors and managed `SynchronizedCollection<T>` callback
lists are iterated without locking around the enumeration. A subscriber
adding/removing during dispatch throws `InvalidOperationException`.
**Fix:** Snapshot `.ToArray()` under the collection's `SyncRoot` before
iterating, then iterate the snapshot. Same pattern for the native
`std::vector` callback stores — copy-on-iterate.

---

## 🟢 Easy cleanups (quick wins)

### Dead code to delete (~250 lines)

- `Launcher/main.cpp:33-172` — the entire `attachToVisualStudio` block with
  `#import` typelib magic. Comment admits it never worked. Recoverable
  from git history if anyone wants it.
- `utinni.cpp:71,75` — commented-out detours (`cuiIntro`,
  `cuiMediatorFactorySetup`).
- `swg/scene/render_world.cpp` `hkRender` / `hkClearVisibleCells` — disabled
  experimental bodies, file is now effectively just
  `addObjectNotifications`.
- `swg/scene/client_world.cpp:46-58,63` — `hkInternalCollide` exists but
  isn't hooked.
- `swg/misc/io_win.cpp:50-57` — `IoWin::hkDraw` is hooked nowhere.
- `swg/ui/cui_chat_window.cpp:166`, `cui_io.cpp:96`, `cui_hud.cpp:164,168`,
  `appearance.cpp:102-103` — commented-out detours.
- `swg/appearance/particle.cpp`, `swg/scene/scene.cpp` — empty `.cpp` files.

### Typos / consistency

- `void detatch()` → `void detach()` in `utinni.cpp:132`.
- `Log.AddOuputSinkCallback` → `AddOutputSinkCallback` (with `[Obsolete]`
  shim for compat). `Utility/Log.cs:121,126`.
- `// Executes/redose` → `redoes` in `IUndoCommand.cs:31`.
- `licenses.txt` has `Jo�o Matos` (mojibake — should be `João`) and is
  missing **DetourXS** and **nvapi** entries.
- Stray semicolons after `#include "utinni.h";` and similar.
- 3-space / 4-space / tab-mixed indentation across the C++ tree. Adopt a
  `.clang-format` and run once.

### Pointlessly inconsistent

- `Add*Callback` (persistent) vs `Add*Call` (queue) naming is not enforced
  by type. Pick one suffix scheme and rename.
- `OnUpdateCommandsCallback` / `OnUndo` / `OnRedo` in `FormMain.cs:230-246`
  are identical — three callbacks plumbed for one effect.
- `Native.SendMessage` uses `int wParam, int lParam` — should be `IntPtr`.
- `TJT.ico` baked into the framework as default form icon — that's a
  plugin's branding, not the framework's.
- Empty `namespace Std {}` blocks in `Generated/StdEdited.cs`.

### Build / config polish

- Windows SDK target versions differ across `.vcxproj` files (`10.0` vs
  `10.0.19041.0` vs `10.0.16299.0`). Pick one in a shared
  `Directory.Build.props`.
- DXSDK include / lib paths only set in `RelWithDbgInfo` config — Debug /
  Release fail silently if user doesn't have `DXSDK_DIR` env var.
- `UtinniCoreDotNetGen.csproj:37-48` has `PlatformTarget=x64` and
  `Prefer32Bit=true` together — incoherent. Drop `Prefer32Bit`.
- `ExampleEditorPlugin.csproj:28` — Release config outputs to
  `bin\Debug\Plugins\...`. Copy-paste bug.
- `.gitignore` excludes `Std.cs` but `StdEdited.cs` is committed — document
  the convention.

---

## ✅ Solid foundations (don't refactor)

These are well-designed and load-bearing — leave them alone:

### Native architecture

- The `swg::<subsystem>` namespace **detour-table pattern**
  (`using pX = ...; pX x = (pX)0xRVA;` then optional `Detour::Create` swaps
  the slot). Uniform, greppable, makes per-build RVA churn a single-table
  find/replace.
- The `utinni::` thin-wrapper firewall over `swg::*` — right separation
  between "messy RE'd SWG" and "callable from CLR + plugins."
- Mid-function naked trampolines (`midPopCell`, `midCrashLogWrite`,
  `midCtor`) with `pushad`/`popad` register save — uniform and correct.
- `utility/memory::copy` / `createJMP` bracket every write with
  `VirtualProtect` save/restore.
- The Launcher's suspended-process + EP-park (`EB FE`) +
  `CreateRemoteThread(LoadLibraryA)` + OEP-restore — textbook
  implementation.
- `imgui_impl` device-loss handling (`isSetup` guard, invalidate/recreate
  on `hkReset`).
- `Game::loadScene`'s two-frame state machine (`loadNewScene` +
  `sceneCleaned` ping-pong).
- `PluginManager` pImpl idiom keeping STL out of the DLL boundary.
- `spdlog::sinks::base_sink<std::mutex>` for the `OutputSink` — correct.

### Managed architecture

- The `IPlugin` / `IEditorPlugin` interfaces are minimal and friendly. Null
  returns from `GetForms()` etc. are a clean low-coupling SPI.
- `[InheritedExport]` for MEF discovery — plugin authors just implement
  the interface.
- `WorldSnapshotCommands` copy-on-construct
  (`new WorldSnapshotReaderWriter.Node(node)`) — captures state at
  command-creation time rather than dereferencing potentially-dangling
  natives.
- `HotkeyManager`'s `CreateSettings` / `Load` / `Save` triplet against
  `UtINI` — clean opt-in persistence.
- `UndoRedoManager.OnCleanupCallback` clearing both stacks on scene-cleanup
  — prevents undoing into a dead world.
- `UtinniForm`'s custom title bar via `OnPaint` + `WM_NCHITTEST` regions —
  sensible WinForms-only approach.
- `Log.AddOutputSinkCallback` pattern (modulo typo): UI-agnostic fanout;
  `FormLog` correctly marshals via `BeginInvoke`.
- `PanelGame.PanelGame_Layout` re-calls `Client.SetHwnd(Handle)` on every
  layout — subtle but correct for re-parenting.
- `FormObjectBrowser`'s drag-drop orchestration (preview-object follows
  cursor, ray-cast via `cui_hud.CollideCursorWithWorld`, commit on drop) —
  genuinely good design worth documenting as canonical.

### Process / tooling

- The `UtinniCore.vcxproj` post-build chain (copy `data/` then run
  `UtinniCoreDotNetGen.exe`) is the right factoring.
- `RelWithDbgInfo` configuration plumbed end-to-end across all five
  projects plus templates plus examples — conscientious.
- Two-language template parity (C++ + .NET runtime + .NET editor).
- `Props.cs` factoring centralizes plugin MSBuild boilerplate in one
  wizard-emitted file.
- The Jawa Toolbox `*Impl` separation pattern (presentation thin SubPanel,
  business in `*Impl`, callbacks registered in `*Impl` ctor) — promote
  this as **the** canonical plugin architecture.

---

## ❓ Open questions for project history

These need someone-who-was-there to answer:

1. **`isSafeToUse`** — code uses `||` (`game.cpp:307`), doc says `&&`.
   Which is correct?

   **Resolved 2026-Q2 (CON-O-01, Phase 2 Plan 02-02 Task 10 KB-05):** D-12
   default-fallback: docs/ai/internals.md:218-231 is the source of truth ("AND ... Both
   must be true"). Changed `game.cpp:307` from `||` to `&&`. Risk: if internals.md is
   wrong and `||` was intentional, `&&` blocks legitimate isSafeToUse=true cases. Live
   SWG verification (Tier-4 manual) will surface any regression.
2. **Was `AddPostDrawLoopCall` ever actually used?** If broken since 2020
   and nobody noticed, the fix is trivial but adds a strong case for
   smoke tests.

   **Resolved 2026-Q2 (CON-O-02, Phase 2 Plan 02-01 Task 2, commit
   9aa0eb9):** Bounded archaeology — grep finds zero callers in Utinni
   and UtinniPlugins as of the fork's current state. However, the method
   is a public API surface for plugin authors (Wave-1 plugins may use it),
   so the fix-it-properly disposition (D-12 default-fallback: assume IS
   used) was taken. The `Drain(ConcurrentQueue<Action>)` helper introduced
   in commit 9aa0eb9 makes the queue-vs-method correspondence explicit
   across `GroundSceneCallbacks`, `GameCallbacks`, and `ObjectCallbacks`
   so this class of bug cannot recur in these files.
3. **The "very odd bug … storing this in a variable prevents corruption"**
   comment in `GameCallbacks.cs:46`, etc. — strongly smells like
   GC-collected delegate being passed to unmanaged without `GCHandle.Alloc`.
   Knowing the original repro would let us confirm the right fix is
   `GCHandle.Alloc`.

   **Resolved 2026-Q2 (CON-O-03, Phase 2 Plan 02-02 Task 9):** Audit of all
   `Add*Callback` delegate-passing sites in `GameCallbacks.cs`, `ObjectCallbacks.cs`,
   and `GroundSceneCallbacks.cs` confirms the existing static-field approach IS a valid
   GC root — no unanchored inline delegates found. The misleading "Very odd bug" comment
   was replaced with a precise CLR P/Invoke delegate-marshalling explanation. A GC-survival
   regression test (`GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV`)
   was added to `UtinniCoreDotNet.Tests`. The correct fix is the static-field approach
   (not `GCHandle.Alloc`) — the static field is the GC root that keeps the delegate alive.
4. **VS 2019 pin** — was there a real reason (compiler bug with x86 + CLR
   hosting?) or just history?

   **Resolved 2026-Q2 (CON-O-04, Phase 2 Plan 02-01 Task 5, commit
   88b5b6b):** Bounded archaeology — no technical rationale found in git
   log, in-tree comments, or upstream `ptklatt/Utinni` history. The pin
   dates to before VS 2022's November-2021 release and was never updated.
   Per D-12 default-fallback (audit-then-widen), the Vsix.csproj
   `Microsoft.VisualStudio.SDK` was bumped 16.0.206 → 17.0.32112.339 and
   `Microsoft.VSSDK.BuildTools` 16.8.3038 → 17.0.5241, and the manifest
   widened to `[16.0,18.0)`. The cross-IDE install confirmation (VS 2019
   AND VS 2022) is the Phase 2 Plan 02-01 Task 6 human-verify checkpoint.
   If a regression is discovered in a specific VS 2022 path, narrow the
   range at that point.
5. **`StdEdited.cs` curation criteria** — what exactly is hand-maintained
   vs auto-generated?

   **Resolved 2026-05-21 (CON-O-05, Phase 3 Plan 03-03 Task 2 — D-24):**
   `StdEdited.cs` is the only hand-curated `Generated/` file. CppSharp's
   STL-template handling for `std::basic_string` is unreliable, so
   `StdEdited.cs` is curated by hand. Plan 03-03's R-F header
   auto-discovery (commit 8aea6af in UtinniCoreDotNetGen/HeaderDiscovery.cs)
   regenerates ONLY `Generated/UtinniCore.cs` — `StdEdited.cs` and
   `Std.cs` (the latter generated by the separate `UtinniCore-Symbols`
   project) are out of scope for the discovery glob. Curation criteria
   for keeping a binding hand-maintained in `StdEdited.cs`: (a) CppSharp
   generates incorrect output for the symbol, OR (b) the symbol name is
   unstable across MSVC versions, OR (c) the binding requires marshaling
   logic CppSharp cannot infer. Disposition documented in the file
   header of `UtinniCoreDotNetGen/HeaderDiscovery.cs` and the matching
   block-comment in `UtinniCoreDotNetGen/Program.cs`.
6. **LeksysINI** — README says "temporary, will most likely be replaced"
   — what was the plan?
7. **Sytner's plugin** — code elsewhere that was never merged, or always
   aspirational?

   **Resolved 2026-05-21 (CON-O-07, Phase 3 Plan 03-02 Task 2 — D-15):** Sytner =
   legacy plugin, no source-compat target preserved. R-B's symmetric `UTINNI_PLUGIN`
   macro (Utinni commit 2884c2c) intentionally breaks at link for any plugin that
   omits the `destroyPlugin` export. PluginManager's loader has a virtual-destructor
   fallback path (`Utinni.LegacyPlugin` fixture exercises it) so a legacy DLL that
   still ships with only `createPlugin` will load and run — it just won't get
   symmetric CRT teardown. Upstream `ptklatt/UtinniPlugins` is dormant; no
   community consumers depend on the old single-symbol ABI.
8. **DXSDK June 2010 dependency** — could it be replaced with Windows 10
   SDK's d3d9 headers? (DXSDK has `d3dx9.h`, Windows SDK lacks it — check
   if Utinni actually uses `d3dx9` math helpers.)
9. **CON-O-09 — Test fixture storage.** In-repo (small files) vs Git LFS (binary TRE
   samples can be large)?
   (Source: docs/ai/test-harness-plan.md — resolved by Phase 4 Plan 04-02.)

   **Resolved 2026-05-23 (CON-O-09, Phase 4 Plan 04-02 — D-03):** Fixture storage =
   **in-repo synthesized + tiny real samples, no LFS**. Synthesized fixtures (hand-
   crafted minimal cases — 3-record TRE v0005, 2-record TRE v0006, synthetic-nested IFF,
   3-object world-snapshot, 4 plugin sub-fixtures) are the primary suite and always-
   run in CI. Small (<128KB each, <256KB cap per file) real samples supplement when
   synthesized-only would miss real-world edge cases. Synthesized-first policy
   sidesteps redistribution-rights questions on SWG game assets; committed real
   samples are format-trivial enough to be defensible as "minimal reproducer"
   rather than "redistributed game content". No `.gitattributes`-LFS pointer files,
   no env-var bring-your-own path, no opt-in 'live-snapshot' tier. If Phase 5
   (Catch2 C++) or Phase 7+ (Wave-1 subpanels) surface needs that synthesized
   fixtures can't satisfy, LFS revisit is a Phase 6+ decision (Phase 4 CONTEXT.md
   "Deferred Ideas").

10. **CON-O-11 — CLI shim distribution.** Public artifact, or test-harness-internal only?
   (Source: docs/ai/test-harness-plan.md — resolved by Phase 4 Plan 04-01.)

   **Resolved 2026-05-23 (CON-O-11, Phase 4 Plan 04-01 — D-02):** CLI distribution
   policy = **public artifact**. `Utinni.Cli/utinni-cli.exe` ships alongside
   `Launcher.exe` in the release bundle. Stable JSON output contract (sorted-key
   indented, LF, UTF-8 no BOM, `schemaVersion: 1` at the envelope root per
   REVIEWS HIGH-6) documented and regression-tested via the Tier-2 golden suite.
   Public surface lets modders + plugin authors consume `parse-tre` /
   `list-objects` / `inspect-iff` / `validate-plugin` as standalone tools
   without injecting SWG. Format change = breaking change = goldens re-baselined
   in the same PR (Phase 4 D-04). Reversal to test-harness-internal remains
   possible in Phase 6+ at lower cost than the opposite direction (Phase 4
   CONTEXT.md "Deferred Ideas").

---

## 📋 Recommended sequencing

If we were doing this work top-to-bottom:

### Week 1 — stop the bleeding (trivial / low-effort criticals)

- C-04 (post-draw queue)
- C-06 (PluginLoader exception swallowing)
- C-08 (Hotkey TryParse)
- C-13 (TJT Debug path)
- C-14 (utinni.cfg login server)
- C-12 (VSIX 16+17)

### Week 2 — durability (single-file criticals)

- C-02 (cross-CRT free)
- C-03 (Network::cast)
- C-05 (drag-drop static-field)
- C-07 (UndoRedo locking)
- C-10 (clr::stop nulls)
- C-11 (DirectX null check)
- C-15 (CppSharp slnDir)

### Week 3 — architectural

- C-01 (DllMain loader-lock) — the riskiest, deserves a focused week with
  a debugger attached.
- C-09 (UI / game thread deadlock candidate)

### Week 4 — leverage

- R-D (Add CI build workflow). Sweep dead code (the ~250 lines listed
  above). Run a C++ formatter once.

### Week 5–6 — strategic reworks

- R-A (symmetric callbacks)
- R-B (plugin lifecycle)
- R-C (single-source RVAs)
- R-E (`[CallerMemberName]` logging)

### Week 7–8 — modernization

- Bump imgui to docking-branch + spdlog 1.14 + ImGuizmo latest.
- R-F (CppSharp header auto-discovery)
- R-G (Directory.Build.props idempotent)
- R-H (snapshot-iteration)
- Decide LeksysINI fate.
- Move templates to SDK-style csproj if we also widen to VS 2022.

### Week 9 — 1.0 cut

- Packaging script
- Release workflow
- Tag 1.0

After this, [the Wave 1 plugins from the vision](vision.md#wave-1--round-out-what-we-have)
become tractable: TRE Browser, IFF Editor, Datatable Editor, String-table
Editor, Object Template Editor.

---

## 📝 Status tracking

When work begins on these items, update the status in this table. Keep this
doc in sync so future sessions (human or AI) can see what's done.

| ID    | Item                                              | Status     | Notes |
| ----- | ------------------------------------------------- | ---------- | ----- |
| C-01  | DllMain loader-lock                               | done       | b2f5c16 — fix(C-01): utinni_init exported + DllMain slim + Launcher second CreateRemoteThread (path a); harness scaffolding: 0d56d93; live-SWG human-verify: pending (Task 3 checkpoint) |
| C-02  | Cross-CRT delete[] in config                      | done       | 8e88879 — Phase 2 Plan 02-02 Task 2 (delete[] removed; TreeFile dtor owns buffer per CON-B-04) |
| C-03  | `Network::cast` returns uninitialized             | done       | 70038a9 — Phase 2 Plan 02-02 Task 3 (networkId=0; OUT param returned; double-semicolon typo removed) |
| C-04  | DequeuePostDrawLoopCalls wrong queue              | done       | 9aa0eb9 — Phase 2 Plan 02-01 Task 2 (Drain helper, closes CON-O-02 per D-12) |
| C-05  | GameDragDropEventHandlers static-field            | done       | 5fd0dac — Phase 2 Plan 02-02 Task 4 (static events + forwarder lambdas; Initialize accepts Panel; 4 tests) |
| C-06  | PluginLoader swallows exceptions                  | done       | efdb80b — Phase 2 Plan 02-01 Task 3 (per-plugin try/catch + LoadErrors surface) |
| C-07  | UndoRedoManager thread-safety + AllowMerge        | done       | 1a8ff42 — Phase 2 Plan 02-02 Task 5 (lock(syncRoot); AllowMerge gate; RedoCommands.Clear ordering TD-29; D-06 testability seam; 5 tests) |
| C-08  | Hotkey.ProcessString throws                       | done       | c6879b5 — Phase 2 Plan 02-01 Task 4 (Enum.TryParse + multi-segment split; both Phase-1 skips removed) |
| C-09  | UI/game thread busy-wait deadlock                 | done       | c3ba6fd — Phase 2 Plan 02-04 Task 1 (getPresentBlockedEvent export + SetEvent in hkPresent + ResetEvent in blockPresent(false); FormMain.WndProc busy-wait replaced with EventWaitHandle.WaitOne(100ms); 3 mock-signaller tests; CON-N-01+CON-N-04 preserved; live SWG verification: Task 2 human-verify pending) |
| C-10  | clr::stop null deref                              | done       | eabc0d2 — Phase 2 Plan 02-02 Task 6 (null-checked Release + nullptr; idempotent double-call harness) |
| C-11  | DirectX9 findPattern no null check                | done       | ba1402a — Phase 2 Plan 02-02 Task 7 (getVtbl null-checks GetModuleHandle + findPattern; log::critical; CON-N-04 preserved; 3 tests) |
| C-12  | VSIX pinned to VS 2019                            | done       | 88b5b6b — Phase 2 Plan 02-01 Task 5 (SDK + BuildTools bumped to 17.x; resolves CON-O-04 per D-12; IDE-install verification = Task 6 human-verify) |
| C-13  | TJT Debug path extra ..\                          | done       | UtinniPlugins@1c1eb0a (cross-repo) — Phase 2 Plan 02-01 Task 8 |
| C-14  | utinni.cfg login.swgemu.com default               | done       | e7c6699 — Phase 2 Plan 02-01 Task 7 (CON-D-01 blank-login default) |
| C-15  | CppSharp slnDir brittle                           | done       | 8a4d7f9 — Phase 2 Plan 02-02 Task 8 (ResolveSlnDir 3-mode function; post-build passes $(SolutionDir); 4 tests) |
| C-16  | GameCallbacks delegate-pinning (CON-O-03)         | done       | bfddf7d — Phase 2 Plan 02-02 Task 9 (comment audit + GC-survival regression test; CON-O-03 resolved) |
| R-A   | Symmetric Add/Remove for callbacks                | done       | Phase 3 Plan 03-01 Tasks 1-3 (b220e36 IN-05 Drain consolidation; 2e1b61d managed-side handle Subscribe/Unsubscribe + Log.cs typo fix; 5e81410 + e4b2b59 native-side handle Subscribe/Unsubscribe across 11 files / 32 registries; ddda9f0 utinni_test_* native bridge + NativeCallbacksHandleTests) |
| R-B   | Plugin lifecycle contract                         | done       | Phase 3 Plan 03-02 Tasks 1-2 (ff0b473 UTINNI_PLUGIN macro extended with destroyPlugin + Utinni.CrtMatchPlugin /MD + Utinni.LegacyPlugin /MT fixture DLLs; 2884c2c PluginManager two-phase init with per-plugin try/catch + HMODULE tracking + LoadLibrary GetLastError logging + destroyPlugin/FreeLibrary shutdown + virtual-dtor legacy fallback; PluginManagerLifecycleTests 5 Facts); paired cross-repo commit UtinniPlugins@73b1856 (TJT destroyPlugin export); CON-O-07 disposition: Sytner = legacy, no compat target |
| R-C   | Single source of truth for RVAs                   | done       | Phase 3 Plan 03-02 Task 3 (9337da7 Client::getSwgWndProc UTINNI_API getter + extern "C" getSwgWndProcExport shim mirrors getSwgHwndExport; PanelGame.cs P/Invoke-caches via Native.GetSwgWndProc; 0x00AA0970 literal eliminated from managed source; GetSwgWndProcTests 2 Facts + negative-grep gate) |
| R-D   | Add CI                                            | done       | Phase 1 Plan 01-02 (2790de4 .github/workflows/ci.yml -- windows-2022 runner builds Utinni.sln /p:Configuration=Release /p:Platform=x86, runs dotnet test, badge in README; D-07/D-08/D-09 dispositions). Phase 1 TEST-01 deliverable. |
| R-E   | Log `[CallerMemberName]`                          | done       | Phase 3 Plan 03-03 Task 1 (cb3f373 -- Log.FormatText runtime stack walk replaced with [CallerMemberName] + [CallerFilePath] defaulted parameters on every public Log method (Info/Debug/Warning/Error/Critical); compile-time resolution, zero runtime cost; LogCallerMemberNameTests 4 Facts + 5-row Theory = 9 tests including negative-grep gate on Log.cs for absence of new StackTrace().GetFrame) |
| R-F   | CppSharp header auto-discovery                    | done       | Phase 3 Plan 03-03 Task 2 (8aea6af -- HeaderDiscovery.Discover(utinniCoreRoot) walks UtinniCore/ recursively; _internal/ filter at any depth, case-insensitive; replaces 23-entry explicit allowlist in UtinniCoreDotNetGen/Program.cs; HeaderDiscoveryTests 6 Facts; CON-O-05 disposition: StdEdited.cs hand-curated, NOT regenerated by glob; Plan 03-02 R-C getSwgWndProc auto-picked-up forward-compat verified) |
| R-G   | Directory.Build.props idempotent                  | done       | Phase 3 Plan 03-03 Task 3 (e8fe682 -- Props.CreateDotNetDirectoryProps rewritten as idempotent XmlReader+XDocument-based merger; preserves user-authored PropertyGroups + non-PropertyGroup siblings (Import/ItemGroup/Target); explicit DtdProcessing=Prohibit + XmlResolver=null for T-03-03-01 XXE safety; CON-T-04 invariant preserved (single method in Props.cs; private UpsertPropertyGroup helper); DirectoryBuildPropsTests 6 Facts) |
| R-H   | SynchronizedCollection snapshot iteration         | done       | Phase 3 Plan 03-01 Tasks 2-3 (2e1b61d managed snapshot dispatch via Dictionary.Values.ToArray() under lock; 5e81410 + e4b2b59 native snapshot via std::vector copy per registry; CallbacksSnapshotIterationTests + NativeCallbacksHandleTests cover Subscribe-during-dispatch + Unsubscribe-during-dispatch invariants) |

When updating: set status to `in progress` while working, `done` when
merged, and add a one-line note (PR link, commit SHA, or "deferred — see
follow-up"). Keep this list compact; once an item is `done`, it can be
deleted on the next cleanup pass.

---

## 📝 Phase 02 code-review findings (02-REVIEW.md)

Findings surfaced by post-Phase-02 code review (`02-REVIEW.md`, 2026-05-17). Tracked separately
from the original assessment C-NN findings because Phase 02 was already closed when the review
ran. Phase 02.1 closes these gaps.

| ID    | Item                                              | Status | Notes |
| ----- | ------------------------------------------------- | ------ | ----- |
| CR-02 | pCast OUT param narrower than SWG write (swgptr* 4-byte vs int64_t 8-byte) | done | 54e0211 — Phase 02.1 Plan 01 Task 1 (pCast typedef widened to int64_t(__thiscall*)(int64_t*, int, int); D-06 option c hybrid; static_assert(sizeof==8) added; setCastForTest/resetCast seam added) |
| CR-03 | UB shift (id >> 32) on 32-bit int in Network::cast | done | 54e0211 — Phase 02.1 Plan 01 Task 1 (Network::cast parameter widened to int64_t; shift is now well-defined; covered by same fix as CR-02) |
| CR-04 | hPresentBlockedEvent TOCTOU lazy-init race        | done   | 408ca22 + 8ea2840 — Phase 02.1 Plan 02 Tasks 1+2: directX::initPresentBlockedEvent() added; getPresentBlockedEvent() simplified to pure reader (no lazy CreateEvent); called from utinni_init before createDetours(). utinni_test_initPresentBlockedEvent() + utinni_test_getPresentBlockedEvent() exports added; FormMainSignallerTests has D-04 regression guard (InitPresentBlockedEvent_AfterEagerInit_ReturnsNonNullHandle). |
| WR-01 | utinni_test_networkCast tautological sentinel stub | done | 7c0aac1 — Phase 02.1 Plan 03 Task 1: testCastDoubleNaked (__declspec(naked) + inline asm) implements __thiscall ABI; writes 0xDEADBEEFCAFEBABE through int64_t* OUT param (ECX); utinni_test_networkCast reseats swg::network::cast via setCastForTest, calls Network::cast through real chain, restores via resetCast; NetworkCastTests asserts full 64-bit sentinel — CR-02 revert (4-byte slot) or CR-03 revert (UB shift) would both fail the assertion. |
| WR-02 | utinni_test_freeConfigBuffer no-op stub           | done (partial-proof) | d74949a — Phase 02.1 Plan 03 Task 2: ConfigBufferFreeTests.cs updated to use Marshal.AllocHGlobal (OS heap / HeapAlloc) instead of GCHandle.Pinned (managed heap). Post-C-02 no-op export proves no crash on OS-heap pointer. PARTIAL-PROOF: Release/MT CRT routes new[]/delete[] through GetProcessHeap() — same as Marshal.AllocHGlobal — so a delete[] re-introduced in Release would NOT crash via this test. Follow-up: Phase 6 STAB-03 to add Utinni.CrossCrtFreeFixture.vcxproj with /MD linkage for full cross-heap proof. |
| WR-03 | depthTexture lazy-init race from render thread    | done   | 408ca22 + 8ea2840 — Phase 02.1 Plan 02 Tasks 1+2: directX::initDepthTexture() added; hkPresent null-branch retained as defensive fallback with log::critical regression warning; called from utinni_init before createDetours(). directX::cleanup() drops delete depthTexture (exit-side UAF fix: OS reclaims on process exit per Win32 DllMain teardown pattern, confirmed live 2026-05-18). utinni_test_initDepthTexture() + utinni_test_getDepthTexturePtr() exports added; FormMainSignallerTests has D-04 regression guard (InitDepthTexture_AfterEagerInit_ReturnsNonNullPointer). |
| WR-05 | GetVtbl negative-only test (no affirmative path)  | done | ca08ff0 — Phase 02.1 Plan 03 Task 3: GetVtbl_WithD3d9Loaded_ReturnsNonZero added to FindPatternHarnessTests.cs; LoadLibraryA(d3d9.dll) + Utinni_GetVtbl() asserts non-zero; written without [Skip] — CI is arbiter; inline comment shows Skip text to add if windows-2022 d3d9.dll pattern is absent. |
| WR-09 | CoInitializeEx unpaired / no COM consumer         | revised — kept-with-rationale | 56725af attempted removal (Phase 02.1 Plan 01 Task 2) but the C-01 live UAT on 2026-05-18 surfaced `ThreadStateException: Current thread must be set to single thread apartment (STA) mode before OLE calls can be made` from `PanelGame.WndProc:42` → `Control.SetAcceptDrops` → `RegisterDragDrop`. RESEARCH §3's grep-based audit scanned only UtinniCore native sources and missed the implicit WinForms drag-drop consumer (documented in `docs/ai/injection.md` line 167). Restored in commit `7dc30bd` with thorough rationale comment: STA apartment IS required for drag-drop; per-thread "leak" is process-lifetime bounded since `clr::load()` blocks until shutdown (conventional Win32 main-thread COM init pattern); OS reclaims on process exit. Tests: Tier-4 manual — "no DragDrop registration dialog on editor startup." |

**Phase 02.1 closure summary:** All 8 findings (CR-02, CR-03, CR-04, WR-01, WR-02, WR-03, WR-05, WR-09) closed in code. Every fix has a regression test that would fail if the fix were reverted (D-04 satisfied), except WR-09 which reverted from "removed" to "kept-with-rationale" after the 2026-05-18 live UAT surfaced the WinForms drag-drop STA dependency. Additionally, a new utinni_test_resolveExports harness (`d13de0a`) was added to close the C-01 export-decoration gap that the LoaderLockHarness missed — see ExportResolutionTests.cs. WR-02 is marked partial-proof in Release/MT (see notes); full proof deferred to Phase 6 STAB-03. WR-09's "research grep scope was too narrow" failure-mode is logged as a process-improvement for future phases — managed-side consumers must be in scope when researching native lifecycle changes.

---

## See also

- [Vision](vision.md) — the one-stop-shop modding-tool direction this
  assessment serves.
- [Architecture](architecture.md) — how the framework is shaped today.
- [Internals](internals.md) — the RVAs and hooks that some critical items
  touch.
