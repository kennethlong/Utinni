# CODEX Consult: scene-change AV at SWG `0x0051fb0a` after Phase 3 R-A in `ground_scene.cpp`

> Paste this into CODEX with the same project root (`D:\Code\Utinni`). CODEX can read files directly — paths below are relative to that root.

## What you're being asked

A bug bisect (11 cycles) has isolated a 100%-reproducible AV to one file pair: `UtinniCore/swg/scene/ground_scene.{cpp,h}` at master HEAD. Reverting just those two files to their pre-Phase-3 baseline (commit `2523228`) eliminates the crash. The change introduced 4 modified registries (subscribe/unsubscribe/snapshot dispatch + per-registry `std::mutex`).

I want your independent read on:

1. **Most likely root cause.** Given the diff in `.planning/debug/codex-consult-ground-scene-diff.patch` (and `git show master:UtinniCore/swg/scene/ground_scene.cpp`), what's the most likely mechanism by which the new code crashes SWG's own `GroundScene::ctor` deep into scene init?
2. **Suspect commit within the 4-commit window.** Of the 4 commits since baseline (`5e81410` R-A+R-H, `427f474` CR-01 mutex, `9626174` WR-01 atomic counter, `c1681bd` WR-04 skip-zero), which is the likely cause and why?
3. **Minimal fix design.** I want to preserve the R-A subscribe/unsubscribe handle API, the per-registry mutex (CR-01), and the R-H snapshot dispatch pattern. What's the narrowest fix that keeps all three intact and unblocks scene change? If a constraint is unsalvageable, say so explicitly.

Be terse. I want analysis, not narration. If you need to assume something, state it as an assumption.

## Project tooling context (terse)

- Utinni is a C++ DLL (`UtinniCore.dll`) injected into SWGEmu's `SWGEmu.exe` (a Star Wars Galaxies private-server client). It detours SWG functions at hardcoded RVAs and adds plugin/callback infrastructure (`UtinniCoreDotNet.dll`, MEF-loaded managed plugins like `TheJawaToolbox`).
- Detour library: `external/DetourXS/` — patches SWG code with `DETOUR_TYPE_PUSH_RET` (6-byte `push abs; ret` trampolines). Recently fixed: explicit detour-length trap (2026-05-19), so detour install is trustworthy.
- Toolchain: VS 2026 MSBuild, PlatformToolset v142 (so MSVC compiler v14.29 baseline). Release/x86. No /sdl, no /GS-, default exception model.
- `GroundScene` mirrors the SWG client's C++ class layout (member offsets match SWG's). Static member functions don't change object layout / vtable.
- Phase 3 R-A goal: convert ~32 native callback registries from `std::vector<fn_ptr>` (no remove API, order-leaky, no concurrency story) to `std::unordered_map<int, fn_ptr>` with handle-based Subscribe/Unsubscribe + per-registry `std::mutex` + R-H snapshot dispatch pattern.

## The crash

Faulting instruction at SWG `0x0051fb0a` (inside `GroundScene::ctor` at base `0x00519830` + `0x22da`, deep into scene init — well past the ctor entry). Exception code `c0000005` (ACCESS_VIOLATION). Address is consistent across multiple maps (Talus → Naboo, Naboo → Talus) and across the two AV minidumps captured this session (`SWGEmu.exe-stage.119798-20260522144001.{txt,mdmp}` from Cycle 10).

Triggers on **scene CHANGE**, not first scene load. First login completes fine; the first `/warp talus` or `/warp naboo` command via TJT's chat command parser crashes the new scene's ctor within ~5-10 frames (`MainLoop=1216`, `UpTime=57s`).

Pre-Phase-3 worked: `SESSION-HANDOFF-2026-05-21.md:126` records "Scene transitions (3 cycles) ✅" before Phase 3 landed.

A separate piece of forensic evidence from earlier in the investigation: VEH caught int3 at `EIP=0x00AA1E3F` between SWG WndProc (`0x00AA0970`) and PersistentCrcString::ctor (`0x00AA4050`) — i.e., on inter-function alignment padding. Byte pattern there: `16 CE 87 00 [CC] E8 DB 04 00 00 6A 28 E8 04 71 FE`. I read that as: an indirect call dispatched to alignment padding (vtable/fn-ptr corruption signature). The AV at `0x0051fb0a` may be the actual call site that does the indirect dispatch.

## Bisect history (what's ruled out)

