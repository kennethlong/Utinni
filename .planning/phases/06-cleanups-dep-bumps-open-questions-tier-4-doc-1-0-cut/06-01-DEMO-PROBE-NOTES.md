# 06-01 Demo-Probe Notes — imgui overlay never-displays investigation

**Plan:** 06-01
**Phase:** 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
**Maintainer:** Kenneth Long
**Started:** 2026-05-23
**Status:** In progress (Tasks 1+2 shipped; Task 3 awaiting live-SWG sign-off)

---

## Probe 0: d3d9 pattern-scan disposition (the [[feedback-d3d9-hook-diagnosis]] 30-sec first move)

**Memory cited:** `[[feedback-d3d9-hook-diagnosis]]` — "when ImGui doesn't render in Utinni-injected sessions, test the d3d9.dll pattern-scan FIRST (30 sec) before assuming SWG-side RVA drift (multi-day investigation)."

**Disposition:** **ALREADY-NOT-THE-CAUSE.** Pattern-scan was replaced with the dummy-device approach in commit **`2c57d38`** on **2026-05-19** (commit message: "fix: replace broken d3d9.dll pattern-scan with dummy-device vtable harvest").

### Evidence A — verbatim quote of `directX::getVtbl()` body (`UtinniCore/swg/graphics/directx9.cpp:426-526`)

```cpp
// 2026-05-19 — Replaced the d3d9.dll code-pattern scan that broke on modern
// Windows (probe of Win11 24H2 d3d9.dll 6.2.26100.8328 showed the IDirect3DDevice9
// vtable is allocated per-instance on the heap, NOT as a static array in
// d3d9.dll's read-only data — modern d3d9 ships without an .rdata section at all).
// The new approach creates a throwaway IDirect3DDevice9 via the public D3D9 API,
// snapshots its vtable, and releases. The method addresses inside the vtable point
// into d3d9.dll's .text section (verified 119/119 entries) and remain valid after
// the dummy device is released, because we patch the function bodies there
// rather than mutating any vtable. This works identically against the SWG Source
// build and the stock SWGEmu client because both load the OS-provided d3d9.dll.
swgptr* getVtbl()
{
    static swgptr s_vtbl[kD3D9VtblEntries];
    static bool s_initialized = false;
    if (s_initialized) return s_vtbl;

    // Dynamic load of Direct3DCreate9 — avoids adding d3d9.lib to the link line.
    // d3d9.dll is loaded by SWG before utinni_init runs (the launcher injects after
    // the game has bootstrapped its render subsystem); in the test process the
    // xUnit harness LoadLibraryAs it explicitly.
    HMODULE hD3d9 = GetModuleHandleA("d3d9.dll");
    if (hD3d9 == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: d3d9.dll not loaded");
        return nullptr;
    }

    typedef IDirect3D9* (WINAPI *PFN_Direct3DCreate9)(UINT);
    auto pfnDirect3DCreate9 =
        (PFN_Direct3DCreate9)GetProcAddress(hD3d9, "Direct3DCreate9");
    if (pfnDirect3DCreate9 == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: Direct3DCreate9 not exported by d3d9.dll");
        return nullptr;
    }

    IDirect3D9* pD3D = pfnDirect3DCreate9(D3D_SDK_VERSION);
    if (pD3D == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: Direct3DCreate9 returned null");
        return nullptr;
    }

    // Hidden 1x1 window — required as the hDeviceWindow. Never shown, never pumped.
    HWND hwnd = CreateWindowExA(0, "STATIC", nullptr, WS_POPUP, 0, 0, 1, 1,
                                nullptr, nullptr, GetModuleHandleA(nullptr), nullptr);
    if (hwnd == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DirectX9 hook installation failed: dummy window creation failed (GetLastError=0x%08lX)",
                 GetLastError());
        pD3D->Release();
        utinni::log::critical(msg);
        return nullptr;
    }

    D3DPRESENT_PARAMETERS pp = {};
    pp.BackBufferWidth = 1;
    pp.BackBufferHeight = 1;
    pp.BackBufferFormat = D3DFMT_X8R8G8B8;
    pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
    pp.Windowed = TRUE;
    pp.hDeviceWindow = hwnd;
    pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;

    // HAL is mandatory: SWG uses HAL, so HAL's vtable is what we need to harvest.
    // NULLREF/REF can return different IDirect3DDevice9 implementations whose
    // function addresses don't intercept HAL Present calls — falling back to them
    // would be a silent miss in production.
    IDirect3DDevice9* pDevice = nullptr;
    HRESULT hr = pD3D->CreateDevice(
        D3DADAPTER_DEFAULT,
        D3DDEVTYPE_HAL,
        hwnd,
        D3DCREATE_SOFTWARE_VERTEXPROCESSING | D3DCREATE_DISABLE_DRIVER_MANAGEMENT,
        &pp,
        &pDevice);

    if (FAILED(hr) || pDevice == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DirectX9 hook installation failed: CreateDevice(HAL) returned 0x%08lX",
                 (unsigned long)hr);
        DestroyWindow(hwnd);
        pD3D->Release();
        utinni::log::critical(msg);
        return nullptr;
    }

    swgptr* liveVtbl = *(swgptr**)pDevice;
    memcpy(s_vtbl, liveVtbl, sizeof(swgptr) * kD3D9VtblEntries);

    pDevice->Release();
    DestroyWindow(hwnd);
    pD3D->Release();

    s_initialized = true;
    return s_vtbl;
}
```

