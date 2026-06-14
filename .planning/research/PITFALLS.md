# Pitfalls Research

**Domain:** Injected-DLL SWG modding tool (x86 in-process UtinniCore + .NET WinForms host + CppSharp CLR bridge + MEF plugins) — adding a Terrain `.trn` editor, one Effects-family editor, a parallel **D3D11 render-path foundation**, and **v145-toolset/CppSharp-bump completion** to a shipped v2.0 system.
**Researched:** 2026-06-14
**Confidence:** HIGH for the render-path + CppSharp/ABI pitfalls (grounded in this project's own captured incidents and direct inspection of `UtinniCore/swg/graphics/directx9.cpp` + `swg-client-v2`'s D3D11 source); MEDIUM for the `.trn`/effects-codec pitfalls (format-shape verified against `swg-client-v2/clientTerrain`, but no v2.1 fixtures exist yet).

> **Scope note.** These are pitfalls of adding *these* features to *this* injected/WinForms/CppSharp system. Generic "test your code" advice is omitted. Every critical pitfall below is anchored to either a captured Utinni incident (auto-memory) or a concrete fact read out of the live source this milestone touches.

---

## Ground-truth facts this milestone is built on (read, not assumed)

These shape the pitfalls; stated up front so phase planners can rely on them.

1. **The SWG renderer is a swappable DLL chosen by config, not a fixed import.** `clientGraphics/Graphics.cpp` loads `.\\gl%02d_r.dll` where `%02d` = `ConfigClientGraphics::getRasterMajor()`: **5–7 → D3D9** (`gl05_r.dll`…), **11 → D3D11** (`gl11_r.dll`), via `GetProcAddress(ms_dll, "GetApi")`. So the *backend is selectable at launch* and the hook-target module name itself changes. (Verified: `Graphics.cpp:195-253`.)
2. **SWG D3D11 uses a DXGI flip-model swapchain.** `Direct3d11_Device.cpp`: `D3D11CreateDevice` (separate device) then `IDXGIFactory2::CreateSwapChainForHwnd` with `DXGI_SWAP_EFFECT_FLIP_DISCARD`, `DXGI_SCALING_STRETCH`, `BufferCount=2`, `BufferUsage=RENDER_TARGET_OUTPUT` only, `SampleDesc.Count=1` (no MSAA), `Flags=0`. The device comment block states: *"DXGI flip model has no lost-device concept; DEVICE_REMOVED from Present() is a process-restart class event"* and *"flip-model unbinds RTV after Present; rebind every frame."* (Verified: `Direct3d11_Device.cpp:14-20,428-577,892`.)
3. **Utinni's D3D9 hook installs by harvesting a live OS-d3d9.dll vtable from a throwaway HAL device, then patches function bodies in `d3d9.dll`'s `.text`** — it does NOT pattern-scan and does NOT mutate vtables (rewritten 2026-05-19; `directx9.cpp:435-526`). It hooks 7 methods (BeginScene/EndScene/Present/Reset/DrawIndexedPrimitive/SetRenderTarget/SetDepthStencilSurface) + a fragile `s207_r.dll` shader-compile override.
4. **Utinni deliberately never `Reset`s the third-party device and never resizes its backbuffer**; the overlay is mapped in render-target space and the reparented window is stretched by windowed Present. (`feedback_d3d9_reset_third_party`, `feedback_imgui_embedded_d3d9_rt_space`.)
5. **`Generated/UtinniCore.cs` is CppSharp output that reorders nondeterministically every build** (symmetric ~11k-line no-op diff). The current v145 build only works because the vendored CppSharp clang-11 parser is *redirected at VS2019 14.29 STL* — a pin that breaks the moment Utinni C++ uses a C++23 STL header. (`project_utinnicore_cs_regen_churn`, `project_vs2026_cppsharp_block`.)

---

## Critical Pitfalls

### Pitfall 1: Treating the D3D11 path as "swap `imgui_impl_dx9` for `imgui_impl_dx11`" — when the *hook acquisition* model is fundamentally different

**What goes wrong:**
The D3D11 overlay is built by mirroring `directx9.cpp` one-for-one, but rendering never appears (or installs zero hooks) because the D3D9 acquisition trick doesn't transfer. The D3D9 path harvests a vtable from a *throwaway HAL device created against the OS `d3d9.dll`* and patches function bodies in `d3d9.dll`'s shared `.text` — every D3D9 device in the process shares those bodies. **D3D11/DXGI has no equivalent shared `.text` to patch for `Present`:** `IDXGISwapChain::Present` and `ID3D11DeviceContext` methods are COM vtable entries on per-instance objects, and the swapchain lives in `gl11_r.dll`'s world via `dxgi.dll`. The correct target is the **DXGI swapchain vtable** (`IDXGISwapChain::Present` index 8, `ResizeBuffers` index 13) — reached by creating a throwaway `D3D11CreateDeviceAndSwapChain`, reading its swapchain vtable, and hooking that. Hooking `D3D11CreateDevice` itself is too early (you get the device before the swapchain exists).

