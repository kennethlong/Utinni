# Hard-Won Engineering Lessons — Utinni

Distilled from incidents during v1.0/v2.0 development. Each entry: **symptom → root cause → fix.** These
are the traps that cost real time; consult before working in the matching area. Source-of-truth for the
runbook is [`../../AGENTS.md`](../../AGENTS.md); Claude also keeps fuller per-incident notes in auto-memory
(slugs referenced as `memory:<slug>`).

## Rendering & overlay (D3D9 + ImGui)

- **Never call `IDirect3DDevice9::Reset` on a third-party app's device.** It owns untracked default-pool
  resources → `Reset` returns `D3DERR_INVALIDCALL`, the device goes DEVICELOST, and the app crashes on the
  next render. Modern D3D9 windowed `Present` handles backbuffer-vs-window mismatches itself — just resize
  the window. (`memory:feedback_d3d9_reset_third_party`) **The D3D11 analog:** release/recreate the RTV
  inside the `ResizeBuffers` hook; never tear down the swapchain.
- **The embedded overlay runs in render-target space.** The reparented SWG window is stretched onto an
  un-`Reset` backbuffer, so `DisplaySize` AND the mouse must both be scaled to RT space. Set `io.MousePos`
  via `AddMousePosEvent` in imgui 1.87+ (poking it pre-`NewFrame` is clobbered).
  (`memory:feedback_imgui_embedded_d3d9_rt_space`)
- **Right-edge cursor dead zone is SWG's own `ClipCursor`, not an imgui scale bug.** SWG clips the OS
  cursor to its backbuffer rect; when the panel is stretched wider than the backbuffer (maximized), the
  rightmost ~175px is cursor-dead. RT-space mapping is correct. (`memory:project_swg_cursor_clip_deadzone`)
- **Diagnose "ImGui won't render" by testing the `d3d9.dll` pattern-scan FIRST** (30 sec) before assuming
  SWG-side RVA drift (multi-day hunt). (`memory:feedback_d3d9_hook_diagnosis`)

## Injection, detours & native hot paths

- **Prefer `DETOUR_LEN_AUTO` with DetourXS.** An explicit `detourLen < minDetLen` silently corrupts the
  target and overflows `pbPatchBuf` by one byte. Root cause of the 2026-05-19 post-preload halt.
  (`memory:feedback_detourxs_explicit_len`)
- **No heap allocation on per-frame callback-dispatch paths.** A per-frame `std::vector::reserve()` in
  callback dispatch fragmented SWG's allocator and crashed scene change at `0x0051fb0a`. Use a
  stack-allocated fixed-size snapshot (`kInlineCap=16`; the `dispatchSnapshot` template in
  `ground_scene.cpp`). (`memory:project_rh_snapshot_no_heap_alloc`)
- **The PE-entry `jmp`-self is Utinni's OWN Launcher** (`main.cpp:351`), not SWGEmu. The restore path is
  blocked behind `Application.Run`. Reusable pattern for any injector whose in-process init blocks.
  (`memory:project_eb_fe_patch_origin`)
- **CRT vs SWG startup fingerprint:** `GetVersion` + four globals (`_osver`/`_winver`/`_winmajor`/
  `_winminor`) + fatal codes `0x10`/`0x1C` = MSVC CRT startup, NOT SWG `Os::install` (which uses
  `GetVersionEx` with a struct arg). Saves days of source-side chasing. (`memory:feedback_crt_vs_swg_fingerprint`)
- **Loose-override shadowing:** `swgemu_live.cfg` priority-27 `searchPaths` (blender-plugin validation
  leftovers) shadow retail data machine-wide — caused the 2026-06-12 phantom-walk. **Re-test the vanilla
  baseline before bisecting injection.** (`memory:project_swg_client_loose_overrides`)