This is the dummy-device approach: `Direct3DCreate9` → hidden 1x1 `STATIC` window → `CreateDevice(HAL)` → snapshot 119 vtable entries → release. No pattern-scan in `d3d9.dll`'s `.rdata`.

### Evidence B — verbatim quote of `.planning/STATE.md` "Blockers/Concerns" item #2

> **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

### Conclusion

**Pattern-scan is NOT the cause of imgui not rendering.** The `[[feedback-d3d9-hook-diagnosis]]` "30-second check" already resolves to "already not the cause" because Phase 02.1 commit `2c57d38` retired the broken pattern-scan. The investigation must move to the next probe: walk the imgui state machine (`isSetup` gate, `hkPresent` → `imgui_impl::render()` invocation, `ImGui_ImplDX9_NewFrame`/`NewFrame`/`Render`/`EndFrame` ordering).

---

## Probe 1: `isSetup` + render-entry one-shot tripwires (Task 1)

**What was added in Task 1 commit:**
- A one-shot `utinni::log::info` line `"imgui_impl::setup complete, isSetup=true"` at the bottom of `setup(IDirect3DDevice9*)` in `UtinniCore/swg/ui/imgui_impl.cpp`, gated behind `static bool sLoggedOnce` so it fires exactly once per process lifetime when `isSetup` first flips true.
- A one-shot `utinni::log::debug` line `"imgui_impl::render entered isSetup branch"` at the top of the `if (isSetup)` branch in `render()`, gated behind `static bool sLoggedOnceRender` so it fires exactly once per process lifetime when render() first crosses the gate.

**Purpose:** triangulate whether the imgui state machine ever reaches a healthy render-ready state during a live SWG session. The tripwires are observation-only — they change no behavior.

**Expected log lines if the gate is healthy:**
```
[info]  imgui_impl::setup complete, isSetup=true
[debug] imgui_impl::render entered isSetup branch
```