**Why it happens:**
`directx9.cpp` is a clean, greppable template (the detour-table pattern, CON-N-01), and the obvious move is "do the same for 11." But the D3D9 implementation's key insight — patch shared `.text`, not vtable (`directx9.cpp:426-434`) — is a D3D9-on-modern-Windows-specific fact. D3D11's analog is the opposite: you *must* hook the per-object vtable.

**How to avoid:**
- Build a `directx11.cpp` whose acquisition is `D3D11CreateDeviceAndSwapChain` (throwaway, 1×1 hidden window like the existing `getVtbl()` dummy window) → read `IDXGISwapChain` vtable → hook entry 8 (`Present`) and entry 13 (`ResizeBuffers`). Apply the same `Detour::CheckPointer` null-guard discipline the 7 D3D9 detours use (CON-H-02).
- Use ImGui's `imgui_impl_dx11` backend, initialized from the *device + context obtained inside the first hooked Present* (`swapChain->GetDevice` + `GetImmediateContext`), never from your throwaway device.
- **Rebind the render target every frame before drawing the overlay** — the SWG D3D11 device explicitly unbinds the RTV after Present (flip model). `imgui_impl_dx11` assumes a bound RTV; under flip-discard you must `OMSetRenderTargets(backbufferRTV)` yourself in the hook before `ImGui_ImplDX11_RenderDrawData`. (Verified from `Direct3d11_Device.cpp:892`.)

**Warning signs:**
Zero hooks install (mirrors the 2026-05-18 D3D9 black-screen, see `feedback_d3d9_hook_diagnosis`); or hooks fire but ImGui draws nothing / draws one frame then vanishes (RTV-unbind symptom); or overlay renders only when a specific menu is open (you accidentally hooked `ID3D11DeviceContext` rather than the swapchain).

**Phase to address:**
D3D11 render-path foundation phase (early — "foundation-before-features"). The acquisition spike is the first plan in that phase.

---

### Pitfall 2: Running two render hooks live at once and corrupting state — instead of detecting the backend and installing exactly one

**What goes wrong:**
Both `directx9.cpp::detour()` and `directx11.cpp::detour()` are wired into `utinni_init`/`graphics::install` unconditionally. On a D3D9 client, the D3D11 path's throwaway `D3D11CreateDeviceAndSwapChain` still succeeds (D3D11 runtime is present on the OS), installs a swapchain `Present` hook that never fires (SWG isn't presenting through DXGI) — *but* the dummy-device creation, the extra hidden window, and any DXGI factory leak add startup cost and a second set of global hook state. Worse: if both `imgui_impl_dx9` and `imgui_impl_dx11` initialize, two ImGui contexts fight over `io`, input double-counts, and the wireframe/depth-texture/`blockPresent` machinery (all D3D9-specific in `directx9.cpp`) runs against a backend that isn't live.

**Why it happens:**
The render backend is *not* knowable from a compile-time `#ifdef` — it's `rasterMajor` in the SWG client's config, resolved at SWG launch (Fact 1). Developers default to "install both, let the dead one no-op," which is how D3D9-only frameworks bolt on D3D11.

**How to avoid:**
- **Detect first, install one.** At `graphics::install` time, check which `gl%02d_r.dll` SWG loaded: `GetModuleHandleA("gl11_r.dll") != nullptr` → D3D11; else D3D9 (`gl05/06/07_r.dll`). Drive a single `directX::detour()` *or* `directX11::detour()` off that. This is the explicit open question flagged in `project_d3d11_migration` ("concurrent paths vs clean swap").
- Confirm via the cheap diagnostic *before* theorizing (the `feedback_d3d9_hook_diagnosis` discipline): a one-shot log at install printing the detected backend + the loaded `gl*_r.dll` name. 30-second ground truth beats a multi-day "why is input doubled" hunt.
- Keep the D3D9-specific subsystems (depth texture, wireframe toggle, `blockPresent` minimize workaround) gated behind the D3D9 branch — do not let them run under D3D11. Their D3D11 equivalents (if needed) are separate work, not a copy-paste.

**Warning signs:**
Doubled mouse/keyboard input; ImGui asserts on a second `CreateContext`; startup slower by one device-create; `hkPresent` (D3D9) one-shot log AND a DXGI Present log both firing in the same session; a DXGI factory/device leak reported at exit.

**Phase to address:**
D3D11 render-path foundation phase — the backend-detection switch is the *first* deliverable, before either overlay is fleshed out.

---

### Pitfall 3: Porting the D3D9 "never Reset / stretch the window / map in RT-space" model verbatim to DXGI — where the resize semantics are inverted