| Cycle | Hypothesis tested | Result |
|---|---|---|
| 1 | CR-01 mutex on creature_object alone (`427f474` partial) | REFUTED |
| 2 | R-C WndProc P/Invoke (`9337da7`) | REFUTED |
| 3 | Pre-Phase-3 baseline | UNSMOKEABLE (no TJT = no /warp command path) |
| 4 | R-B init + CR-02 reject (CODEX's top pick at the time) | REFUTED |
| 5b | R-A native cluster (`5e81410` + `e4b2b59` + `ddda9f0`) | CLEAN |
| 6 | R-A Task 3b alone (`e4b2b59`) | REFUTED |
| 7 | R-A Task 3a alone (`5e81410`) | CLEAN — narrowed to Task 3a |
| 8 | Task-3a `game.{cpp,h}` alone | REFUTED — bug is NOT in game |
| 9 | Task-3a `creature_object` + `ground_scene` (binary split) | CLEAN — narrowed to one of those two |
| 10 | Only `creature_object.{cpp,h}` reverted | REFUTED (CRASHED) — `creature_object` alone is NOT the bug |
| 11 | Only `ground_scene.{cpp,h}` reverted | CLEAN — **`ground_scene` is the bug** |

Conclusion: the bug is necessary-and-sufficient in `UtinniCore/swg/scene/ground_scene.{cpp,h}`. Not in game.cpp, creature_object, graphics, post_processing, depth_texture, shader, cui_*, log, imgui_impl, plugin lifecycle, WndProc, or any managed-side code. ALL of those were either tested at master HEAD with ground_scene reverted (Cycle 11) or otherwise eliminated.

## The diff under scrutiny

Full diff in `.planning/debug/codex-consult-ground-scene-diff.patch` (305 lines, +200 / -27 across the two files). Per-commit attribution (chronological):

```
5e81410  feat(03-01): R-A + R-H native-side game/scene/object/graphics (Task 3a)
  -- ground_scene.cpp +123 / -16, ground_scene.h +12
  -- vector<fn_ptr> → unordered_map<int, fn_ptr>; Subscribe/Unsubscribe/snapshot dispatch
  -- NO mutex yet in this commit

427f474  fix(03-review-cr-01): add per-registry std::mutex to native callback layer
  -- ground_scene.cpp +43 / -15
  -- adds 4 per-registry std::mutex statics + lock_guard around Subscribe/Unsubscribe and around snapshot build
  -- the mutex is dropped before invoking callbacks (so callbacks can re-subscribe without deadlock; new subs fire on NEXT dispatch)

9626174  fix(03-review-wr-01): atomic<int> s_ioEventLogCount + document _ReturnAddress
  -- ground_scene.cpp +28 / -11
  -- promotes the diag input-event counter from plain int to std::atomic<int> with fetch_add
  -- ONLY touches hkHandleInputEvent's diag logging block; does NOT touch any registry / dispatch / draw / update code

c1681bd  fix(03-review-wr-04): skip-zero handle overflow guard across all registries
  -- ground_scene.cpp +4
  -- if (id == 0) { id = s_next++; } skip in 4 Subscribe* bodies
  -- only fires after 2^31 subscribes in a single session (never in practice)
```

My ranked priors:

1. **`5e81410` storage swap** — biggest surface area, weakest formal verification, runtime impact on render hot path
2. **`427f474` per-registry mutex** — adds lock acquisition in hot path; lock-order issues during scene-cleanup-then-setup could matter
3. **`9626174` atomic counter** — code path unrelated to scene change
4. **`c1681bd` skip-zero** — unreachable in this repro

But this is exactly where I want your read, not mine.

## Hot path analysis (where the new code runs)

The Phase 3 R-A code in `ground_scene.cpp` at master HEAD changes runtime behavior at these sites:

1. **`hkDrawLoop`** (detoured on SWG `swg::groundScene::draw` at `0x0051B770`) — fires every frame. Stack-allocates `std::vector<fn_ptr> snapshot`, locks `preDrawLoopCallbacksMutex`, `snapshot.reserve(size)` (heap alloc if size > 0), copies values, unlocks, iterates snapshot, calls original `swg::groundScene::draw(pThis)`. Then same for postDraw with `postDrawLoopCallbacksMutex`.
2. **`hkUpdateLoop`** (detoured on SWG `swg::groundScene::update` at `0x0051AF10`) — fires every frame. Same snapshot+lock pattern as hkDrawLoop, then calls original.
3. **`hkHandleInputEvent`** (detoured on SWG `handleInputMapEvent` at `0x0051AA40`) — fires per IO event. Now has WR-01 atomic counter for diag logging; does NOT touch registries.
4. **`toggleFreeCamera`** — only fires on user toggle (NOT scene-change-driven). Snapshot+iterate `cameraChangeCallbacks` after switching camera mode.

**The only registry subscriber at runtime is the managed bridge** (`UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:Initialize()`):
- `AddPreDrawLoopCallback(DequeuePreDrawLoopCalls)` — single entry
- `AddPostDrawLoopCallback(DequeuePostDrawLoopCalls)` — single entry
- `AddUpdateLoopCallback(DequeueUpdateLoopCalls)` — single entry
- `AddCameraChangeCallback(CallCameraChangeCallbacks)` — single entry

So insertion-order vs hash-bucket-order is moot (one entry per registry). The managed bridge functions just drain a managed `ConcurrentQueue<Action>` — the queues are populated by TJT etc. but the dispatch mechanism is unchanged between pre- and post-Phase-3 in terms of WHAT runs.

What did change: per-frame hot path now allocates a `std::vector` on the stack, reserves heap, takes/releases a mutex, copies one fn-ptr, deallocates. Pre-Phase-3 just iterated a static vector with one element inline. Compiler-generated code size for `hkDrawLoop` / `hkUpdateLoop` grew (each is now ~3x the byte length).

## Key constraints on the fix

1. **R-A handle-based Subscribe/Unsubscribe** is the new primary API (`subscribe*Callback(fn) -> int`, `unsubscribe*Callback(int) -> bool`, handle 0 reserved invalid). Must be preserved.
2. **Per-registry `std::mutex`** (CR-01) protects against concurrent Subscribe/Unsubscribe — must be preserved for thread-safety.
3. **R-H snapshot dispatch** — must be preserved so callbacks can re-Subscribe without invalidating the iterator.
4. **Legacy `add*Callback` API** — must keep working (TJT, Sytner, etc. plugin DLLs are pre-built and call these).
5. **No reverting** — `git checkout 2523228 -- ground_scene.{cpp,h}` works but gives up the R-A surface for these 4 registries permanently.
6. **No source modification of SWG client** — Utinni is read-only on SWG's binary.

## Possible directions I want you to weigh (not exhaustive)

- **Iteration-order divergence.** Switching `std::vector` (insertion order) → `std::unordered_map` (bucket order) changes iteration order. But there's only one entry per registry, so this seems unlikely to be causal here. Unless I'm wrong about who subscribes — please verify by grepping `D:/Code/UtinniPlugins/` if needed.
- **Hot-path heap allocation.** `snapshot.reserve(N)` on every frame is a heap allocation. If SWG's allocator is custom or fragile during scene init, this could matter. But it'd fire every frame, not only on scene change.
- **Lock acquisition during a `ctor`-internal callback dispatch.** Does SWG call `update` or `draw` from inside `GroundScene::ctor`? If so, our hook fires DURING ctor. The hook now locks a mutex; if ctor recursively calls something that wants the same lock, we deadlock — but we'd see a hang, not an AV. (Unless deadlock detection in MSVC throws, propagating an exception into SWG code that doesn't have unwind info.)
- **Stale function pointer in the registry.** The managed bridge's delegate is GC-rooted (static field) per `C-16`. So the fn-ptr should remain valid. But could the trampoline / GCHandle layout differ between scene-cleanup and re-init? Could the COM/MEF unload of TheJawaToolbox during scene change invalidate the managed delegate's stub?
- **Code-size / TLS layout change.** Adding ~500 bytes of static data + ~3x function body bytes shifts other symbols. If anything in UtinniCore.dll depends on a fixed RVA / TLS slot index, the shift breaks it. I don't think we have such dependencies but worth a sanity check.
- **MSVC `std::mutex` / `std::unordered_map` interaction with the detour trampoline.** DetourXS rewrites the first 6 bytes of SWG's `draw`/`update`/`handleInputMapEvent` with `push abs; ret`. If the new hook function's body exceeds some I-cache or unwind-info constraint, behavior could differ. Unlikely but possible.
- **C++ exception escape.** `std::unordered_map::operator[]` and `std::vector::push_back` can throw on alloc failure. If our hook throws, the exception propagates through SWG C-style code that doesn't unwind. But SWG would crash with `Exception E06D7363` (SEH for C++ exceptions) or unwind-related signature, not `c0000005`. Probably not the cause.