The `setup` line fires once from inside `hkPresent`'s tail-call to `imgui_impl::setup(pDevice)` (line 362 in `directx9.cpp`). The `render` line fires once from inside `hkPresent`'s call to `imgui_impl::render()` (line 326 in `directx9.cpp`). **Order:** `render` fires first on frame N (when `isSetup` is still false, so we DON'T see the render log on N), then `setup` flips `isSetup=true` at the bottom of frame N's `hkPresent`, then on frame N+1 the render log fires (because the `if (isSetup)` branch is now entered).

### Live-SWG Observation Result (TASK 1 — PLACEHOLDER, to be filled by maintainer)

> Maintainer: run Launcher.exe against live SWGEmu, log into a character (Tatooine works fine), wait ~5 seconds, then close the client cleanly. Open the most recent `utinni.log` and confirm:
> - Exactly ONE `imgui_impl::setup complete, isSetup=true` info line is present.
> - Exactly ONE `imgui_impl::render entered isSetup branch` debug line is present.
>
> Paste the two log lines (with timestamps) here, plus any anomalies (line missing → setup never completed; line missing → render gate never entered).

```
<placeholder — maintainer to append observation transcript here>
```

---

## Probe 2: `ImGui::ShowDemoWindow` additive probe (Task 2)

**What was added in Task 2 commit:**
- A file-scope `static bool g_showDemoWindowProbe = true;` near the top of `imgui_impl.cpp`.
- A call to `ImGui::ShowDemoWindow(nullptr)` immediately after `ImGui::NewFrame()` inside the `if (isSetup)` branch of `render()`, gated by `if (g_showDemoWindowProbe)`. The existing `renderCallbacks` dispatch path is untouched — the Demo window is additive instrumentation, NOT a replacement.

**Purpose:** the highest-bar exit criterion for the overlay-debug investigation. `ImGui::ShowDemoWindow` exercises menus + sliders + buttons + tabs + plots + popups + drag-and-drop end-to-end. If it renders and is fully interactive over a live SWG client, imgui's render + input + state-mgmt are healthy and the docking-branch switch in 06-02 is unblocked.

### Demo Screen Exercise Result (TASK 2 — PLACEHOLDER, to be filled by maintainer)

> Maintainer: run Launcher.exe against live SWGEmu, log into a character, confirm the ImGui Demo window appears. Exercise EVERY widget category in the Demo screen end-to-end:
> 1. Menus — open the top menu bar, navigate File/Edit/Tools/Help submenus, verify all entries render and respond.
> 2. Sliders — drag every numeric slider in the "Widgets" → "Basic" section; verify the value updates live.
> 3. Buttons — click every button in "Widgets" → "Basic"; verify hover/active states render correctly.
> 4. Tabs — navigate every tab in "Widgets" → "Tabs"; verify tab switching renders the correct page.
> 5. Plots — scroll to "Widgets" → "Plots"; verify the line and histogram plots render without artifacts.
> 6. Popups — open every popup in "Popups & Modal windows"; verify they appear, accept input, and close cleanly.
> 7. Drag-and-drop — navigate to "Widgets" → "Drag and Drop"; verify drag tokens move correctly between targets.
>
> Paste a YES/NO disposition here, plus per-category notes (any glitches, partial render, missing input, crash).

```
<placeholder — maintainer to append YES/NO + per-category observation transcript here>
```

### Disposition

> If YES end-to-end across all seven categories: **no fix required.** The render path is healthy; the prior "never displayed" belief was stale post-Phase 02.1. 06-02 imgui-docking-branch switch is unblocked.
>
> If NO (any category fails): **escalation.** Document the failure mode below and re-open this plan to add fix tasks. Next-probe candidates: `hkReset` state-loss inspection, `ImGui_ImplWin32_Init` HWND-identity check, render-target rebinding inspection, font-atlas size at the active backbuffer dimensions.

```
<placeholder — maintainer to fill with the chosen disposition here>
```

---

## Investigation log

- 2026-05-23 — Plan 06-01 spun up. Task 1 + Task 2 shipped by parallel executor agent on per-agent branch `worktree-agent-aedca507c501c71b9` (base SHA `0dc8646`).
- 2026-05-23 — Task 3 (Tier-4 sign-off + TESTING.md row + diag rollback) pending maintainer live-SWG verification.
