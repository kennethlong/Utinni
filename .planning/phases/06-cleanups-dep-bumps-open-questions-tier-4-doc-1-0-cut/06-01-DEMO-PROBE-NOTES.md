# 06-01 Demo Probe Notes

**Plan:** 06-01 (Overlay-debug investigation)
**Started:** 2026-05-23
**Owner:** Kenneth Long (kenny.alan.long@gmail.com)

## Investigation Charter

Close the long-standing concern that the imgui in-game overlay has never displayed in Utinni-injected SWG sessions. Per [[feedback-d3d9-hook-diagnosis]], the d3d9.dll pattern-scan check is the 30-second first move BEFORE assuming SWG-side RVA drift or multi-day investigation. Exit gate is intentionally high: `ImGui::ShowDemoWindow` exercising the full Demo screen (menus, sliders, buttons, tabs, plots, popups, drag-and-drop) over a live SWG client.

## d3d9 Pattern-Scan Disposition (Task 1)

**Disposition: ALREADY NOT THE CAUSE. Resolved 2026-05-19 by Phase 02.1 commit `2c57d38`.**

The [[feedback-d3d9-hook-diagnosis]] memory's "test pattern-scan FIRST" advice resolves in 30 seconds here because the broken pattern-scan was already replaced with the dummy-device approach four days before this plan opened.

### Evidence #1 — `UtinniCore/swg/graphics/directx9.cpp::getVtbl()` is the dummy-device path

The current `getVtbl()` body (lines 436-526) creates a throwaway IDirect3DDevice9 via the public D3D9 API, snapshots its vtable, then releases. The vtable entries point into d3d9.dll's `.text` section and remain valid after the dummy device is released because we patch the function bodies there rather than mutating any vtable. The header comment block is explicit:

> 2026-05-19 — Replaced the d3d9.dll code-pattern scan that broke on modern Windows (probe of Win11 24H2 d3d9.dll 6.2.26100.8328 showed the IDirect3DDevice9 vtable is allocated per-instance on the heap, NOT as a static array in d3d9.dll's read-only data — modern d3d9 ships without an .rdata section at all). The new approach creates a throwaway IDirect3DDevice9 via the public D3D9 API, snapshots its vtable, and releases. The method addresses inside the vtable point into d3d9.dll's .text section (verified 119/119 entries) and remain valid after the dummy device is released, because we patch the function bodies there rather than mutating any vtable. This works identically against the SWG Source build and the stock SWGEmu client because both load the OS-provided d3d9.dll.

The function uses `Direct3DCreate9` + a hidden 1x1 `WS_POPUP` window + `CreateDevice(D3DDEVTYPE_HAL, ...)` + a `memcpy(s_vtbl, liveVtbl, sizeof(swgptr) * kD3D9VtblEntries)` snapshot of 119 entries (3 IUnknown + 116 D3D9-specific), then releases the device + destroys the window + releases the D3D9 interface. HAL is mandatory — NULLREF/REF would return a different IDirect3DDevice9 implementation whose addresses don't intercept HAL Present calls.

### Evidence #2 — STATE.md "Blockers/Concerns" item #2

The state file explicitly marks the pattern-scan investigation as resolved:

> **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

### Conclusion

Pattern-scan is **not** the cause of the overlay never displaying. The 30-second first move is done; the chain has already moved on. Next probe (Task 1's instrumentation) is to confirm the `isSetup` gate in `imgui_impl.cpp` actually flips true in a live SWG session, and that `render()`'s `if (isSetup)` body actually executes per-frame after that flip. Both diag log lines below are static-bool-guarded so they fire exactly once.

## Task 1 Diag Instrumentation Probes

Two one-shot `utinni::log` calls landed in `UtinniCore/swg/ui/imgui_impl.cpp`:

1. **`info`-level** at the bottom of `imgui_impl::setup(IDirect3DDevice9*)`, guarded by `static bool sLoggedOnce`. Fires the first time `isSetup` flips true. Expected line in utinni.log: `imgui_impl::setup complete, isSetup=true`.
2. **`debug`-level** at the top of the `if (isSetup)` branch in `imgui_impl::render()`, guarded by `static bool sLoggedOnceRender`. Fires the first time render() crosses the gate. Expected line in utinni.log: `imgui_impl::render entered isSetup branch`.

Both probes are dormant after first fire — no per-frame log spam. They survive into production as latent regression detectors and are converted to `debug`-only at Task 3 sign-off.