- **Hardcoded RVAs are poison on the advertised (non-SWGEmu) client; defend by per-subsystem capability
  guard, NOT by per-call source-tagging.** When the same injected code runs against two targets — SWGEmu
  (legacy; every engine fn is a hardcoded RVA literal) and the advertised DX11 client (exports
  `GetEngineHookPoints()`; the resolver overwrites bound slots) — any *unbound* RVA reached on the advertised
  client is garbage → crash (e.g. `generateHighestId` → the offline `worldSnapshotReaderWriter::openFile`,
  which only fired once `treeFile::enumerateFiles` populated the Repository). Crash taxonomy: (1) resolved
  slot = safe; (2) bound-but-missed = still the SWGEmu literal; (3) unbound raw literal = the resolver never
  touches it; (4) advertised-only null slot; (5) **resolved-but-wrong** — advertised addr present + executable
  but ABI/precondition mismatch, uncatchable by ANY source tag. The fix that pays is a per-subsystem
  `isAdvertisedClient()` guard that degrades the editor path to a no-op (centralize it: one
  `offlineSnapshotUnavailable()`-style helper per domain, not scattered reads). Crew review (2026-06-25)
  killed a tempting per-call `slotSafeOnAdvertised` runtime guard: it only catches state 2 (never observed
  in practice) while giving *false* coverage for the states we actually hit (3, and a null-object that is not
  a slot problem at all). Resolver source-tags are worth keeping ONLY as init-time telemetry (log a
  bound-but-missed name at resolve, never assert in a live client). (WS-3: `9f476cd`/`aaae8b1`/`f74b6ca`)

## SWG reverse-engineering gotchas

- **SWG's key-context selector breaks under injection:** in-game Enter dispatches as `chatEnter`
  (input-mode) instead of `openChat` (game-mode). Fix pattern: detour the wrong-context handler at
  `0x00F3E420` and override its stateless behavior. (`memory:project_swg_context_routing`)
- **Esc in SWG = `untarget`, not `gameMenuActivate`.** Don't assume WoW-era key conventions. Verify a
  "broken" key by dumping `val1[0]` in `hkActionPerformAction` before blaming injection.
  (`memory:project_swg_keymap_reality`)
- **Scene changes are TJT-driven** (the chat-command parser); **landing naked after a scene change is the
  baseline, NOT a regression.** Never disable TJT in a bisect that needs the scene-change repro.
  (`memory:project_scene_change_via_tjt`, `memory:project_tjt_scene_change_naked_baseline`)

## WinForms / UI

- **Owned-popup Z-order needs `HWND_TOP`.** Setting `GWLP_HWNDPARENT` post-creation does NOT recompute
  Z-order — the popup stays buried. Fix: `SetWindowPos(HWND_TOP, …)` without `SWP_NOZORDER`, with
  `SWP_NOACTIVATE` to avoid focus theft. (`memory:feedback_owned_popup_zorder`)
- **A `Dock.Fill` control must be front-most.** Sent to back, it docks first and grabs the whole rect,
  starving `Top`/`Bottom` siblings. Keep Fill at front (add first / `BringToFront`); for multi-section
  panes prefer nested `SplitContainer`s (set `Size` before `SplitterDistance`).
  (`memory:feedback_winforms_dockfill_zorder`)
- **A `Dock.Fill` `SplitContainer` ignores an explicit `Size` while unparented — defer `SplitterDistance`
  to `OnShown`.** Setting `Size` on a `Dock.Fill` split before it has a parent/handle does NOT stick (the
  layout engine owns its bounds), so its width stays at the ~150px default and any `SplitterDistance`
  (or a `Panel1MinSize` wider than that default) is out of range → the setter throws. In a guarded
  build (try/catch around content construction) this silently drops the editor to its failure surface —
  it looks like a "blank window". Fix: build the split with no min-sizes / no `SplitterDistance`, then in
  `OnShown` (real client width) set `SplitterDistance` FIRST (min-sizes still the 25px default), clamp it
  into `[Panel1MinSize, Width − Panel2MinSize − SplitterWidth]`, THEN tighten the min-sizes. This
  supersedes the "just set `Size` before `SplitterDistance`" advice above for the Fill case. Found in the
  22-04 ClientEffect-editor live smoke; a process-isolated harness (`new Form…(null)` → assert
  `Controls.Count`) reproduced it in seconds where the live inject only showed "blank". (Phase 22-04)