## What I want from you

Three things, in order:

1. **Read the diff and the master HEAD `ground_scene.cpp` carefully.** Spend the budget you'd normally spend on a 305-line patch review. If there's something I missed (off-by-one, lifetime hole, calling-convention mismatch, allocator interaction), flag it.
2. **Pick a most-likely-cause hypothesis** with reasoning. If you want to suggest a specific Cycle-12 bisect cut to confirm before fixing, do — I'll run it.
3. **Sketch the narrowest fix.** Code-level if you have a strong opinion, prose-level if you want me to make the call. Constraint: preserve R-A API + CR-01 mutex + R-H snapshot.

Files to read:
- `UtinniCore/swg/scene/ground_scene.cpp` (master HEAD — the buggy version)
- `UtinniCore/swg/scene/ground_scene.h` (master HEAD)
- `.planning/debug/codex-consult-ground-scene-diff.patch` (305-line diff vs `2523228` baseline)
- `.planning/debug/03-scene-change-av-0x0051fb0a.md` (full 11-cycle bisect log)
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (managed bridge — only subscriber)
- `external/DetourXS/detourxs.{cpp,h}` (detour mechanism)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/FreeCamImpl.cs` (TJT's only GroundScene callback subscriber — `AddCameraChangeCallback`)
- (optional) `git show 5e81410 -- UtinniCore/swg/scene/ground_scene.cpp` for the just-R-A version
- (optional) `git show 427f474 -- UtinniCore/swg/scene/ground_scene.cpp` for the mutex-add version