**What goes wrong:**
The team carries forward the hard-won D3D9 rule ("never `Reset` a third-party device; let windowed Present stretch; map ImGui in render-target space" — `feedback_d3d9_reset_third_party`, `feedback_imgui_embedded_d3d9_rt_space`) into the D3D11 path and gets a stretched, blurry, or mis-hit-tested overlay — or a crash on window resize. Under DXGI flip-model the rules are different:
- There **is no `Reset`**; resize is `IDXGISwapChain::ResizeBuffers`, and the SWG device *does* call it on `displayModeChanged` (`Direct3d11_Device.cpp:15`). An overlay that caches the backbuffer RTV must release/recreate it inside the `ResizeBuffers` hook or it holds a stale buffer and `ResizeBuffers` fails with `DXGI_ERROR_INVALID_CALL` (outstanding references) — the DXGI analog of the D3D9 Reset-crash, and it will look identical from the outside.
- The swapchain is `DXGI_SCALING_STRETCH` (`Direct3d11_Device.cpp:569`), so the backbuffer-vs-window stretch the D3D9 path leaned on *does* exist — but the cursor-clip dead-zone (`project_swg_cursor_clip_deadzone`, SWG's own `ClipCursor` to backbuffer rect) may behave differently because the D3D11 backbuffer is sized from `CreateSwapChainForHwnd(hwnd, width, height)`, not a fixed 1280×1024.

**Why it happens:**
The D3D9 lessons are correct, load-bearing, and recent — so they feel universal. They are D3D9-and-windowed-Present-specific. The flip-model has *its own* "release all references before resize" rule that is structurally similar (you can't resize while you hold buffers) but reached through a different API and a different failure code.

**How to avoid:**
- Hook `ResizeBuffers`: on entry, release your cached backbuffer RTV (and any ImGui RTV); call the original; re-acquire `GetBuffer(0)` + `CreateRenderTargetView`. This is the flip-model equivalent of `hkReset`'s `InvalidateDeviceObjects`/`CreateDeviceObjects` bracket already in `directx9.cpp:365-378`.
- **Do not** attempt to resize SWG's swapchain yourself (the DXGI analog of the forbidden D3D9 `Reset`). Let SWG drive `ResizeBuffers`; you only react.
- Re-validate the cursor-clip dead-zone empirically under D3D11 before assuming the D3D9 finding carries — run the one-shot `render()` diag pattern from `project_swg_cursor_clip_deadzone` against the D3D11 backbuffer dims.

**Warning signs:**
Crash or `DXGI_ERROR_INVALID_CALL` on SWG window maximize/restore/resolution-change; overlay frozen at old size after resize; ImGui drawing to a released RTV (debug layer screams "deleted resource").

**Phase to address:**
D3D11 render-path foundation phase, immediately after Pitfall-1 acquisition lands (resize handling is part of a *working* foundation, not polish).

---

### Pitfall 4: A CppSharp version bump silently changing generated public C# signatures and detonating every pre-built plugin DLL at MEF compose

**What goes wrong:**
The v145/CppSharp-upgrade phase regenerates `UtinniCoreDotNet/Generated/UtinniCore.cs` with a *newer* CppSharp. A different generator version can rename, reorder-overload, change marshalling of, add/remove parameters from, or re-type public methods the bridge exposes. Source in *this* repo rebuilds fine — but every **pre-built plugin DLL** in `kennethlong/UtinniPlugins` (TJT, Sytner) was compiled against the old IL signatures. At inject time MEF tries to compose them, `Log.Info(System.String)` (or any bridged symbol) no longer matches, and the plugin throws `MissingMethodException`/`CompositionException` and silently fails to activate. This is the *exact* failure mode already lived in Phase 3 (`feedback_caller_attrs_binary_compat`) — there it was a hand-added `[CallerMemberName]`; here CppSharp does it wholesale and invisibly.

**Why it happens:**
CppSharp output is treated as "regenerated, so it's fine" — but the generated file *is* the public ABI surface the plugins bind to. The churn-discard habit (`git checkout -- Generated/UtinniCore.cs`, `project_utinnicore_cs_regen_churn`) trains the team to ignore diffs in that file as noise — so a *real* ABI change hides inside what looks like the usual reorder-only churn.

**How to avoid:**
- **Distinguish real ABI change from reorder churn before trusting the regen.** The `project_vs2026_cppsharp_block` resolution already used a *per-type block-hash check* (cursor confirmed 119/119 partial-class blocks byte-identical, only positions moved). Make that a gate in this phase: after the CppSharp bump, hash each partial-class/method block; if any block hash changes (not just order), it's an ABI event, not churn.
- Treat any signature delta as a deliberate **ABI bump**: rebuild *both* `UtinniPlugins` (TJT and Sytner) in the same milestone, or provide binary-compat shims. (Standing cross-repo authority exists per the UtinniPlugins write-authority memory — use it; the rebuild is paired, not a separate checkpoint.)
- Build the missing regression harness flagged in `feedback_caller_attrs_binary_compat`: a **frozen plugin DLL fixture** compiled once against the old bridge, asserted to still MEF-compose against the freshly regenerated `UtinniCoreDotNet.dll`. None exists today; this phase is the right time since it's the phase that risks the break.
- Live-smoke the inject after the bump (Tier-4) — MEF compose failures only surface at inject, never in `dotnet test`.

**Warning signs:**
`CompositionException`/`MissingMethodException` at inject; a plugin panel silently absent in TJT; the per-block hash check reports >0 changed blocks; the `Generated/UtinniCore.cs` diff is *not* symmetric (insertions ≠ deletions, or content differs beyond ordering).

**Phase to address:**
v145-toolset / CppSharp-upgrade phase. This is the headline risk of that phase and should be its primary acceptance gate.

---

### Pitfall 5: The CppSharp/clang upgrade not actually reaching v145 — repeating the Path-2 dead-end

**What goes wrong:**
The phase "upgrades CppSharp to retire the 14.29-STL parser redirect," picks the next CppSharp release, migrates the generator's TFM (net4.7.2 → net9/10), burns a week — and discovers the new CppSharp's bundled clang *still* can't parse MSVC 14.5x's C++23 STL, so the redirect can't be removed. This is documented Path-2 in `project_vs2026_cppsharp_block`: CppSharp v1.2 ships clang 19, which only unlocks MSVC 14.4x (v143) — **no CppSharp release ships a clang new enough for v145's STL** (as of that research). The team ends up where it started, plus a TFM migration to maintain.

**Why it happens:**
"Bump CppSharp" sounds like a version-number change. The real constraint is the clang-version ↔ MSVC-STL pairing, and the binding is set by whatever clang CppSharp vendors — not by CppSharp's own version. The current redirect works *precisely because* clang-11 is pinned to its contemporaneous 14.29 STL (`yvals_core.h` literally guards `#if __clang_major__ < 11`).

**How to avoid:**
- **Pin down the clang version inside the candidate CppSharp release before committing the phase** — confirm it can parse a 14.5x STL header sample (`<vector>`, `<tuple>`, `<__msvc_string_view.hpp>`) in a throwaway spike. If no shipping CppSharp clears v145's STL, the honest outcome is: keep the redirect, document it as the supported config, and scope the phase to *hardening the redirect* (the C++23-header tripwire below) rather than removing it.
- Add a **C++23-STL-header tripwire**: a grep/CI check that fails if UtinniCore C++ starts `#include`-ing `<format>`/`<ranges>`/`<concepts>`/`<expected>`/`<span>`/`<chrono>` etc. (the headers absent from 14.29 that break the parser pin — enumerated in `project_vs2026_cppsharp_block`). This protects the redirect whether or not the bump succeeds.
- If the TFM migration (net4.7.2 → net9/10 for `UtinniCoreDotNetGen`) does proceed, keep it isolated to the *generator* tool — it must not pull the injected x86 net472 bridge off its TFM (the bridge runs in SWG's 32-bit address space; CON-P-02).

**Warning signs:**
The redirect-removal spike still hits `CppSharp has encountered an error while parsing code` / MSB3077; the candidate CppSharp's `lib/clang/<N>` is < the clang version that supports 14.5x; the TFM migration starts touching `UtinniCoreDotNet.csproj` (bridge) rather than only `UtinniCoreDotNetGen.csproj` (generator).

**Phase to address:**
v145-toolset / CppSharp-upgrade phase — make the clang-capability spike the *first* task so the phase can re-scope to "harden redirect" before sinking days into a TFM migration that doesn't unblock removal.

---

### Pitfall 6: Per-frame heap allocation in the new editors' live-preview / callback dispatch — re-triggering the scene-change allocator crash

**What goes wrong:**
The Terrain or Effects editor adds a live-preview hook that, per frame, allocates (a `std::vector` of changed chunks, a `new` snapshot, a `std::string` format) on SWG's render/update hot path. This fragments SWG's CRT heap and reproduces the `0x0051fb0a` `GroundScene::ctor` access violation on scene change — the exact class of bug that cost 11 bisect cycles + a CODEX consult in Phase 3 (`project_rh_snapshot_no_heap_alloc`). Terrain is *especially* exposed: terrain regenerates on scene/zone change, so a terrain-preview callback fires precisely during the fragile `GroundScene` construction window.

**Why it happens:**
Editor code is written in a "normal" allocation style; the hot-path constraint (CON-H-04 dispatch safety + the heap-free rule) is a native-render-thread fact that managed/editor authors don't carry. The callback-dispatch helper pattern (`dispatchSnapshot`, stack-allocated `kInlineCap=16`) lives in the R-A files but isn't obviously the law for *new* preview hooks.

**How to avoid:**
- Any new per-frame native callback (terrain-preview, effect-preview) must use the established stack-allocated fixed-size snapshot dispatch (`dispatchSnapshot` in `ground_scene.cpp`), never per-frame `std::vector::reserve`/`new`/`std::string`. CON-H-04 (snapshot-under-lock) and the heap-free rule both apply.
- Prefer to push preview *data* across the boundary once (on edit), not allocate per frame. The save-tier-first candor from RESID-03/SC3 ("live render deferred") is the safe default: if live preview can't be done heap-free, ship save-then-reload preview rather than a crashing live hook.
- Re-test scene change via the TJT chat-command path after wiring any preview (the only repro path — `project_scene_change_via_tjt`); "naked but in world" after a TJT warp is the success baseline (`project_tjt_scene_change_naked_baseline`), a crash at `0x0051fb0a`-class addresses is the regression.

**Warning signs:**
AV inside SWG-internal code Utinni doesn't detour (corruption upstream, symptom downstream); crash reproducible only on scene/zone change, not in a static scene; VEH int3 / `0xCC` landing addresses.

**Phase to address:**
Both editor phases (Terrain, Effects) — as an explicit hot-path constraint in any live-preview plan. The D3D11 foundation phase should also re-state it for the new Present hook.

---

### Pitfall 7: A `.trn` / effects codec that aborts (or silently truncates) on the multi-chunk / version variants it didn't fixture — the OT and TRE history repeating

**What goes wrong:**
The Terrain `.trn` codec (and the Effects `.iff` codec) is written against one sample file and aborts, mis-pads, or silently drops data on the variants it never saw. This project has hit this **three times**: OT multi-chunk list-params aborted 17% of templates (`project_ot_multichunk_list_params`); the IFF reader consumed a phantom pad byte because real SWG datatable chunks aren't word-padded (`project_swg_iff_no_pad`); the TRE reader needed both SWGEmu 0004/0005/0006 *and* Restoration 5000/6000 versions because the user mods both clients (`project_tre_version_support_gap`). `.trn` is a deep, recursively-nested layer/affector/boundary/filter tree (terrain procedural generation) — far more variant-rich than a flat datatable — so the abort-on-unknown risk is higher, not lower.

**Why it happens:**
A codec built from one fixture encodes that fixture's assumptions (padding, chunk multiplicity, version) as if universal. SWG formats are 15 years of accreted variants across forks; "it round-trips my one file" is not coverage.

**How to avoid:**
- Port the codec from the authoritative reference, not from guesswork: `swg-client-v2/src/.../sharedTerrain` + `clientTerrain` is the terrain format spec (read-only reference per `project_swg_client_v2_reference`); the Blender `swg_iff` Python is a cross-check.
- Apply the established degrade-don't-abort rule: unknown chunk/version → raw-fallback passthrough that *preserves* bytes for round-trip (the OT fix pattern, `project_ot_multichunk_list_params` FIXED d68387f), never a hard abort that loses data.
- Golden-fixture both client lineages (SWGEmu *and* Restoration) per the TRE-version lesson; build the synthesized ≤200-byte fixtures the Tier-2 harness already uses (DEC-C3), and a byte-exact round-trip CLI verb (`roundtrip-*`) before the editor UI is trusted.
- Verify the IFF chunk no-pad behavior is inherited correctly by the new codec (`project_swg_iff_no_pad`) — don't reintroduce the phantom-pad bug.

**Warning signs:**
Codec throws/aborts on a real-world `.trn` from a shard; round-trip output differs from input in byte count; "open in editor → unexpected end of stream" (the `CompressedSize=0` / pad-byte class of symptom); coverage measured in "my one file" not a fixture matrix.

**Phase to address:**
Terrain editor phase and Effects editor phase — codec + golden round-trip *before* the WinForms panel (verbs-first, DEC-V2-VERBS-FIRST).

---

### Pitfall 8: WinForms SubPanel layout/MEF-load failures that look like editor bugs — the Dock-z-order and ctor-throw traps

**What goes wrong:**
The new Terrain/Effects SubPanel ships a `Dock.Fill` content region next to `Dock.Top`/`Dock.Bottom` toolbars, sends the Fill region to back "to be safe," and the main region renders empty (Fill docked first and ate the rect — `feedback_winforms_dockfill_zorder`, lived in 07-04b). Or a `SplitContainer` sets `SplitterDistance` before `Size`, the ctor throws, and MEF silently rejects the whole plugin so the panel never appears at all (`project_tre_version_support_gap`-era 07-02 gotcha). Both look like "my editor is broken" but are pure WinForms/MEF wiring.

**Why it happens:**
WinForms docking is processed in reverse z-order (back-most docks first) and a thrown ctor inside an `IEditorPlugin` is swallowed by MEF composition — neither is intuitive, and Terrain/Effects panels are layout-heavy (viewport + property grid + layer tree) so they hit both.

**How to avoid:**
- Keep the `Dock.Fill` region front-most (add first / `BringToFront()`), never `SendToBack()` it. For the multi-section terrain/effects panes (viewport + layer tree + properties), use nested `SplitContainer`s and set a definite `Size` *before* `SplitterDistance`.
- Don't let any `IEditorPlugin`/SubPanel ctor throw — guard it; a throw = silent MEF non-activation, not a visible error.
- If the panel embeds a *live* SWG viewport (terrain preview reparented like Issue #10), inherit the owned-popup Z-order fix: after `GWLP_HWNDPARENT`, `SetWindowPos(HWND_TOP, …, SWP_NOACTIVATE)` and drop `SWP_NOZORDER`, or the embedded view is buried (`feedback_owned_popup_zorder`).

**Warning signs:**
Panel region blank despite controls present; plugin entirely absent from TJT after a build (silent MEF reject); embedded SWG view invisible until the editor closes (Z-order bury).

**Phase to address:**
Terrain editor phase and Effects editor phase (UI plan), reusing the Wave-1 SubPanel conventions.

---

### Pitfall 9: Animation/movement (or render) weirdness blamed on the new code — when stale loose-override searchPaths are shadowing data machine-wide

**What goes wrong:**
After wiring terrain/effects preview, the injected session shows wrong terrain, missing effects, or the phantom-walk/bind-pose animation bug — and the team bisects the new render/editor code for a day. The actual cause is `swgemu_live.cfg` priority-27 `searchPath_NN_27` entries (left over from `swg-blender-plugin` validation) that shadow retail data for *every* client run, vanilla and injected alike (`project_swg_client_loose_overrides`, the 2026-06-12 phantom-walk that burned a full day). Terrain/effects work increases exposure because those overrides sit at priority 27, above every `.tre` (max 25), so a stale override `.trn`/effect file silently wins.

**Why it happens:**
The override dirs live outside any repo, nothing removes them, and the blender plugin's docs instruct adding them. They look like a Utinni/editor problem because they only manifest in-client.

**How to avoid:**
- When terrain/effects/animation looks wrong in an injected session, **re-test the vanilla (non-injected) baseline first** and check `swgemu_live.cfg` searchPaths + the override dirs (a 30-second check, same discipline as `feedback_d3d9_hook_diagnosis`). If vanilla is also wrong, it's not Utinni.
- When every bisect arm returns the same wrong result, suspect the baseline, not the next arm.
- Document the override-dir check in the Terrain/Effects live-smoke runbook.

**Warning signs:**
Both vanilla and injected clients misbehave identically; every bisect arm identical; wrong asset loads that no Utinni edit explains; `searchPath_NN_27` present in `swgemu_live.cfg`.

**Phase to address:**
Cross-cutting — bake the baseline-first check into every editor phase's live-smoke step; it's a verification-protocol item, not a build task.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Install both D3D9 + D3D11 hooks unconditionally, let the dead one no-op | No backend-detection code needed | Doubled input, two ImGui contexts, leaks, undiagnosable "works on D3D9 not D3D11" reports | **Never** — backend detect is one `GetModuleHandle` call (Pitfall 2) |
| Commit the regenerated `Generated/UtinniCore.cs` from the CppSharp bump without a per-block hash diff | Looks "done" | A real ABI change hides in reorder churn → silent plugin breakage at inject (Pitfall 4) | Never for the bump phase; the per-block hash gate is mandatory there |
| Keep the 14.29-STL parser redirect "temporarily" if the bump can't reach v145 | Build stays green | Indefinite hidden config; one C++23 `#include` silently breaks codegen | Acceptable **with** the C++23-header tripwire (Pitfall 5); never silently |
| `.trn`/effects codec that aborts on unknown chunk/version | Ships against one fixture fast | Data loss / round-trip failure on real shard files; the OT/IFF/TRE rework tax, ×3 already paid (Pitfall 7) | Never — raw-fallback passthrough is the established rule |
| Per-frame allocation in a live-preview hook to get a quick demo | Live preview "works" in a static scene | `0x0051fb0a` scene-change crash; 11-bisect-cycle debugging (Pitfall 6) | Never on the render/update hot path; use save-then-reload preview instead |
| Reparent a live SWG terrain viewport into the panel before the D3D11 path is stable | Flashy demo | Couples a feature to the in-flux render hook; resize/Reset/RTV-unbind crashes | Only after the D3D11 foundation phase verifies resize handling |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| SWG D3D11 swapchain (`gl11_r.dll`) | Hook `D3D11CreateDevice` or `ID3D11DeviceContext::Present` | Hook `IDXGISwapChain::Present` (vtbl idx 8) + `ResizeBuffers` (idx 13), acquired from a throwaway `D3D11CreateDeviceAndSwapChain` (Pitfall 1) |
| DXGI flip-model RTV | Assume RTV stays bound across frames (D3D9 habit) | Rebind backbuffer RTV every frame before overlay draw; release+recreate it inside the `ResizeBuffers` hook (Fact 2, Pitfall 3) |
| `swg-client-v2` reference tree | `#include`/build against it (drags in the live D3D9→D3D11 churn) | Read-only reference; pin SHA; lift-and-shift any ported code (DEC-V2-LIFT-SHIFT) |
| CppSharp generated bridge | Treat regen as pure churn and skip the diff | Per-block hash gate to separate real ABI change from reorder churn (Pitfall 4) |
| `UtinniPlugins` (TJT/Sytner) DLLs | Ship new bridge without rebuilding plugins | Paired cross-repo rebuild in the same milestone; frozen-DLL MEF-compose fixture (Pitfall 4) |
| `s207_r.dll` shader-compile override | Assume it loaded at preferred base | Already guarded (relocate + `VirtualQuery` exec check, `directx9.cpp:576-615`); the D3D11 path needs its own shader-compat story, not this D3D9 hack |
| `swgemu_live.cfg` searchPaths | Assume in-client wrongness = Utinni bug | Vanilla-baseline-first check; priority-27 overrides beat all `.tre` (Pitfall 9) |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Per-frame heap alloc in preview/dispatch hook | AV at `GroundScene::ctor` (`0x0051fb0a`) on scene change | Stack-allocated `dispatchSnapshot`, `kInlineCap=16`; push data on-edit not per-frame | Every scene/zone change (terrain regen window) |
| RTV/backbuffer reacquired every frame under D3D11 | Frame-time spike, debug-layer churn | Cache RTV; recreate only inside `ResizeBuffers` hook | Continuous, worsens at high res |
| `.trn` full-tree parse on every preview tick | UI stalls on large terrain | Parse once, diff/patch incrementally | Large procedural-terrain files |
| Throwaway D3D11 device left alive after vtable harvest | DXGI device/factory leak | Release dummy device/factory/window like `getVtbl()` does (`directx9.cpp:520-522`) | Accumulates across re-inject cycles |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| New editors writing edits to retail `.tre` instead of loose-override | Corrupting the user's base install; ToS exposure | Honor the loose-override-default write path (MCP/CLI precedent); never default-write retail archives |
| `.trn`/effects codec trusting file-declared sizes/counts unbounded | Malformed shard file → OOB read/alloc in the injected x86 process (crashes SWG) | Bounds-check declared sizes against actual stream length (the `CompressedSize=0` / `unexpected end of stream` class); null-check pattern-scans (CON-H-02) |
| New D3D11 hook in `DllMain` / heavy init at attach | Loader-lock deadlock | Defer to `utinni_init`/first SWG callback (CON-H-01), exactly as `initPresentBlockedEvent`/`initDepthTexture` already do |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Promising live in-client terrain/effects preview that crashes on scene change | Tool feels unstable; loses the injection differentiator | Ship save-then-reload preview honestly first (RESID-03/SC3 candor pattern); add live preview only when heap-free + resize-safe |
| Overlay cursor drifts / right-edge dead-zone under D3D11 | Clicks miss; unusable at maximized | Verify RT-space mapping under DXGI (Pitfall 3); re-check `ClipCursor` dead-zone empirically |
| Backend mismatch leaves overlay invisible on D3D11 clients with no error | User on `rasterMajor=11` sees no Utinni UI, no diagnostic | One-shot backend-detection log at install (Pitfall 2) so support can read it instantly |

## "Looks Done But Isn't" Checklist

- [ ] **D3D11 overlay:** Renders one frame in a menu, but verify it survives a **scene change** and a **window resize** (`ResizeBuffers`) without crashing — flip-model RTV-rebind is the usual missing piece.
- [ ] **D3D11 path:** Works on a `rasterMajor=11` client — but verify a `rasterMajor=5/6/7` (D3D9) client still uses the D3D9 path and isn't running both hooks.
- [ ] **CppSharp bump:** `dotnet test` green — but verify **TJT and Sytner DLLs still MEF-compose at live inject** (the only place `MissingMethodException` surfaces).
- [ ] **CppSharp bump:** Redirect "removed" — but verify the parser actually parses 14.5x STL, not that you re-pinned a different STL silently.
- [ ] **`.trn` codec:** Round-trips your sample — but verify against a real shard `.trn` AND both SWGEmu and Restoration lineages, with raw-fallback on unknown chunks.
- [ ] **Effects codec:** Decodes one effect — but verify the IFF no-pad behavior is inherited (no phantom pad byte) and multi-chunk variants don't truncate.
- [ ] **Live preview:** Updates in a static scene — but verify it's **heap-free on the render/update hot path** (no `0x0051fb0a` on TJT-warp scene change).
- [ ] **New SubPanel:** Appears and lays out — but verify the ctor can't throw (silent MEF reject) and the `Dock.Fill` region isn't starved.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Both render hooks installed live | LOW | Add the `GetModuleHandle("gl11_r.dll")` backend switch; gate each `detour()` behind it |
| Plugin DLLs broken by CppSharp ABI change | MEDIUM | Per-block-hash the regen to find the changed symbol; rebuild TJT/Sytner in the paired repo; add frozen-DLL compose fixture to catch recurrence |
| CppSharp bump can't reach v145 | LOW | Revert to the 14.29 redirect; re-scope phase to "harden redirect + C++23 tripwire"; document as supported config |
| `.trn` codec aborts on shard files | MEDIUM | Add raw-fallback passthrough; capture the failing file as a new golden; widen the fixture matrix |
| Per-frame-alloc scene-change crash | HIGH (if shipped) | Bisect via the TJT-warp repro + `[DebugBisect]` skip-groups (commit 04fa26d); convert preview hook to stack-snapshot dispatch; consider CODEX consult (precedent set) |
| Wrong data in-client from loose overrides | LOW | Re-test vanilla baseline; clean `searchPath_NN_27` from `swgemu_live.cfg` and the two override dirs |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. D3D11 hook acquisition model differs from D3D9 | D3D11 foundation (first plan) | Overlay renders via `IDXGISwapChain::Present` hook; RTV rebound each frame |
| 2. Two render hooks live at once | D3D11 foundation (first deliverable) | Backend-detect log shows exactly one path installed per `rasterMajor` |
| 3. DXGI resize ≠ D3D9 Reset semantics | D3D11 foundation | No crash on maximize/restore/resolution-change; `ResizeBuffers` hook recreates RTV |
| 4. CppSharp regen breaks pre-built plugin DLLs | v145/CppSharp upgrade (primary gate) | Per-block hash diff clean OR plugins rebuilt; live inject composes TJT+Sytner; frozen-DLL fixture green |
| 5. CppSharp bump doesn't reach v145 | v145/CppSharp upgrade (first spike) | clang-capability spike result documented; C++23-header tripwire in CI |
| 6. Per-frame heap alloc on hot path | Terrain + Effects + D3D11 phases | TJT-warp scene change stable ("naked but in world", no `0x0051fb0a`) |
| 7. Codec aborts on unfixtured variants | Terrain + Effects (verbs-first) | Byte-exact `roundtrip-*` verb green across SWGEmu+Restoration fixtures; raw-fallback on unknown |
| 8. WinForms Dock/MEF layout traps | Terrain + Effects (UI plan) | Panel present + non-empty regions; ctor guarded; embedded view Z-ordered |
| 9. Loose-override data shadowing | All editor phases (live-smoke step) | Vanilla baseline checked before any injected bisect |

## Sources

- **Utinni captured incidents (auto-memory — highest predictive value):** `feedback_d3d9_reset_third_party`, `feedback_imgui_embedded_d3d9_rt_space`, `feedback_owned_popup_zorder`, `feedback_winforms_dockfill_zorder`, `feedback_caller_attrs_binary_compat`, `project_utinnicore_cs_regen_churn`, `project_vs2026_cppsharp_block`, `project_rh_snapshot_no_heap_alloc`, `project_swg_cursor_clip_deadzone`, `feedback_d3d9_hook_diagnosis`, `project_swg_client_loose_overrides`, `project_d3d11_migration`, `project_ot_multichunk_list_params`, `project_swg_iff_no_pad`, `project_tre_version_support_gap`, `project_scene_change_via_tjt`, `project_tjt_scene_change_naked_baseline`, `project_swg_client_v2_reference`. (HIGH)
- **Direct source inspection (this milestone's touch points):** `UtinniCore/swg/graphics/directx9.cpp` (hook model, 7 detours, dummy-device vtable harvest, `hkReset` bracket, `s207_r.dll` guard); `swg-client-v2/.../clientGraphics/src/win32/Graphics.cpp:185-253` (`gl%02d_r.dll` backend selection by `rasterMajor`); `swg-client-v2/.../Direct3d11/src/win32/Direct3d11_Device.cpp` (`D3D11CreateDevice` + `CreateSwapChainForHwnd`, `DXGI_SWAP_EFFECT_FLIP_DISCARD`, RTV-unbind-after-Present, DEVICE_REMOVED = restart). (HIGH)
- **Project planning context:** `.planning/PROJECT.md` (CON-H/N/M/T constraints, DEC-V2 locks, v2.1 milestone scope), `docs/ai/toolchain-inventory.md` (revive/replace cross-walk, `.trn`/effects targets, lift-and-shift). (HIGH)

---
*Pitfalls research for: Utinni v2.1 "Wave-2 Editors + Foundation Hardening" — Terrain + Effects editors on a D3D11/v145-hardened base*
*Researched: 2026-06-14*