- **A WinForms form's `Shown` event may not fire under `Application.Run` headless.** For a borderless
  custom-painted singleton, driving it from a harness via `Application.Run` + a `Shown` handler can hang
  with the handler never invoked. Use `form.Show()` + a few `Application.DoEvents()` pumps, then drive it
  synchronously on the STA thread. (Phase 22-04 interactive harness)
- **Locking around event `+=`/`-=` must wrap the USE window too.** Narrow add/remove-only locking still
  lets two threads run with both handlers installed. (`memory:feedback_event_handler_lock_scope`)

## Binding generation & plugin ABI

- **Adding defaulted `[Caller*]` / new params to a public C# method is NOT binary-compatible.** Pre-built
  plugin DLLs throw `MissingMethodException` at MEF compose. Rebuild every cross-binary plugin in the same
  commit, or add a 1-arg binary-compat shim. **This is the dominant risk of any CppSharp regen** — a
  changed `Generated/UtinniCore.cs` public signature detonates TJT/Sytner DLLs, hidden inside the routine
  reorder churn. (`memory:feedback_caller_attrs_binary_compat`)
- **`Generated/UtinniCore.cs` reorders on every build** (CppSharp) — a huge symmetric no-op diff. Always
  `git checkout --` it; never commit. (`memory:project_utinnicore_cs_regen_churn`)

## Formats & codecs

- **Real SWG datatable `.iff` chunks are NOT word-padded.** A strict pad consumes a phantom byte →
  `MalformedFourCc`. The IFF reader now detects the pad (consume only a real `0x00`).
  (`memory:project_swg_iff_no_pad`)
- **TRE `CompressedSize=0` means UNCOMPRESSED**, not zero bytes. On-disk length =
  `compressor==0 ? UncompressedSize : CompressedSize`. Caused the "unexpected end of stream" bug on
  uncompressed patch-`.tre` records. (`memory:project_tre_uncompressed_zero_compressedsize`)
- **Object-template list/array params = a named header chunk + N nameless `StructParamOT` element
  chunks** (draft-schematic slots/attributes, hair tint lists), not single-chunk `name\0value`. The codec
  degrades nameless chunks to raw hex-fallback; full typed decode is a Tier-2 follow-up.
  (`memory:project_ot_multichunk_list_params`)

## Build & CI

- **`dotnet build` can't compile the WinForms projects** (MSB3823 on `.resx` images). Build with VS2026
  MSBuild; run xUnit via `dotnet test --no-build`. (`memory:feedback_dotnet_build_msbuild_resources`)
- **`Debug/` gitignore swallows lifted `tools/.../debug/` source** case-insensitively on Windows → CI
  C1083 (local green). (`memory:project_ci_debug_gitignore_trap`)
- **De-flake inside CI-covered code, never by touching the injection hot path CI can't validate.** The
  LoaderLockHarness 50 ms-threshold flake was hardened with a best-of-3 min. (`memory:project_loader_lock_harness_ci_flake`)
- **Ratchet a large brownfield invariant against a committed baseline; don't try to fix it all at once.**
  With ~320 hardcoded RVA / `memory::` sites already in the tree, a "guard-or-allowlist every literal" CI
  gate is infeasible (you'd hand-allowlist the whole codebase). Instead the audit auto-generates a classified
  baseline that grandfathers today's inventory, then CI **fails hard only on NEW unbaselined sites** — every
  new RVA must be consciously guarded or justified with a Reason. Keep it source-only (no binary, no egress)
  so it runs next to the clang-format / C++23 gates and not behind the build. Two traps: `-UpdateBaseline`
  must PRESERVE existing Reasons or a regen silently wipes the annotations the gate exists to enforce; and a
  PowerShell `.ps1` invoked with `&` *does* set `$LASTEXITCODE` from its `exit N` (verified) — so the CI
  step's `if ($LASTEXITCODE -ne 0) { throw }` idiom works. (`scripts/audit-advertised-rva-safety.ps1`, `f74b6ca`)
